using Prism.Commands;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Model.Parsely;
using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;

namespace SWTORCombatParser.ViewModels.Combat_Monitoring
{
    public class PastCombat : INotifyPropertyChanged
    {
        private bool isSelected;
        private bool isVisible = false;
        private string combatDuration;

        public event Action<PastCombat> PastCombatSelected = delegate { };
        public event Action UnselectAll = delegate { };
        public event Action<PastCombat> PastCombatUnSelected = delegate { };
        public event PropertyChangedEventHandler PropertyChanged;
        public bool IsCurrentCombat { get; set; }
        public bool IsMostRecentCombat { get; set; } = false;
        public bool IsVisible
        {
            get => isVisible; set
            {
                isVisible = value;
                OnPropertyChanged();
            }
        }
        public EncounterInfo EncounterInfo { get; set; }
        public Combat Combat { get; set; }
        public bool IsTrash => Combat != null && !Combat.IsCombatWithBoss && !IsCurrentCombat && !IsPvPCombat;
        public bool WasBossKilled => Combat?.WasBossKilled ?? false;

        public SolidColorBrush PvPBorderInidcator =>
            !IsPvPCombat ? Brushes.WhiteSmoke : WasPlayerKilled ? Brushes.IndianRed : Brushes.MediumAquamarine;
        public bool WasPlayerKilled => Combat?.WasPlayerKilled(Combat.LocalPlayer) ?? false;
        public bool IsPvPCombat => Combat?.IsPvPCombat ?? false;
        public (EncounterInfo, bool, SolidColorBrush) TextColorSetter => (EncounterInfo, WasBossKilled, PvPBorderInidcator);
        public string CombatLabel { get; set; }
        public string CombatDuration
        {
            get => combatDuration; set
            {
                combatDuration = value;
                OnPropertyChanged();
            }
        }
        public ICommand UploadToParselyCommand => new DelegateCommand(UploadToParsely);

        private async void UploadToParsely()
        {
            var lines = CombatExtractor.GetCombatLinesForCombat((int)Combat.AllLogs.Where(v=>v.LogLineNumber!=0).MinBy(v=>v.LogLineNumber).LogLineNumber, (int)Combat.AllLogs.MaxBy(v => v.LogLineNumber).LogLineNumber);
            await ParselyUploader.TryUploadText(lines, Combat.LogFileName);
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
                isSelected = value;
                if (value)
                    SelectCombat();
                else
                    UnselectCombat();
                OnPropertyChanged();
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
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
