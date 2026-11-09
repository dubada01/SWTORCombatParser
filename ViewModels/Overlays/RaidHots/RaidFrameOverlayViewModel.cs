//using MoreLinq;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Model.Timers;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.Timers;
using SWTORCombatParser.Views.Overlay.RaidHOTs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MoreLinq;
using ReactiveUI;
using SWTORCombatParser.Views;

namespace SWTORCombatParser.ViewModels.Overlays.RaidHots
{
    public class RaidFrameOverlayViewModel : BaseOverlayViewModel
    {
        private readonly ManualRaidFrameAlignment _manualAlignment = new();
        private readonly DispatcherTimer _manualTimeout = new() { Interval = TimeSpan.FromMilliseconds(250) };
        private Window? _manualWindow;
        private CancellationTokenSource? _detectionCancellation;
        private int _layoutVersion;
        private bool _detecting;
        private string _alignmentStatus = "Ready to align raid frames.";
        public string AlignmentStatus
        {
            get => _alignmentStatus;
            private set => this.RaiseAndSetIfChanged(ref _alignmentStatus, value);
        }
        public bool IsManuallyAligning => _isManuallyModifiying;
        public bool ManualAlignmentSupported => OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS();
        private int rows;
        private int columns;
        private bool editable;
        private bool _inCombat;
        private bool _usingDecreasedAccuracy;
        private List<long> _validBossIds = new List<long>();
        private object _cellUpdateLock = new object();
        private ObservableCollection<RaidHotCell> _raidHotCells = new ObservableCollection<RaidHotCell>();
        public override bool ShouldBeVisible => true;
        public bool DisplayGrid => Editable || _isManuallyModifiying;
        private bool _isManuallyModifiying;
        public bool Editable
        {
            get => editable;
            set
            {
                if (_isManuallyModifiying) EndManualAlignment(false);
                if (editable == value) return;
                InvalidateDetection();
                this.RaiseAndSetIfChanged(ref editable, value);
                this.RaisePropertyChanged(nameof(DisplayGrid));
                OverlaysMoveable = editable;
            }
        }
        public bool CanDetect => Active && !_detecting && !_isManuallyModifiying;
        public double ColumnWidth => OverlayScaledSize.X / Columns;
        public double RowHeight => OverlayScaledSize.Y / Rows;

        public List<PlacedName> CurrentNames = new List<PlacedName>();
        public bool SizeSet = false;
        private readonly RaidFrameOverlay _raidFrameView;

        public RaidFrameOverlayViewModel(string overlayName):base(overlayName)
        {
            TimerController.TimerTriggered += CheckForRaidHOT;
            TimerController.TimerTriggered += CheckForDefensive;
            CombatLogStreamer.CombatUpdated += OnStartCombat;
            CombatLogStreamer.NewLineStreamed += CheckForTargetChanged;
            CombatLogStreamer.HistoricalLogsFinished += SetCurrentEncounter;
            CombatLogStateBuilder.AreaEntered += NewEncounterEntered;
            HotkeyHandler.OnHideOverlaysHotkey += ToggleHide;
            _raidFrameView = new RaidFrameOverlay(this);
            _raidFrameView.AreaClicked += CellClicked;
            BackgroundLockedOpacity = 0;
            BackgroundUnLockedOpacity = 0.05;
            SettingsType = OverlaySettingsType.Character;
            MainContent = _raidFrameView;
            Settings.SettingsUpdated += SettingsChanged;
            _overlayWindow.Closed += (_, _) => Settings.SettingsUpdated -= SettingsChanged;
            _overlayWindow.PropertyChanged += (_, change) =>
            {
                if (change.Property == Window.IsVisibleProperty && !_overlayWindow.IsVisible)
                {
                    EndManualAlignment(false);
                    InvalidateDetection();
                }
            };
            _manualTimeout.Tick += (_, _) => ExpireManualClick();
            ActiveChanged += enabled => Dispatcher.UIThread.Invoke(() =>
            {
                if (!enabled)
                {
                    EndManualAlignment(false);
                    InvalidateDetection();
                }
                this.RaisePropertyChanged(nameof(CanDetect));
            });
        }
        private void SettingsChanged(string setting)
        {
            if (setting == "offline_mode" && Settings.ReadSettingOfType<bool>(setting))
                Dispatcher.UIThread.Post(InvalidateDetection);
        }
        
