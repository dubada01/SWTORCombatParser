using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Utilities;
using System;
using System.ComponentModel;
using System.Reactive;
using Avalonia.Threading;
using ReactiveUI;

namespace SWTORCombatParser.ViewModels.Overlays.RaidHots
{
    public class RaidHotsConfigViewModel : ReactiveObject
    {
        private string raidFrameRows = "4";
        private string raidFrameColumns = "2";
        private readonly RaidFrameOverlayViewModel _currentOverlayViewModel;
        private bool _isRaidFrameEditable;
        private bool raidHotsEnabled;
        private string toggleEditText = "Move grid";
        private string _currentCharacter = "no character";
        private bool _decreasedSpecificity;
        private bool _loadingLayout;

        public event Action<bool> EnabledChanged = delegate { };
        public RaidFrameOverlayViewModel Overlay => _currentOverlayViewModel;
        public bool CanDetect => RaidHotsEnabled && Overlay.CanDetect;
        public string AlignmentStatus => RaidHotsEnabled ? Overlay.AlignmentStatus : "Enable raid HOTs to align frames.";

        public RaidHotsConfigViewModel()
        {
            RaidFrameOverlayManager.Init();
            _currentOverlayViewModel = new RaidFrameOverlayViewModel("RaidFrame")
            {
                Columns = 2,
                Rows = 4,
                Editable = _isRaidFrameEditable
            };
            _currentOverlayViewModel.CloseRequested += () => RaidHotsEnabled = false;
            _currentOverlayViewModel.PropertyChanged += OverlayPropertyChanged;
            _currentOverlayViewModel.ActiveChanged += SynchronizeEnabledState;
            HotkeyHandler.OnRefreshHOTsHotkey += AutoDetection;
            CombatLogStreamer.HistoricalLogsFinished += (t, isLive) =>
            {
                if (!isLive || CombatLogStateBuilder.CurrentState?.LocalPlayer == null)
                    return;
                SetClass(CombatLogStateBuilder.CurrentState.LocalPlayer,
                    CombatLogStateBuilder.CurrentState.GetLocalPlayerClassAtTime(t));
            };
            CombatLogStateBuilder.PlayerDiciplineChanged += SetClass;

            LoadLayout();
            EnsureCharacterOverlayDefaults(_currentCharacter);
            _currentOverlayViewModel.FirePlayerChanged(_currentCharacter);
            SynchronizeEnabledState(_currentOverlayViewModel.Active);
        }

