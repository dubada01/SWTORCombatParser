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

        public void CacheCombat(Combat combat, bool keepLoaded = false)
        {
            SetCombat(combat);
            if (combat == null || IsCurrentCombat)
                return;

            CombatCachePath = CombatDiskCacheManager.CacheCombat(combat);
            if (!keepLoaded)
                Combat = null;
        }

        public void SetCombat(Combat combat)
        {
            Combat = combat;
            UpdateSummary(combat);
        }

        public Combat GetCombat()
        {
            if (Combat != null)
                return Combat;
            if (string.IsNullOrEmpty(CombatCachePath))
                return new Combat();

            Combat = CombatDiskCacheManager.LoadCombat(CombatCachePath);
            UpdateSummary(Combat);
            return Combat;
        }

        public List<ParsedLogEntry> LoadLogs()
        {
            if (Combat != null)
                return Combat.AllLogs.OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value).ToList();
            if (string.IsNullOrEmpty(CombatCachePath))
                return new List<ParsedLogEntry>();
            return CombatDiskCacheManager.LoadLogs(CombatCachePath);
        }

        public void UnloadCombat()
        {
            if (IsCurrentCombat || string.IsNullOrEmpty(CombatCachePath))
                return;
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

        private void DisplayWipeReview()
        {
            var combat = GetCombat();
            if (combat.StartTime == DateTime.MinValue)
                return;

            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainViewModel = desktop.MainWindow.DataContext as MainWindowViewModel;
                mainViewModel?.ShowDeathReviewForCombat(combat);
            }
        }

        private async void UploadToParsely()
        {
            var combat = GetCombat();
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
            CombatDurationSeconds = combat.DurationSeconds;
            CombatDuration = TimeSpan.FromSeconds(combat.DurationSeconds).ToString(@"mm\:ss");
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
