﻿using SWTORCombatParser.DataStructures.RaidInfos;
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
        public event Action<PastCombat> PastCombatSelected = delegate { };
        public event Action UnselectAll = delegate { };
        public event Action<PastCombat> PastCombatUnSelected = delegate { };
        public event PropertyChangedEventHandler PropertyChanged;
        public bool IsCurrentCombat;
        public bool IsMostRecentCombat = false;
        public EncounterInfo EncounterInfo { get; set; }
        public Combat Combat;
        private bool isSelected;

        public string CombatLabel { get; set; }
        public string CombatDuration { get; set; }
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
        public bool IsSelected { get => isSelected; set {
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
