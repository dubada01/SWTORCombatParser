using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.CloudRaiding;
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
    public class RaidParticipantInfo : INotifyPropertyChanged
    {
        private string playerName;
        private Role playerRole;

        public RaidParticipantInfo(List<ParsedLogEntry> logs, string logName)
        {
            LogName = logName;
            CurrentCombatInfo = new Combat();
            UpdateMetaDatas();
            Update(logs);

        }
        public void Update(List<ParsedLogEntry> logs)
        {
            if (!logs.Any())
                return;
            var newLogs = GetValidLogs(logs);
            CurrentLogs.AddRange(GetValidLogs(logs));
            var playerName = logs.FirstOrDefault(l => l.Source.IsPlayer && l.Target.IsPlayer);
            if (string.IsNullOrEmpty(PlayerName) && playerName != null)
                PlayerName = playerName.Source.Name;


            ParticipantCurrentState = CombatLogStateBuilder.GetStateOfRaidingLogs(newLogs, LogName);
            if (ParticipantCurrentState.PlayerClass != null && ParticipantCurrentState.PlayerClass.Role != Role.Unknown)
            {
                PlayerRole = ParticipantCurrentState.PlayerClass.Role;
                OnPropertyChanged("PlayerRole");
            }
            var enterCombatLogs = logs.Where(l => l.Effect.EffectName == "EnterCombat" || l.Ability == "StartCombatMarker").ToList();
            for (var c = 0; c < enterCombatLogs.Count(); c++)
            {
                var enterCombatIndex = enterCombatLogs.Count() == 0 ? 0 : logs.IndexOf(enterCombatLogs[c]);
                var endCombatIndex = LogSearcher.GetIndexOfNextCombatEndLog(enterCombatIndex, logs);

                CurrentCombatInfo = CombatIdentifier.ParseOngoingCombat(logs.GetRange(enterCombatIndex, (endCombatIndex - enterCombatIndex)));
                if (enterCombatLogs.Count > c - 1)
                {
                    PastCombats.Add(CombatIdentifier.ParseOngoingCombat(logs.GetRange(enterCombatIndex, (endCombatIndex - enterCombatIndex))));
                }
            }

            OnPropertyChanged("CurrentCombatInfo");
            UpdateMetaDatas();

        }
        private void UpdateMetaDatas()
        {
            var metaDatas = MetaDataFactory.GetMetaDatas(CurrentCombatInfo);
            App.Current.Dispatcher.Invoke(() =>
            {
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
        public string PlayerName
        {
            get => playerName; 
            set
            {
                playerName = value;
                OnPropertyChanged();
            }
        }
        public Role PlayerRole
        { 
            get => playerRole; 
            set 
            {
                playerRole = value;
                OnPropertyChanged();
            } 
        }
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
            //if (CurrentLogs.Count > 0)
            //    PastCombats.Add(CombatIdentifier.ParseOngoingCombat(CurrentLogs));
        }
        internal void ResetCombat()
        {
            CurrentLogs.Clear();
            CurrentCombatInfo = null;
            MetaDatas.Clear();
        }
    }
}