        private void OnStartCombat(CombatStatusUpdate update)
        {
            if (update.Type != UpdateType.Start && update.Type != UpdateType.Stop) return;
            Dispatcher.UIThread.Post(() =>
            {
                _inCombat = update.Type == UpdateType.Start;
                if (!_isManuallyModifiying) return;
                ClearPendingClick();
                try { _raidFrameView.SetManualEditing(!_inCombat); }
                catch (Exception error)
                {
                    EndManualAlignment(true);
                    Logging.LogError("Could not resume raid frame click capture: " + error.Message);
                    AlignmentStatus = "Could not resume click capture. Try Assign by click again.";
                    return;
                }
                AlignmentStatus = _inCombat
                    ? "Assignment paused during combat. Your saved names are unchanged."
                    : "Click a player in SWTOR's raid frames, then wait for their name to appear.";
            });
        }
        public ReactiveCommand<Unit, Unit> RefreshFramesCommand => ReactiveCommand.CreateFromTask(DetectPlayersAsync);

        public async Task DetectPlayersAsync()
        {
            if (!CanDetect) return;
            if (!_raidFrameView.TryGetRaidBounds(out var position, out var size, out var scale))
            {
                AlignmentStatus = "Show the raid overlay and match its grid to your raid frames first.";
                return;
            }
            if (Settings.ReadSettingOfType<bool>("offline_mode"))
            {
                AlignmentStatus = "Name detection requires online mode. Use Assign by click while offline.";
                return;
            }
            _detecting = true;
            this.RaisePropertyChanged(nameof(CanDetect));
            var version = _layoutVersion;
            var scanRows = Rows;
            var scanColumns = Columns;
            using var cancellation = new CancellationTokenSource();
            _detectionCancellation = cancellation;
            AlignmentStatus = "Detecting player names… Keep the raid frames visible.";
            try
            {
                // Capture the game's text, without our labels, borders, or timer icons.
                // Give the compositor a frame to remove the overlay before screen capture.
                System.IO.MemoryStream bitmap;
                var opacity = _overlayWindow.Opacity;
                try
                {
                    _overlayWindow.Opacity = 0;
                    await Task.Delay(150, cancellation.Token);
                    cancellation.Token.ThrowIfCancellationRequested();
                    bitmap = await Task.Run(() => RaidFrameScreenGrab.GetRaidFrameBitmapStream(position,
                        (int)size.X, (int)size.Y, scanRows, scale, maskOverlayNames: false), cancellation.Token);
                }
                finally { _overlayWindow.Opacity = opacity; }
                using var capturedBitmap = bitmap;
                var result = await AutoHOTOverlayPosition.DetectCurrentPlayerLayoutAsync(position, bitmap,
                    scanRows, scanColumns, (int)size.Y, (int)size.X, cancellation.Token);
                if (cancellation.IsCancellationRequested || version != _layoutVersion || !Active) return;
                if (!_raidFrameView.TryGetRaidBounds(out var currentPosition, out var currentSize, out _) ||
                    position != currentPosition || size != currentSize)
                {
                    AlignmentStatus = "The grid moved during detection. Detect names again in its new position.";
                    return;
                }
                if (!result.Success)
                {
                    AlignmentStatus = result.ErrorMessage + " Your existing names were kept. Retry or use Assign by click.";
                    return;
                }
                if (result.Names.Count == 0)
                {
                    AlignmentStatus = "No names detected. Existing names were kept. Check grid alignment and make sure SWTOR is visible.";
                    return;
                }
                UpdateNames(result.Names);
                AlignmentStatus = $"Detected {CurrentNames.Count} of {Rows * Columns} slots. Check the names; use Assign by click to correct any mistakes.";
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                Logging.LogError("Failed to detect raid frame names: " + error.Message);
                AlignmentStatus = error.Message + " Existing names were kept.";
            }
            finally
            {
                _detectionCancellation = null;
                _detecting = false;
                this.RaisePropertyChanged(nameof(CanDetect));
            }
        }

