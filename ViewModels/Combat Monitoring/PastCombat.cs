using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.CombatCaching;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Model.Parsely;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using MsBox.Avalonia;
using ReactiveUI;

namespace SWTORCombatParser.ViewModels.Combat_Monitoring
{
    public class PastCombat : ReactiveObject
    {
        private bool isSelected;
        private bool isVisible;
        private string combatDuration;
        private bool _isTrash;
        private bool _isBossCombat;
        private bool _wasBossKilled;
        private bool _shouldShowDeathReview;
        private bool _wasPlayerKilled;
        private bool _isPvPCombat;
        private string _combatLoadError = "";
        private readonly object _combatLoadLock = new();
        private readonly Func<string, Combat> _loadCombat;

        public PastCombat() : this(CombatDiskCacheManager.LoadCombat)
        {
        }

        internal PastCombat(Func<string, Combat> loadCombat)
        {
            _loadCombat = loadCombat;
        }

        public event Action<PastCombat> PastCombatSelected = delegate { };
        public event Action UnselectAll = delegate { };
        public event Action<PastCombat> PastCombatUnSelected = delegate { };

        public bool IsCurrentCombat { get; set; }
        public bool IsMostRecentCombat { get; set; }

        public bool IsVisible
        {
            get => isVisible;
            set => this.RaiseAndSetIfChanged(ref isVisible, value);
        }

        public EncounterInfo EncounterInfo { get; set; }
        public EncounterCombat ParentEncounter { get; set; }
        public Combat Combat { get; private set; }
        public string CombatCachePath { get; private set; }
        public string CombatLoadError
        {
            get => _combatLoadError;
            private set => this.RaiseAndSetIfChanged(ref _combatLoadError, value);
        }
        public int CombatDurationSeconds { get; set; }
        public bool IsTrash => _isTrash;
        public bool IsBossCombat => _isBossCombat;
        public bool WasBossKilled => _wasBossKilled;
        public bool ShouldShowDeathReview => _shouldShowDeathReview;
        public bool WasPlayerKilled => _wasPlayerKilled;
        public bool IsPvPCombat => _isPvPCombat;
        public SolidColorBrush PvPBorderInidcator =>
            !IsPvPCombat
                ? new SolidColorBrush(Colors.WhiteSmoke)
                : WasPlayerKilled
                    ? new SolidColorBrush(Colors.IndianRed)
                    : new SolidColorBrush(Colors.MediumAquamarine);

        public (bool, bool, bool, SolidColorBrush) TextColorSetter => (IsBossCombat, WasBossKilled, IsPvPCombat, PvPBorderInidcator);
        public string CombatLabel { get; set; }

        public string CombatDuration
        {
            get => combatDuration;
            set => this.RaiseAndSetIfChanged(ref combatDuration, value);
        }

        public ReactiveCommand<Unit, Unit> DisplayWipeReviewCommand => ReactiveCommand.Create(DisplayWipeReview);
        public ReactiveCommand<Unit, Unit> UploadToParselyCommand => ReactiveCommand.Create(UploadToParsely);

        public DateTime CombatStartTime { get; set; }
        internal DateTime CombatEndTime { get; private set; }
        internal Guid CacheOwner { get; private set; }
        internal string? SourceLogName { get; private set; }
        internal bool IsReplay { get; private set; }
        internal (string Boss, string GroupSize, string Difficulty) BossDetails { get; private set; }

        public void CacheCombat(Combat combat, bool keepLoaded = false)
        {
            SetCombat(combat);
            if (combat == null || IsCurrentCombat || LogLoadDiagnostics.KeepCombatsInMemory)
                return;

            try
            {
                CombatCachePath = CombatDiskCacheManager.CacheCombat(combat);
                if (!keepLoaded)
                    Combat = null;
            }
            catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or NotSupportedException)
            {
                // Keep the combat usable in memory if the optional cache is unavailable.
                CombatCachePath = null;
                try
                {
                    Logging.LogError("Could not cache combat: " + e.Message, tryCloudLogs: false);
                }
                catch (Exception logError) when (logError is IOException or UnauthorizedAccessException)
                {
                    System.Diagnostics.Debug.WriteLine("Could not record cache error: " + logError.Message);
                }
            }
        }

        public void SetCombat(Combat combat)
        {
            Combat = combat;
            if (combat != null) CombatLoadError = "";
            UpdateSummary(combat);
        }

        public Combat GetCombat()
        {
            var resident = Combat;
            if (resident != null)
                return resident;
            // Rapid A -> B -> A selections can ask for the same unloaded row twice.
            // Share one rebuild so a stale worker cannot overwrite the displayed instance.
            // UnloadCombat never takes this lock, keeping UI deselection independent of I/O.
            lock (_combatLoadLock)
            {
                resident = Combat;
                if (resident != null)
                    return resident;
                try
                {
                    if (string.IsNullOrEmpty(CombatCachePath))
                        throw new IOException("No combat cache is available.");
                    var loaded = _loadCombat(CombatCachePath);
                    if (loaded.StartTime == DateTime.MinValue || loaded.AllLogs.Count == 0)
                        throw new InvalidDataException("The combat cache contains no usable events.");
                    SetCombat(loaded);
                    return loaded;
                }
                catch (Exception error) when (IsCacheReadFailure(error))
                {
                    ReportCacheReadFailure(error);
                    // Keep the row's original summary and leave Combat null so a repaired
                    // cache can be retried. An invalid sentinel never becomes cached state.
                    return new Combat();
                }
            }
        }

