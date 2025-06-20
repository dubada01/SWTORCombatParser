using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Model.Parsely;
using System;
using System.IO;
using System.Linq;
using System.Reactive;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using MsBox.Avalonia;
using ReactiveUI;
using SWTORCombatParser.Utilities;

namespace SWTORCombatParser.ViewModels.Combat_Monitoring
{
    public class PastCombat :ReactiveObject
    {
        private bool isSelected;
        private bool isVisible = false;
        private string combatDuration;

        public event Action<PastCombat> PastCombatSelected = delegate { };
        public event Action UnselectAll = delegate { };
        public event Action<PastCombat> PastCombatUnSelected = delegate { };
        public bool IsCurrentCombat { get; set; }
        public bool IsMostRecentCombat { get; set; } = false;
        public bool IsVisible
        {
            get => isVisible; set => this.RaiseAndSetIfChanged(ref isVisible, value);
        }
        public EncounterInfo EncounterInfo { get; set; }
        public EncounterCombat ParentEncounter { get; set; }
        public Combat Combat { get; set; }
        public bool IsTrash => Combat != null && !Combat.IsCombatWithBoss && !IsCurrentCombat && !IsPvPCombat;
        public bool WasBossKilled => Combat?.WasBossKilled ?? false;
        public bool ShouldShowDeathReview => Combat is { IsCombatWithBoss: true, WasBossKilled: false };
        public SolidColorBrush PvPBorderInidcator =>
            !IsPvPCombat ? new SolidColorBrush(Colors.WhiteSmoke) : WasPlayerKilled ? new SolidColorBrush(Colors.IndianRed) : new SolidColorBrush(Colors.MediumAquamarine);
        public bool WasPlayerKilled => Combat?.WasPlayerKilled(Combat.LocalPlayer) ?? false;
        public bool IsPvPCombat => Combat?.IsPvPCombat ?? false;
        public (EncounterInfo, bool, SolidColorBrush) TextColorSetter => (EncounterInfo, WasBossKilled, PvPBorderInidcator);
        public string CombatLabel { get; set; }
        public string CombatDuration
        {
            get => combatDuration; set => this.RaiseAndSetIfChanged(ref  combatDuration, value);
        }
        public ReactiveCommand<Unit,Unit> DisplayWipeReviewCommand => ReactiveCommand.Create(DisplayWipeReview);

        private void DisplayWipeReview()
        {
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainViewModel = desktop.MainWindow.DataContext as MainWindowViewModel;
                mainViewModel.ShowDeathReviewForCombat(Combat);
            }
        }

        public ReactiveCommand<Unit,Unit> UploadToParselyCommand => ReactiveCommand.Create(UploadToParsely);

        private async void UploadToParsely()
        {
            var lines = CombatExtractor.GetCombatLinesForCombat((int)Combat.AllLogs.Keys.Where(v=>v!=0).Min(), (int)Combat.AllLogs.Keys.Max(),Combat.LogFileName);
            var response = await ParselyUploader.TryUploadText(lines, Path.Combine(Settings.ReadSettingOfType<string>("combat_logs_path"), Combat.LogFileName));
            if (!response.WasSuccess)
            {
                if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    var box = MessageBoxManager.GetMessageBoxStandard("Error", response.ErrorMessage,windowStartupLocation: WindowStartupLocation.CenterOwner);
                    await box.ShowWindowDialogAsync(desktop.MainWindow);

                }
            }
            if (response.WasSuccess)
            {
                if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    var box = MessageBoxManager.GetMessageBoxStandard("Success",
                        "Successfully uploaded to Parsely! Use the File->Parsely menu in Orbs to open and view your battle.",
                        windowStartupLocation: WindowStartupLocation.CenterOwner);
                    await box.ShowWindowDialogAsync(desktop.MainWindow);
                }
            }
        }

        public DateTime CombatStartTime { get; set; }
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
            get => isSelected; set
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
    }
}