        private void InvalidateDetection()
        {
            _layoutVersion++;
            if (_detectionCancellation != null)
            {
                _detectionCancellation.Cancel();
                AlignmentStatus = "Setup changed. Detect names again when the grid is ready.";
            }
        }

        public ReactiveCommand<Unit, Unit> SetFramesToManualCorrect => ReactiveCommand.Create(BeginManualAlignment);
        public void BeginManualAlignment()
        {
            if (_isManuallyModifiying || !Active) return;
            if (!ManualAlignmentSupported)
            {
                AlignmentStatus = "Assign by click is available on Windows. Use Detect names on this platform.";
                return;
            }
            InvalidateDetection();
            Editable = false;
            _isManuallyModifiying = true;
            this.RaisePropertyChanged(nameof(IsManuallyAligning));
            this.RaisePropertyChanged(nameof(DisplayGrid));
            this.RaisePropertyChanged(nameof(CanDetect));
            AlignmentStatus = _inCombat ? "Assignment paused during combat." :
                "Click a player in SWTOR's raid frames, then wait for their name to appear.";
            try { _raidFrameView.SetManualEditing(!_inCombat); }
            catch (Exception error)
            {
                EndManualAlignment(true);
                Logging.LogError("Could not start raid frame click capture: " + error.Message);
                AlignmentStatus = error.Message + " Try again or use Detect names.";
                return;
            }
            _manualTimeout.Start();
            var instructions = new TextBlock
            {
                Text = "Return to SWTOR and click each player's raid frame. If they are already selected, select someone else first. Combat logging must be enabled.",
                TextWrapping = TextWrapping.Wrap
            };
            var status = new TextBlock { TextWrapping = TextWrapping.Wrap, FontWeight = FontWeight.Bold };
            status.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding(nameof(AlignmentStatus)));
            var finish = new Button { Content = "Finish assigning", HorizontalAlignment = HorizontalAlignment.Right };
            var panel = new StackPanel { Margin = new Thickness(12), Spacing = 10 };
            panel.Children.Add(instructions);
            panel.Children.Add(status);
            panel.Children.Add(finish);
            var window = new Window
            {
                Title = "Assign raid players", Width = 340, SizeToContent = SizeToContent.Height,
                CanResize = false, Topmost = true, ShowInTaskbar = false, ShowActivated = false,
                WindowStartupLocation = WindowStartupLocation.Manual,
                DataContext = this, Content = panel
            };
            _manualWindow = window;
            finish.Click += (_, _) => EndManualAlignment(true);
            window.Closed += (_, _) => EndManualAlignment(true);
            window.Show();
            var parent = _overlayWindow;
            var area = parent.Screens.ScreenFromWindow(parent)?.WorkingArea;
            var x = parent.Position.X + (int)(parent.Width * parent.RenderScaling) + 8;
            var y = parent.Position.Y;
            if (area.HasValue)
            {
                var dialogWidth = (int)(window.Width * window.RenderScaling);
                var dialogHeight = (int)(window.Height * window.RenderScaling);
                // Keep the instruction window off the raid frames whenever the monitor has room.
                if (x + dialogWidth > area.Value.Right)
                {
                    x = parent.Position.X - dialogWidth - 8;
                    if (x < area.Value.X)
                    {
                        x = parent.Position.X;
                        y = parent.Position.Y - dialogHeight - 8;
                        if (y < area.Value.Y)
                            y = parent.Position.Y + (int)(parent.Height * parent.RenderScaling) + 8;
                    }
                }
                x = Math.Clamp(x, area.Value.X, Math.Max(area.Value.X, area.Value.Right - dialogWidth));
                y = Math.Clamp(y, area.Value.Y, Math.Max(area.Value.Y, area.Value.Bottom - dialogHeight));
            }
            window.Position = new PixelPoint(x, y);
        }