        public List<ParsedLogEntry> LoadLogs()
        {
            var resident = Combat;
            if (resident != null)
                return resident.AllLogs.OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value)
                    .Where(log => log.Effect.EffectType != EffectType.AbsorbShield).ToList();
            try
            {
                if (string.IsNullOrEmpty(CombatCachePath))
                    throw new IOException("No combat cache is available.");
                var logs = CombatDiskCacheManager.LoadLogs(CombatCachePath);
                if (logs.Count == 0) throw new InvalidDataException("The combat cache contains no usable events.");
                CombatLoadError = "";
                return logs;
            }
            catch (Exception error) when (IsCacheReadFailure(error))
            {
                ReportCacheReadFailure(error);
                // Overall/session scopes must not quietly omit an unavailable fight.
                throw new IOException(CombatLoadError, error);
            }
        }

        private static bool IsCacheReadFailure(Exception error) => error is
            IOException or InvalidDataException or UnauthorizedAccessException or JsonException or NotSupportedException;

        private void ReportCacheReadFailure(Exception error)
        {
            CombatLoadError = IsCurrentCombat
                ? "This combat does not have any parsed events yet."
                : "This combat's cached data is unavailable. Reload the source log to restore it.";
            System.Diagnostics.Debug.WriteLine("Could not load cached combat: " + error.Message);
        }

        public void UnloadCombat()
        {
            if (IsCurrentCombat || string.IsNullOrEmpty(CombatCachePath))
                return;
            if (Combat is { } resident) CombatDiskCacheManager.RememberCombat(CombatCachePath, resident);
            Combat = null;
        }

        public void SelectionToggle()
        {
            UnselectAll();
            IsSelected = !IsSelected;
        }

        public void AdditiveSelectionToggle()
        {
            IsSelected = !IsSelected;
        }

        public bool IsSelected
        {
            get => isSelected;
            set
            {
                this.RaiseAndSetIfChanged(ref isSelected, value);
                if (value)
                    SelectCombat();
                else
                    UnselectCombat();
            }
        }

        public void UnselectCombat()
        {
            PastCombatUnSelected(this);
        }

        public void SelectCombat()
        {
            PastCombatSelected(this);
        }

        public void DisplayWipeReview()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow?.DataContext is MainWindowViewModel mainViewModel)
                mainViewModel.ShowProgressionReview(this);
        }

        private async void UploadToParsely()
        {
            var combat = GetCombat();
            if (combat.IsReplay) return;
            if (combat.StartTime == DateTime.MinValue || !combat.AllLogs.Any())
                return;

            var lines = CombatExtractor.GetCombatLinesForCombat(
                (int)combat.AllLogs.Keys.Where(v => v != 0).Min(),
                (int)combat.AllLogs.Keys.Max(),
                combat.LogFileName);

            var response = await ParselyUploader.TryUploadText(
                lines,
                Path.Combine(Settings.ReadSettingOfType<string>("combat_logs_path"), combat.LogFileName));

            if (!response.WasSuccess)
            {
                if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    var box = MessageBoxManager.GetMessageBoxStandard(
                        "Error",
                        response.ErrorMessage,
                        windowStartupLocation: WindowStartupLocation.CenterOwner);
                    await box.ShowWindowDialogAsync(desktop.MainWindow);
                }

                return;
            }

            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopSuccess)
            {
                var successBox = MessageBoxManager.GetMessageBoxStandard(
                    "Success",
                    "Successfully uploaded to Parsely! Use the File->Parsely menu in Orbs to open and view your battle.",
                    windowStartupLocation: WindowStartupLocation.CenterOwner);
                await successBox.ShowWindowDialogAsync(desktopSuccess.MainWindow);
            }
        }

        private void UpdateSummary(Combat combat)
        {
            if (combat == null)
                return;

            EncounterInfo = combat.ParentEncounter;
            CombatStartTime = combat.StartTime;
            CombatEndTime = combat.EndTime;
            CacheOwner = combat.CacheLogStateId;
            IsReplay = combat.IsReplay;
            BossDetails = combat.EncounterBossDifficultyParts;
            if (!IsCurrentCombat && string.IsNullOrEmpty(SourceLogName))
                SourceLogName = combat.AllLogs.FirstOrDefault(entry => !string.IsNullOrEmpty(entry.Value.LogName)).Value?.LogName;
            // A row describes the whole fight, even while another combat's phase
            // filter is active during an asynchronous cache reload.
            CombatDurationSeconds = (int)Math.Round(Math.Max(0, (combat.EndTime - combat.StartTime).TotalSeconds));
            CombatDuration = TimeSpan.FromSeconds(CombatDurationSeconds).ToString(@"mm\:ss");
            _isBossCombat = combat.IsCombatWithBoss;
            _isPvPCombat = combat.IsPvPCombat;
            _wasBossKilled = combat.WasBossKilled;
            _shouldShowDeathReview = combat.IsCombatWithBoss && !combat.WasBossKilled;
            _wasPlayerKilled = combat.LocalPlayer != null && combat.WasPlayerKilled(combat.LocalPlayer);
            _isTrash = !combat.IsCombatWithBoss && !IsCurrentCombat && !_isPvPCombat;

            this.RaisePropertyChanged(nameof(IsTrash));
            this.RaisePropertyChanged(nameof(IsBossCombat));
            this.RaisePropertyChanged(nameof(WasBossKilled));
            this.RaisePropertyChanged(nameof(ShouldShowDeathReview));
            this.RaisePropertyChanged(nameof(WasPlayerKilled));
            this.RaisePropertyChanged(nameof(IsPvPCombat));
            this.RaisePropertyChanged(nameof(TextColorSetter));
        }
    }
}
