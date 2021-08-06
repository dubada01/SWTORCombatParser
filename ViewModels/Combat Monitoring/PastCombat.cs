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
        public event PropertyChangedEventHandler PropertyChanged;

        public Combat Combat;
        public string CombatLabel { get; set; }
        public string CombatDuration { get; set; }
        public ICommand SelectCombatCommand => new CommandHandler(SelectCombat, () => true);
        public string SelectedCheck { get; set; } = " ";
        public void Reset()
        {
            SelectedCheck = " ";
            OnPropertyChanged("SelectedCheck");
        }
        public void SelectCombat()
        {
            PastCombatSelected(this);
            SelectedCheck = "✓";
            OnPropertyChanged("SelectedCheck");
            Trace.WriteLine("Selected " + CombatLabel);
        }
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
