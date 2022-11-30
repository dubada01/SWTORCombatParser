using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.EncounterInfo;

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
        public bool IsTrash => Combat!=null&&!Combat.IsCombatWithBoss && !IsCurrentCombat;
        public bool WasBossKilled => Combat?.WasBossKilled ?? false;

        public SolidColorBrush PvPBorderInidcator =>
            !IsPvPCombat ? Brushes.WhiteSmoke : WasPlayerKilled ? Brushes.IndianRed : Brushes.MediumAquamarine;
        public bool WasPlayerKilled => Combat?.WasPlayerKilled(Combat.LocalPlayer) ?? false;
        public bool IsPvPCombat => Combat?.IsPvPCombat ?? false;
        public (EncounterInfo, bool,SolidColorBrush) TextColorSetter => (EncounterInfo, WasBossKilled,PvPBorderInidcator);
        public string CombatLabel { get; set; }
        public string CombatDuration
        {
            get => combatDuration; set
            {
                combatDuration = value;
                OnPropertyChanged();
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
