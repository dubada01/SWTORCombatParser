using MoreLinq;
using SWTORCombatParser.DataStructures.RaidInfos;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.Views.Leaderboard_View;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser.ViewModels.Leaderboard
{
    public class LeaderboardViewModel : INotifyPropertyChanged
    {
        private EncounterInfo selectedEncounter;
        private string selectedBoss;
        private List<LeaderboardInstanceViewModel> _viewModels = new List<LeaderboardInstanceViewModel>();
        private string selectedDifficulty;
        private string selectedPlayerCount;
        private List<string> _allDifficulties = new List<string> { "Story", "Veteran", "Master" };
        private List<string> _allPlayerCounts = new List<string> { "8", "16" };

        public event PropertyChangedEventHandler PropertyChanged;

        public List<EncounterInfo> AvailableEncounters { get; set; }
        public EncounterInfo SelectedEncounter
        {
            get => selectedEncounter;
            set
            {
                if (value.Name.Contains("--"))
                    return;
                selectedEncounter=value;
                ShowPlayerCount = selectedEncounter.EncounterType != EncounterType.Flashpoint;
                if (!ShowPlayerCount)
                    selectedPlayerCount = "";
                OnPropertyChanged("ShowPlayerCount");
                SetSelectedEncounter();
                OnPropertyChanged();
            }
        }
        public bool ShowPlayerCount { get; set; }
        public List<string> AvailableBosses { get; set; }
        public string SelectedBoss
        {
            get => selectedBoss;
            set
            {
                selectedBoss=value;
                OnPropertyChanged();
                SetSelectedBoss();
            }
        }
        public ObservableCollection<string> AvailableDifficulties { get; set; } = new ObservableCollection<string>();
        public string SelectedDifficulty
        {
            get => selectedDifficulty; set
            {
                selectedDifficulty=value;
                OnPropertyChanged();
                if (selectedDifficulty == null)
                    return;
                _viewModels.ForEach(vm => vm.Populate(SelectedEncounter.Name, SelectedBoss, SelectedDifficulty, SelectedPlayerCount));
            }
        }
        public ObservableCollection<string> AvailablePlayerCounts { get; set; } = new ObservableCollection<string>();
        public string SelectedPlayerCount
        {
            get => selectedPlayerCount; set
            {
                selectedPlayerCount=value;
                OnPropertyChanged();
                if (selectedPlayerCount == null)
                    return;
                _viewModels.ForEach(vm => vm.Populate(SelectedEncounter.Name, SelectedBoss, SelectedDifficulty, SelectedPlayerCount));
            }
        }
        public LeaderboardInstance DamageContent { get; set; }
        public LeaderboardInstance FocusDamageContent { get; set; }
        public LeaderboardInstance HealingContent { get; set; }
        public LeaderboardInstance EffectiveHealingContent { get; set; }
        public LeaderboardInstance MitigationContent { get; set; }
        public LeaderboardViewModel()
        {
            var damageVM = new LeaderboardInstanceViewModel(LeaderboardEntryType.Damage);
            _viewModels.Add(damageVM);
            DamageContent = new LeaderboardInstance(damageVM);
            OnPropertyChanged("DamageContent");

            var focusDamageVM = new LeaderboardInstanceViewModel(LeaderboardEntryType.FocusDPS);
            _viewModels.Add(focusDamageVM);
            FocusDamageContent = new LeaderboardInstance(focusDamageVM);
            OnPropertyChanged("FocusDamageContent");

            var healingVM = new LeaderboardInstanceViewModel(LeaderboardEntryType.Healing);
            _viewModels.Add(healingVM);
            HealingContent = new LeaderboardInstance(healingVM);
            OnPropertyChanged("HealingContent");

            var effectiveHealsVM = new LeaderboardInstanceViewModel(LeaderboardEntryType.EffectiveHealing);
            _viewModels.Add(effectiveHealsVM);
            EffectiveHealingContent = new LeaderboardInstance(effectiveHealsVM);
            OnPropertyChanged("EffectiveHealingContent");

            var mitigationVM = new LeaderboardInstanceViewModel(LeaderboardEntryType.Mitigation);
            _viewModels.Add(mitigationVM);
            MitigationContent = new LeaderboardInstance(mitigationVM);
            OnPropertyChanged("MitigationContent");

            SetAvailableEncounters();
            PostgresConnection.LeaderboardUpdated += SetAvailableEncounters;
        }
        private async void SetAvailableEncounters()
        {
            var savedEncounters = await PostgresConnection.GetEncountersWithEntries();
            var allEncounters = EncounterLister.SortedEncounterInfos;
            App.Current.Dispatcher.Invoke(() => {
                AvailableEncounters = allEncounters.Where(e => savedEncounters.Contains(e.Name) || e.Name.Contains("--")).ToList();
                OnPropertyChanged("AvailableEncounters");
                SelectedEncounter = AvailableEncounters[1];
            });
        }
        private async void SetSelectedEncounter()
        {
            var bossesSavedForEncounter = await PostgresConnection.GetBossesFromEncounterWithEntries(SelectedEncounter.Name);
            var namesAndDifficulties = bossesSavedForEncounter.Select(s => s.Split('{'));
            var names = namesAndDifficulties.Select(nd => nd[0].Trim()).Distinct();

            var bossesForEncounter = EncounterLister.GetBossesForEncounter(SelectedEncounter.Name);

            AvailableBosses = bossesForEncounter.Where(b => names.Contains(b)).ToList();
            if(AvailableBosses.Any())
                SelectedBoss = AvailableBosses[0];
            OnPropertyChanged("AvailableBosses");
        }
        private async void SetSelectedBoss()
        {
            var bossesSavedForEncounter = await PostgresConnection.GetBossesFromEncounterWithEntries(SelectedEncounter.Name);
            var namesAndDifficulties = bossesSavedForEncounter.Where(b=>b.Contains(SelectedBoss)).ToList();
            var difficultiesAndPlayers = namesAndDifficulties.Select(s => s.Split('{')).Select(s=>s[1]);
            var cleaned = difficultiesAndPlayers.Select(d => d.Replace("}", "").Split(" "));

            var difficulties = new List<string>();
            var counts = new List<string>();

            foreach(var combo in cleaned)
            {
                if (combo.Length > 1)
                { 
                    if(!counts.Contains(combo[0].Trim()))
                        counts.Add(combo[0].Trim());
                    difficulties.Add(combo[1].Trim());
                }
                else
                {
                    difficulties.Add(combo[0].Trim());
                }
            }
            AvailableDifficulties = new ObservableCollection<string>(_allDifficulties.Where(d => difficulties.Contains(d)));
            AvailablePlayerCounts = new ObservableCollection<string>(_allPlayerCounts.Where(c => counts.Contains(c)));
            OnPropertyChanged("AvailableDifficulties");
            OnPropertyChanged("AvailablePlayerCounts");

            if(!AvailableDifficulties.Contains(selectedDifficulty))
                selectedDifficulty = AvailableDifficulties[0];
            if(AvailablePlayerCounts.Any() && !AvailablePlayerCounts.Contains(selectedPlayerCount))
                selectedPlayerCount = AvailablePlayerCounts[0];
            OnPropertyChanged("SelectedDifficulty");
            OnPropertyChanged("SelectedPlayerCount");
            _viewModels.ForEach(vm => vm.Populate(SelectedEncounter.Name, SelectedBoss, selectedDifficulty, selectedPlayerCount));
        }
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
