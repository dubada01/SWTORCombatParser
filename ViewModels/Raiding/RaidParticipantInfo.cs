using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.ViewModels.CombatMetaData;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace SWTORCombatParser.ViewModels.Raiding
{
    public class RaidParticipantInfo:INotifyPropertyChanged
    {
        public RaidParticipantInfo(List<ParsedLogEntry> logs, string logName)
        {
            LogName = logName;
            Update(logs);

        }
        public void Update(List<ParsedLogEntry> logs)
        {
            CurrentLogs.AddRange(GetValidLogs(logs));
            var playerName = logs.FirstOrDefault(l => l.Source.IsPlayer && l.Target.IsPlayer);
            if (string.IsNullOrEmpty(PlayerName) && playerName != null)
                PlayerName = playerName.Source.Name;


            ParticipantCurrentState = CombatLogStateBuilder.GetStateOfRaidingLogs(CurrentLogs);
            if (ParticipantCurrentState.PlayerClass != null && ParticipantCurrentState.PlayerClass.Role !=  Role.Unknown)
            {
                PlayerRole = ParticipantCurrentState.PlayerClass.Role;
                OnPropertyChanged("PlayerRole");
            }
            CurrentCombatInfo = CombatIdentifier.ParseOngoingCombat(CurrentLogs);
            OnPropertyChanged("CurrentCombatInfo");
            var metaDatas = MetaDataFactory.GetMetaDatas(CurrentCombatInfo);
            App.Current.Dispatcher.Invoke(() => {
                MetaDatas.Clear();
                foreach (var metaData in metaDatas)
                {
                    MetaDatas.Add(metaData);
                }
                OnPropertyChanged("PlayerName");
                OnPropertyChanged("ParticipantCurrentState");
            });

        }
        public string LogName { get; set; }
        public string PlayerName { get; set; }
        public Role PlayerRole { get; set; } 
        public LogState ParticipantCurrentState { get; set; } 
        public List<ParsedLogEntry> CurrentLogs { get; set; } = new List<ParsedLogEntry>();
        public Combat CurrentCombatInfo { get; set; }
        public List<Combat> PastCombats { get; set; } = new List<Combat>();
        public ObservableCollection<MetaDataInstance> MetaDatas { get; set; } = new ObservableCollection<MetaDataInstance>();
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        private List<ParsedLogEntry> GetValidLogs(List<ParsedLogEntry> incomingLogs)
        {
            return incomingLogs.Where(l => !CurrentLogs.Any(cl => cl.RaidLogId == l.RaidLogId)).ToList();
        }
        internal void FinishCombat()
        {
            if (CurrentLogs.Count > 0)
                PastCombats.Add(CombatIdentifier.ParseOngoingCombat(CurrentLogs));
        }
        internal void ResetCombat()
        {
            CurrentLogs.Clear();
            CurrentCombatInfo = null;
            MetaDatas.Clear();
        }
    }
}
