using SWTORCombatParser.DataStructures.RaidInfos;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;

namespace SWTORCombatParser.Model.CombatParsing
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