        public ReactiveCommand<Unit, Unit> SetFramesToNormal => ReactiveCommand.Create(() => EndManualAlignment(true));
        public void EndManualAlignment(bool restoreEditing = true)
        {
            if (!_isManuallyModifiying) return;
            _isManuallyModifiying = false;
            _raidFrameView.SetManualEditing(false);
            _manualTimeout.Stop();
            ClearPendingClick();
            var window = _manualWindow;
            _manualWindow = null;
            window?.Close();
            if (restoreEditing) Editable = true;
            this.RaisePropertyChanged(nameof(IsManuallyAligning));
            this.RaisePropertyChanged(nameof(DisplayGrid));
            this.RaisePropertyChanged(nameof(CanDetect));
            AlignmentStatus = $"{CurrentNames.Count} of {Rows * Columns} slots assigned. Lock the overlay when the grid and names are correct.";
        }

        private void SetCurrentEncounter(DateTime arg1, bool arg2)
        {
            NewEncounterEntered(CombatLogStateBuilder.CurrentState.GetEncounterActiveAtTime(arg1));
        }

        private void NewEncounterEntered(EncounterInfo obj)
        {
            _validBossIds = obj.BossIds.Any() ? obj.BossIds.SelectMany(kvp => kvp.Value.SelectMany(kvp => kvp.Value)).ToList() : new List<long>();
        }
        public void CellClicked(double xFract, double yFract)
        {
            if (!_isManuallyModifiying || _inCombat || !Active || LogReplayContext.IsActive ||
                !double.IsFinite(xFract) || !double.IsFinite(yFract) || xFract < 0 || yFract < 0 ||
                xFract >= 1 || yFract >= 1) return;
            ClearPendingClick();
            var row = (int)(yFract * Rows);
            var column = (int)(xFract * Columns);
            var now = DateTime.Now;
            now = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMillisecond));
            _manualAlignment.RecordClick(row, column, now);
            AlignmentStatus = $"Waiting for row {row + 1}, column {column + 1}. Keep this player selected until their name appears.";
        }

        private void ClearPendingClick() => _manualAlignment.Clear();

        private void ExpireManualClick()
        {
            if (_manualAlignment.Expire(DateTime.Now))
                AlignmentStatus = "No new player selection received. Select a different player first, then retry this frame. Check that combat logging is enabled.";
        }
        private void CheckForTargetChanged(ParsedLogEntry entry)
        {
            if (entry.Effect.EffectType != EffectType.TargetChanged) return;
            Dispatcher.UIThread.Post(() =>
            {
                if (_validBossIds.Contains(entry.Source.LogId))
                {
                    ResetCellPreviouslyTargeted();
                    if (entry.Effect.EffectId == _7_0LogParsing.TargetSetId)
                    {
                        var cell = GetCellThatMatchesName(entry.Target.Name);
                        if (cell != null)
                        {
                            cell.IsTargeted = true;
                            cell.TargetedBy = entry.Source.LogId;
                        }
                    }
                }
                CorrectCellWhenTargeted(entry);
            });
        }
        private void CorrectCellWhenTargeted(ParsedLogEntry entry)
        {
            if (!_isManuallyModifiying || _inCombat || !Active || LogReplayContext.IsActive ||
                !ManualRaidFrameAlignment.IsPlayerSelection(entry)) return;
            ExpireManualClick();
            if (!_manualAlignment.TryMatch(entry, out var selected)) return;
            var updated = CurrentNames.Where(n => !(n.Row == selected.Row && n.Column == selected.Column) &&
                !string.Equals(n.Name, entry.Target.Name, StringComparison.OrdinalIgnoreCase)).ToList();
            updated.Add(new PlacedName { Row = selected.Row, Column = selected.Column, Name = entry.Target.Name });
            ApplyNames(updated);
            AlignmentStatus = $"Assigned {entry.Target.Name} to row {selected.Row + 1}, column {selected.Column + 1}. {CurrentNames.Count} of {Rows * Columns} slots assigned.";
        }
        private void ToggleHide()
        {
            if(Active)
                ShowOverlayWindow();
            else
            {
                HideOverlayWindow();
            }
        }
        private void CheckForRaidHOT(TimerInstanceViewModel obj, Action<TimerInstanceViewModel> callback)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => CheckForRaidHOT(obj, callback));
                return;
            }
            if (!obj.SourceTimer.IsHot || !CurrentNames.Any() || obj.TargetAddendem == null ||
                obj.SourceTimer.IsSubTimer || obj.TimerValue <= 0)
            {
                callback(obj);
                return;
            }

            var playerName = obj.TargetAddendem.ToLower();
            var cellToUpdate = GetCellThatMatchesName(playerName);
            if (cellToUpdate == null || cellToUpdate.AlreadyHasTimer(obj.TimerName))
                return;
            cellToUpdate.AddHOT(obj);
            callback(obj);
        }
        private void CheckForDefensive(TimerInstanceViewModel obj, Action<TimerInstanceViewModel> callback)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => CheckForDefensive(obj, callback));
                return;
            }
            if (!obj.SourceTimer.IsBuiltInDefensive || !CurrentNames.Any() || obj.TargetAddendem == null ||
                obj.SourceTimer.IsSubTimer || obj.TimerValue <= 0)
            {
                callback(obj);
                return;
            }

            var playerName = obj.TargetAddendem.ToLower();
            var cellToUpdate = GetCellThatMatchesName(playerName);
            if (cellToUpdate == null || cellToUpdate.AlreadyHasTimer(obj.TimerName))
                return;
            var currentRole = CombatLogStateBuilder.CurrentState.GetCharacterClassAtTime(playerName, DateTime.Now).Role;
            if (currentRole != DataStructures.ClassInfos.Role.Tank)
            {
                callback(obj);
                return;
            }

            cellToUpdate.AddDCD(obj);
            callback(obj);
        }
        public void Reset()
        {
            foreach (var cell in RaidHotCells)
            {
                cell.Reset();
            }
        }
        private RaidHotCell? GetCellThatMatchesName(string playerName)
        {
            var match = RaidFrameNameMatcher.FindBestMatch(playerName, CurrentNames.Select(n => n.Name),
                _usingDecreasedAccuracy ? int.MaxValue : 3);
            if (match == null) return null;
            var placements = CurrentNames.Where(n => string.Equals(n.Name, match, StringComparison.OrdinalIgnoreCase)).Take(2).ToList();
            if (placements.Count != 1) return null;
            var placement = placements[0];
            return RaidHotCells.FirstOrDefault(c => c.Row == placement.Row && c.Column == placement.Column);
        }

        private void ResetCellPreviouslyTargeted()
        {
            foreach (var cell in RaidHotCells)
            {
                cell.TargetedBy = 0;
                cell.IsTargeted = false;
            }
        }

        public void UpdateNames(List<PlacedName> names)
        {
            var recentPlayers = CombatLogStateBuilder.CurrentState.PlayerClassChangeInfo
                .Where(kvp => kvp.Value.Any(up => up.Key > DateTime.Now.AddMinutes(-10)))
                .Select(kvp => kvp.Key.Name).Distinct().ToList();
            foreach (var detected in names)
            {
                var match = RaidFrameNameMatcher.FindBestMatch(detected.Name, recentPlayers,
                    Math.Min(3, Math.Max(0, RaidFrameNameMatcher.Normalize(detected.Name).Length / 2 - 1)));
                if (match != null) detected.Name = match;
            }
            ApplyNames(names);
        }

        private void ApplyNames(List<PlacedName> names)
        {
            CurrentNames = names.Where(n => n.Row >= 0 && n.Row < Rows && n.Column >= 0 && n.Column < Columns &&
                !string.IsNullOrWhiteSpace(n.Name)).GroupBy(n => (n.Row, n.Column)).Select(g => g.First()).ToList();
            UpdateCells();
        }

        public void SetTextMatchAccuracy(bool useLowAccuracy)
        {
            _usingDecreasedAccuracy = useLowAccuracy;
        }
        public int Rows
        {
            get => rows;
            set
            {
                if (!RaidFrameOverlayManager.IsValidGridDimension(value) || value == rows) return;
                ResetLayout();
                this.RaiseAndSetIfChanged(ref rows, value);
                this.RaisePropertyChanged(nameof(RowHeight));
                UpdateCells();
            }
        }
        public int Columns
        {
            get => columns;
            set
            {
                if (!RaidFrameOverlayManager.IsValidGridDimension(value) || value == columns) return;
                ResetLayout();
                this.RaiseAndSetIfChanged(ref columns, value);
                this.RaisePropertyChanged(nameof(ColumnWidth));
                UpdateCells();
            }
        }

        private void ResetLayout()
        {
            EndManualAlignment(false);
            InvalidateDetection();
            ClearPendingClick();
            CurrentNames = new List<PlacedName>();
            AlignmentStatus = "Grid changed. Detect names or assign players by clicking.";
        }

        private void UpdateCells()
        {
            lock (_cellUpdateLock)
            {
                var remaining = RaidHotCells.ToList();
                var updated = new ObservableCollection<RaidHotCell>();
                for (var row = 0; row < Rows; row++)
                for (var column = 0; column < Columns; column++)
                {
                    var name = CurrentNames.FirstOrDefault(n => n.Row == row && n.Column == column)?.Name ?? "";
                    // Reuse each player's cell exactly once so timers follow swaps and longer move cycles.
                    var cell = string.IsNullOrEmpty(name) ? null : remaining.FirstOrDefault(c =>
                        string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
                    if (cell == null) cell = new RaidHotCell(Columns);
                    else remaining.Remove(cell);
                    cell.Columns = Columns;
                    cell.Row = row;
                    cell.Column = column;
                    cell.Name = name.ToUpperInvariant();
                    updated.Add(cell);
                }
                foreach (var removed in remaining) removed.Reset();
                RaidHotCells = updated;
                this.RaisePropertyChanged(nameof(LeftColumnCells));
                this.RaisePropertyChanged(nameof(RightColumnCells));

                var timers = TimerController.GetActiveTimers().Values.ToList();
                foreach (var timer in timers.Where(t => t.SourceTimer.IsHot && t.TargetAddendem != null && t.TimerValue > 0))
                {
                    var cell = GetCellThatMatchesName(timer.TargetAddendem);
                    if (cell != null && !cell.AlreadyHasTimer(timer.TimerName)) cell.AddHOT(timer);
                }
                // ActiveTimers contains only one representative for each timer definition.
                // Recover long-lived effects on other players from the combat-log state too.
                var state = CombatLogStateBuilder.CurrentState;
                var activeHots = state.GetCurrentlyActiveRaidHOTS(DateTime.Now);
                foreach (var hot in activeHots)
                {
                    var cell = GetCellThatMatchesName(hot.Target.Name);
                    if (cell != null && !cell.HasHOT && hot.Source == state.LocalPlayer &&
                        !timers.Any(t => t.SourceTimer.IsHot && t.TargetAddendem == hot.Target.Name && t.TimerValue > 0))
                        TimerController.TryTriggerTimer(hot);
                }
            }
        }
        public List<RaidHotCell> LeftColumnCells => RaidHotCells.Where(c => c.Column == 0).ToList();
        public List<RaidHotCell> RightColumnCells => RaidHotCells.Where(c => c.Column == Columns - 1).ToList();

        public ObservableCollection<RaidHotCell> RaidHotCells
        {
            get => _raidHotCells;
            set => this.RaiseAndSetIfChanged(ref _raidHotCells, value);
        }
        
        internal void FirePlayerChanged(string name)
        {
            ResetLayout();
            UpdateCells();
            SizeSet = false;
            SetRole(name);
        }
    }
}
