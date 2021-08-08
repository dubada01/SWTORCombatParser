using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace SWTORCombatParser.ViewModels
{
    public class TableViewModel
    {
        public ObservableCollection<ParsedLogEntry> CombatLogs { get; set; } = new ObservableCollection<ParsedLogEntry>();
        public void AddCombatLogs(List<ParsedLogEntry> logsInCombat)
        {
            logsInCombat.ForEach(l => CombatLogs.Add(l));
        }
        public void RemoveCombatLogs(List<ParsedLogEntry> logsInCombat)
        {
            logsInCombat.ForEach(l => CombatLogs.Remove(l));
        }
    }
}
