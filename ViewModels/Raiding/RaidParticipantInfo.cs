using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.ViewModels.CombatMetaData;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace SWTORCombatParser.ViewModels.Raiding
{
    public class RaidParticipantInfo : INotifyPropertyChanged, IDisposable
    {
        private string playerName;
        private Role playerRole;
        private List<ParsedLogEntry> _newlyAddedValidLogs;
        public RaidParticipantInfo(List<ParsedLogEntry> logs, string logName)
        {
            CombatSelectionMonitor.NewCombatSelected += DisplayCombat;
            LogName = logName;
            CurrentCombatInfo = new Combat();
            Update(logs); 
            UpdateMetaDatas();

        }
        public List<CombatParticipant> Update(List<ParsedLogEntry> logs)
        {
            if (!logs.Any())
                return new List<CombatParticipant>();
            _newlyAddedValidLogs = GetValidLogs(logs);
            CurrentLogs.AddRange(_newlyAddedValidLogs);
            var playerName = _newlyAddedValidLogs.FirstOrDefault(l => l.Source.IsPlayer && l.Target.IsPlayer);
            if (string.IsNullOrEmpty(PlayerName) && playerName != null)
            { 
                PlayerName = playerName.Source.Name;
                CurrentCombatInfo.CharacterName = PlayerName;
            }
            UpdateState();
            foreach(var log in _newlyAddedValidLogs)
            {
                CombatLogParser.UpdateEffectiveHealValues(log, ParticipantCurrentState);
            }
            return UpdateCombats();
        }
        public void UpdateState()
        {
            ParticipantCurrentState = CombatLogStateBuilder.GetStateOfRaidingLogs(_newlyAddedValidLogs, LogName);
            if (ParticipantCurrentState.PlayerClass != null && ParticipantCurrentState.PlayerClass.Role != Role.Unknown)
            {
                PlayerRole = ParticipantCurrentState.PlayerClass.Role;
                OnPropertyChanged("PlayerRole");
            }
        }
        public List<CombatParticipant> UpdateCombats()
        {
            List<CombatParticipant> _combatsGenerated = new List<CombatParticipant>();
            var enterCombatLogs = _newlyAddedValidLogs.Where(l => l.Effect.EffectName == "EnterCombat" || l.Ability == "StartCombatMarker").ToList();
            var distinctEnterLogs = enterCombatLogs.GroupBy(t => t.TimeStamp).Select(g => g.First()).ToList();
            var hasCombatEnd = _newlyAddedValidLogs.Any(l => l.Ability == "SWTOR_PARSING_COMBAT_END");
            if(distinctEnterLogs.Any() && hasCombatEnd)
            {
                for (var c = 0; c < distinctEnterLogs.Count(); c++)
                {  
                    var enterCombatIndex = distinctEnterLogs.Count() == 0 ? 0 : _newlyAddedValidLogs.IndexOf(distinctEnterLogs[c]);
                    var endCombatIndex = LogSearcher.GetIndexOfNextCombatEndLog(enterCombatIndex, _newlyAddedValidLogs);

                    CombatIdentifier.UpdateOngoingCombat(_newlyAddedValidLogs.GetRange(enterCombatIndex, (endCombatIndex - enterCombatIndex)), CurrentCombatInfo);
                    if (distinctEnterLogs.Count > c - 1 && hasCombatEnd)
                    {
                        var newCombat = CombatIdentifier.GenerateNewCombatFromLogs(CurrentCombatInfo.Logs);
                        newCombat.TotalProvidedSheilding = CurrentCombatInfo.TotalProvidedSheilding;
                        PastCombats.Add(newCombat);
                        _combatsGenerated.Add(new CombatParticipant { Combat = newCombat, Participant = this });
                        CurrentCombatInfo = new Combat();
                    }
                }
            }
            else
            {
                CombatIdentifier.UpdateOngoingCombat(_newlyAddedValidLogs,CurrentCombatInfo);
                _combatsGenerated.Add(new CombatParticipant { Combat = CurrentCombatInfo, Participant = this });
            }

            OnPropertyChanged("CurrentCombatInfo");
            UpdateMetaDatas();
            return _combatsGenerated;
        }
        private void DisplayCombat(Combat clickedCombat)
        {
            var nearestCombat = PastCombats.FirstOrDefault(c => Math.Abs((c.StartTime - clickedCombat.StartTime).TotalSeconds) < 3);
            if(nearestCombat == null)
            {
                CurrentCombatInfo = new Combat();
            }
            else
            {
                CurrentCombatInfo = nearestCombat;
            }
            StaticRaidInfo.FireNewRaidCombatDisplayed(CurrentCombatInfo);
            UpdateMetaDatas();
        }
        private void UpdateMetaDatas()
        {
            List<MetaDataInstance> metaDatas;
            if (CurrentCombatInfo.Logs.Count == 0)
                metaDatas = MetaDataFactory.GetPlaceholders();
            else
                metaDatas = MetaDataFactory.GetMetaDatas(CurrentCombatInfo);
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
            CurrentLogs.RemoveAll(l => l.Ability == "StartCombatMarker");
            if (string.IsNullOrEmpty(CurrentCombatInfo.CharacterName))
            {
                if(PastCombats.Count == 0)
                {
                    Trace.WriteLine("Combat ended but not combats detected for " + playerName);
                    if(CurrentCombatInfo.Logs.Count > 0)
                    {
                        Trace.WriteLine("Valid logs detected though, adding combat for " + playerName);
                        var newCombat = CombatIdentifier.GenerateNewCombatFromLogs(CurrentCombatInfo.Logs);
                        newCombat.TotalProvidedSheilding = CurrentCombatInfo.TotalProvidedSheilding;
                        PastCombats.Add(newCombat);
                    }
                    return;
                }
                CurrentCombatInfo = PastCombats.OrderBy(t => t.EndTime).Last();
            }
            else
            {
                var newCombat = CombatIdentifier.GenerateNewCombatFromLogs(CurrentCombatInfo.Logs);
                newCombat.TotalProvidedSheilding = CurrentCombatInfo.TotalProvidedSheilding;
                PastCombats.Add(newCombat);
            }
        }
        internal void ResetCombat()
        {
            CurrentLogs.Clear();
            CurrentCombatInfo = new Combat() { CharacterName = playerName};
            foreach(var metaData in MetaDatas)
            {
                metaData.Reset();
            }
        }

        public void Dispose()
        {
            StaticRaidInfo.FirePlayerRemoved(PlayerName);
            CombatSelectionMonitor.NewCombatSelected -= DisplayCombat;
        }
    }
}