        private void OverlayPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Overlay.CanDetect))
                this.RaisePropertyChanged(nameof(CanDetect));
            if (e.PropertyName == nameof(Overlay.AlignmentStatus))
                this.RaisePropertyChanged(nameof(AlignmentStatus));
            if (e.PropertyName == nameof(Overlay.Editable))
            {
                _isRaidFrameEditable = Overlay.Editable;
                ToggleEditText = Overlay.Editable ? "Lock grid" : "Move grid";
                this.RaisePropertyChanged(nameof(RaidFrameEditable));
            }
            if (e.PropertyName != nameof(Overlay.Rows) && e.PropertyName != nameof(Overlay.Columns))
                return;

            this.RaiseAndSetIfChanged(ref raidFrameRows, Overlay.Rows.ToString(), nameof(RaidFrameRows));
            this.RaiseAndSetIfChanged(ref raidFrameColumns, Overlay.Columns.ToString(), nameof(RaidFrameColumns));
            if (!_loadingLayout)
                RaidFrameOverlayManager.SetRowsColumns(Overlay.Rows, Overlay.Columns, _currentCharacter);
        }

        private void LoadLayout()
        {
            var defaults = RaidFrameOverlayManager.GetDefaults(_currentCharacter);
            _loadingLayout = true;
            try
            {
                Overlay.Rows = (int)defaults.Rows;
                Overlay.Columns = (int)defaults.Columns;
            }
            finally
            {
                _loadingLayout = false;
            }
            this.RaiseAndSetIfChanged(ref raidFrameRows, Overlay.Rows.ToString(), nameof(RaidFrameRows));
            this.RaiseAndSetIfChanged(ref raidFrameColumns, Overlay.Columns.ToString(), nameof(RaidFrameColumns));
        }

        private static void EnsureCharacterOverlayDefaults(string character)
        {
            var characterDefaults = DefaultCharacterOverlays.GetCharacterDefaults(character);
            if (characterDefaults.ContainsKey("RaidFrame"))
                return;

            var layout = RaidFrameOverlayManager.GetDefaults(character);
            var active = layout.Acive;
            if (character != "no character" &&
                DefaultCharacterOverlays.GetCharacterDefaults("no character").TryGetValue("RaidFrame", out var template))
                active = template.Acive;
            // BaseOverlayViewModel restores geometry and enabled state from the character
            // overlay store. New roles need an entry there as well as their raid grid settings.
            DefaultCharacterOverlays.SetCharacterDefaults("RaidFrame", layout.Position, layout.WidtHHeight, character);
            DefaultCharacterOverlays.SetActiveStateCharacter("RaidFrame", active, character);
        }

        public bool DecreasedSpecificity
        {
            get => _decreasedSpecificity;
            set
            {
                this.RaiseAndSetIfChanged(ref _decreasedSpecificity, value);
                Overlay.SetTextMatchAccuracy(value);
            }
        }

        public ReactiveCommand<Unit, Unit> ManuallyRefreshPlayersCommand => ReactiveCommand.Create(AutoDetection);
        public ReactiveCommand<Unit, Unit> TogglePositionCommand => ReactiveCommand.Create(() => { Overlay.Editable = !Overlay.Editable; });
        public bool RaidFrameEditable => _isRaidFrameEditable;
        public string ToggleEditText
        {
            get => toggleEditText;
            set => this.RaiseAndSetIfChanged(ref toggleEditText, value);
        }

        public string RaidFrameRows
        {
            get => raidFrameRows;
            set
            {
                if (value == "")
                {
                    this.RaiseAndSetIfChanged(ref raidFrameRows, value);
                    return;
                }
                if (!int.TryParse(value, out var count) || !RaidFrameOverlayManager.IsValidGridDimension(count))
                    return;
                this.RaiseAndSetIfChanged(ref raidFrameRows, value);
                Overlay.Rows = count;
            }
        }

        public string RaidFrameColumns
        {
            get => raidFrameColumns;
            set
            {
                if (value == "")
                {
                    this.RaiseAndSetIfChanged(ref raidFrameColumns, value);
                    return;
                }
                if (!int.TryParse(value, out var count) || !RaidFrameOverlayManager.IsValidGridDimension(count))
                    return;
                this.RaiseAndSetIfChanged(ref raidFrameColumns, value);
                Overlay.Columns = count;
            }
        }

        public void HideRaidHots() => Overlay.HideOverlayWindow();

        public bool RaidHotsEnabled
        {
            get => raidHotsEnabled;
            set
            {
                if (!Dispatcher.UIThread.CheckAccess())
                {
                    Dispatcher.UIThread.Invoke(() => RaidHotsEnabled = value);
                    return;
                }
                if (Overlay.Active != value)
                    Overlay.Active = value;
                SynchronizeEnabledState(value);
            }
        }

        private void SynchronizeEnabledState(bool enabled)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => SynchronizeEnabledState(enabled));
                return;
            }
            if (raidHotsEnabled == enabled)
                return;
            this.RaiseAndSetIfChanged(ref raidHotsEnabled, enabled, nameof(RaidHotsEnabled));
            this.RaisePropertyChanged(nameof(CanDetect));
            this.RaisePropertyChanged(nameof(AlignmentStatus));
            EnabledChanged(enabled);
        }

        private void AutoDetection()
        {
            // Both the settings button and hotkey use the overlay's single in-flight request.
            Dispatcher.UIThread.Post(async () =>
            {
                if (CanDetect)
                    await Overlay.DetectPlayersAsync();
            });
        }

        internal void ToggleLock(bool overlaysLocked)
        {
            Dispatcher.UIThread.Invoke(() => Overlay.Editable = !overlaysLocked);
        }

        private void SetClass(Entity player, SWTORClass discipline)
        {
            if (player == null || discipline == null)
                return;
            Dispatcher.UIThread.Invoke(() =>
            {
                var character = player.Name + "/" + discipline.Discipline;
                if (_currentCharacter == character)
                    return;
                _currentCharacter = character;
                LoadLayout();
                EnsureCharacterOverlayDefaults(_currentCharacter);
                // Select the new character before changing Active so the old healer's
                // saved enabled state is not overwritten when switching to a damage role.
                Overlay.FirePlayerChanged(_currentCharacter);
                if (discipline.Role != Role.Healer)
                    RaidHotsEnabled = false;
                else
                    SynchronizeEnabledState(Overlay.Active);
            });
        }
    }
}
