using Newtonsoft.Json;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.Raiding;
using SWTORCombatParser.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SWTORCombatParser.ViewModels
{

    public class RaidViewModel:INotifyPropertyChanged
    {
        private RaidSelectionViewModel _raidSelectionViewModel;
        private DateTime _mostRecentLog;
        private bool _raidingActive;
        private PostgresConnection _postgresConnection;
        private Dictionary<string, RaidParticipantInfo> _participantRaidLogs = new Dictionary<string, RaidParticipantInfo>();
        private RaidParticipantInfo selectedParticipant;

        public event PropertyChangedEventHandler PropertyChanged;
        public event Action<bool> RaidStateChanged = delegate { };
        public RaidViewModel()
        {
            _postgresConnection = new PostgresConnection();
            _raidSelectionViewModel = new RaidSelectionViewModel();
            _raidSelectionViewModel.RaidingStateChanged += UpdateRaidState;
            RaidSelectionContent = new RaidSelectionView(_raidSelectionViewModel);

        }
        public RaidInfo CurrentlySelectedGroup { get; set; }
        public SelectedRaidGroup SelectedRaidGroupView { get; set; }
        public RaidParticipantInfo SelectedParticipant { get => selectedParticipant; set 
            { 
                selectedParticipant = value;
            }
        }
        public ObservableCollection<RaidParticipantInfo> CombatsInRaidGroup { get; set; } = new ObservableCollection<RaidParticipantInfo>();
        public RaidSelectionView RaidSelectionContent { get; set; }
        public ICommand RemoveRaidGroupCommand => new CommandHandler(RemoveRaidGroup);

        private void RemoveRaidGroup()
        {
            _raidSelectionViewModel.Cancel();
        }

        private void UpdateRaidState(bool isActive, RaidInfo selectedRaid)
        {
            _raidingActive = isActive;
            if (isActive)
            {
                _participantRaidLogs = new Dictionary<string, RaidParticipantInfo>();
                CombatLogParser.SetCurrentRaidGroup(selectedRaid);
                CurrentlySelectedGroup = selectedRaid;
                _mostRecentLog = DateTime.Now;
                PollForRaidUpdates();
                SelectedRaidGroupView = new SelectedRaidGroup();
                SelectedRaidGroupView.DataContext = this;
            }
            else
            {
                SelectedRaidGroupView = null;
                CurrentlySelectedGroup = null;
                CombatLogParser.ClearRaidGroup();
            }
            RaidStateChanged(isActive);
            OnPropertyChanged("SelectedRaidGroupView");
        }
        private void PollForRaidUpdates()
        {
            Task.Run(() =>
            {
                while (_raidingActive)
                {
                    Thread.Sleep(1000);
                    if (!_raidingActive)
                        return;
                    var logs = _postgresConnection.GetLogsAfterTime(_mostRecentLog, CurrentlySelectedGroup.GroupId);
                    if (!logs.Any())
                        continue;
                    var ordered = logs.OrderBy(t => t.TimeStamp);
                    if (logs.Any(c => c.Effect.EffectName == "EnterCombat"))
                    {
                        _participantRaidLogs = new Dictionary<string, RaidParticipantInfo>();
                        App.Current.Dispatcher.Invoke(() =>
                        {
                            CombatsInRaidGroup.Clear();
                        });

                    }
                    if (logs.Any(c => c.Effect.EffectName == "ExitCombat" || (c.Effect.EffectName == "Death" && c.Target.IsPlayer)))
                    {
                        _mostRecentLog = ordered.Last().TimeStamp;
                    }

                    var logsByFile = ordered.GroupBy(l => l.LogName);


                    foreach (var logFile in logsByFile)
                    {
                        var logName = logFile.Key;
                        var participantLogs = logFile.ToList();
                        if (!_participantRaidLogs.ContainsKey(logName))
                        {
                            _participantRaidLogs[logName] = new RaidParticipantInfo(participantLogs);
                            App.Current.Dispatcher.Invoke(() =>
                            {
                                CombatsInRaidGroup.Add(_participantRaidLogs[logName]);
                            });
                        }
                        else
                        {
                            _participantRaidLogs[logName].Update(participantLogs);
                        }


                    }
                }
            });
        }
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
