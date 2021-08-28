using MoreLinq;
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
        
        private RaidParticipantInfo selectedParticipant;
        private RaidStateManagement _raidStateManagement;

        public event PropertyChangedEventHandler PropertyChanged;
        public event Action<string> OnRaidParticipantSelected = delegate { };
        public event Action<Combat> OnNewRaidCombatFinished = delegate { };
        public event Action OnNewRaidCombatStarted = delegate { };
        public event Action<bool> RaidStateChanged = delegate { };
        public RaidViewModel()
        {
            _raidStateManagement = new RaidStateManagement();
            _raidStateManagement.UpdatedParticipants += UpdateParticipants;
            _raidStateManagement.CombatFinished += RaidCombatFinished;
            _raidStateManagement.CombatStarted += RaidCombatStarted;
            _raidSelectionViewModel = new RaidSelectionViewModel();
            _raidSelectionViewModel.RaidingStateChanged += UpdateRaidState;
            RaidSelectionContent = new RaidSelectionView(_raidSelectionViewModel);

        }
        public RaidGroupInfo CurrentlySelectedGroup { get; set; }
        public SelectedRaidGroup SelectedRaidGroupView { get; set; }
        public RaidParticipantInfo SelectedParticipant { get => selectedParticipant; set 
            { 
                selectedParticipant = value;
                if (selectedParticipant == null)
                    return;
                OnRaidParticipantSelected(selectedParticipant.PlayerName);
                foreach(var combat in SelectedParticipant.PastCombats)
                {
                    OnNewRaidCombatFinished(combat);
                }
            }
        }
        public ObservableCollection<RaidParticipantInfo> RaidParticipants { get; set; } = new ObservableCollection<RaidParticipantInfo>();
        public RaidSelectionView RaidSelectionContent { get; set; }
        public ICommand RemoveRaidGroupCommand => new CommandHandler(RemoveRaidGroup);

        private void RemoveRaidGroup()
        {
            _raidSelectionViewModel.Cancel();
        }
        private void RaidCombatStarted()
        {
            OnNewRaidCombatStarted();
        }
        private void RaidCombatFinished()
        {
            if(SelectedParticipant == null)
            {
                var playerName = CombatLogStateBuilder.GetLocalPlayerClassandName().PlayerName;
                if (!string.IsNullOrEmpty(playerName))
                {
                    var localParticipant = RaidParticipants.FirstOrDefault(p => p.PlayerName == playerName);
                    SelectedParticipant = localParticipant == null ? RaidParticipants.First() : localParticipant;
                }
            }
            if (selectedParticipant.CurrentCombatInfo.Logs.Count == 0)
                return;
            OnNewRaidCombatFinished(selectedParticipant.CurrentCombatInfo);
        }
        private void UpdateParticipants(List<RaidParticipantInfo> participants)
        {
            if (CurrentlySelectedGroup == null)
                return;
            var removedParticpants = RaidParticipants.Where(rp => !participants.Contains(rp)).ToList();
            foreach (var participant in removedParticpants)
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    RaidParticipants.Remove(participant);
                });
            }
            foreach(var participant in participants)
            {
                if(!RaidParticipants.Any(p=>p.LogName == participant.LogName))
                {
                    App.Current.Dispatcher.Invoke(() => {
                        RaidParticipants.Add(participant);
                    });
                }
            }
            var maxDpsParticipant = RaidParticipants.Where(p=>p.CurrentCombatInfo!=null).MaxBy(p => p.CurrentCombatInfo.DPS).FirstOrDefault();
            var maxEHPSParticipant = RaidParticipants.Where(p => p.CurrentCombatInfo != null).MaxBy(p => p.CurrentCombatInfo.EHPS).FirstOrDefault();
            CurrentlySelectedGroup.SetLeaders(maxDpsParticipant?.PlayerName+": ", maxDpsParticipant?.CurrentCombatInfo.DPS.ToString("#,##0.0"), maxEHPSParticipant?.PlayerName + ": ", maxEHPSParticipant?.CurrentCombatInfo.EHPS.ToString("#,##0.0"));
        }
        private void UpdateRaidState(bool isActive, RaidGroupInfo selectedRaid)
        {
            if (isActive)
            {
                RaidParticipants.Clear();
                CombatLogParser.SetCurrentRaidGroup(selectedRaid);
                CurrentlySelectedGroup = selectedRaid;

                _raidStateManagement.StartRaiding(selectedRaid.GroupId);
                SelectedRaidGroupView = new SelectedRaidGroup();
                SelectedRaidGroupView.DataContext = this;
            }
            else
            {
                _raidStateManagement.StopRaiding();
                RaidParticipants.Clear();
                SelectedRaidGroupView = null;
                CurrentlySelectedGroup = null;
                SelectedParticipant = null;
                CombatLogParser.ClearRaidGroup();
            }
            RaidStateChanged(isActive);
            OnPropertyChanged("SelectedRaidGroupView");
            OnPropertyChanged("CombatsInRaidGroup");
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
