using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Utilities;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SWTORCombatParser.ViewModels.Leaderboard
{
    public class LeaderboardViewModel : INotifyPropertyChanged
    {
        private EncounterInfo selectedEncounter;
        private string selectedBoss;
        private List<LeaderboardInstanceViewModel> _viewModels = new List<LeaderboardInstanceViewModel>();
        private string selectedDifficulty;
        private string selectedPlayerCount;
        private Dictionary<string, long> _parsingLevels = new Dictionary<string, long> { { "1 Million", 1000000 }, { "2 Million", 2000000 }, { "3.25 Million", 3250000 }, { "6.5 Million", 6500000 }, { "10 Million", 10000000 }, { "Story", 0 }, { "Veteran", 0 }, { "Master", 0 }, {"Open World",0 } };
        private List<string> _allDifficulties = new List<string> { "Story", "Veteran", "Master" };
        private List<string> _allPlayerCounts = new List<string> { "8", "16" };
        private List<string> bossesSavedForEncounter;

        public event PropertyChangedEventHandler PropertyChanged;

        public List<EncounterInfo> AvailableEncounters { get; set; }
        public EncounterInfo SelectedEncounter
        {
            get => selectedEncounter;
            set
            {
                if (value == null || value.Name.Contains("--"))
                    return;
                selectedEncounter = value;
                ShowPlayerCount = selectedEncounter.EncounterType != EncounterType.Flashpoint && selectedEncounter.Name != "Dummy Parsing";
                if (!ShowPlayerCount)
                    selectedPlayerCount = "";
                OnPropertyChanged("ShowPlayerCount");
                SetSelectedEncounter();
                OnPropertyChanged();
            }
        }
        public string LeaderboardVersion { get; set; }
        public bool ShowPlayerCount { get; set; }
        public List<string> AvailableBosses { get; set; }
        public string SelectedBoss
        {
            get => selectedBoss;
            set
            {
                selectedBoss = value;
                OnPropertyChanged();
                SetSelectedBoss();
            }
        }
        public ObservableCollection<string> AvailableDifficulties { get; set; } = new ObservableCollection<string>();
        public string SelectedDifficulty
        {
            get => selectedDifficulty; set
            {
                selectedDifficulty = value;
                OnPropertyChanged();
                if (selectedDifficulty == null)
                    return;
                _viewModels.ForEach(vm => vm.Populate(SelectedEncounter.Name, SelectedBoss, SelectedDifficulty, SelectedPlayerCount, SelectedEncounter.Name == "Parsing", _parsingLevels[selectedDifficulty]));
            }
        }
        public ObservableCollection<string> AvailablePlayerCounts { get; set; } = new ObservableCollection<string>();
        public string SelectedPlayerCount
        {
            get => selectedPlayerCount; set
            {
                selectedPlayerCount = value;
                OnPropertyChanged();
                if (selectedPlayerCount == null)
                    return;
                _viewModels.ForEach(vm => vm.Populate(SelectedEncounter.Name, SelectedBoss, SelectedDifficulty, SelectedPlayerCount, SelectedEncounter.Name == "Parsing", _parsingLevels[selectedDifficulty]));
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
            //PostgresConnection.LeaderboardUpdated += SetAvailableEncounters;
            LeaderboardVersion = $"leaderboard v" + Leaderboards._leaderboardVersion;
        }
        private async void SetAvailableEncounters()
        {
            var savedEncounters = await API_Connection.GetEncountersWithEntries();
            var uniqueSavedEncounters = savedEncounters.Distinct();
            var allEncounters = EncounterLister.SortedEncounterInfos;
            allEncounters.Insert(0, new EncounterInfo { Name = "Parsing" });
            App.Current.Dispatcher.Invoke(() =>
            {
                AvailableEncounters = allEncounters.Where(e => uniqueSavedEncounters.Contains(e.Name) || e.IsOpenWorld || e.Name.Contains("--")).ToList();
                OnPropertyChanged("AvailableEncounters");
                SelectedEncounter = AvailableEncounters[0];
            });
        }
        private async void SetSelectedEncounter()
        {
            if(SelectedEncounter.Name == "Open World")
            {
                bossesSavedForEncounter.Clear();
                var savedEncounters = await API_Connection.GetEncountersWithEntries();
                var openWorldEncounters = savedEncounters.Where(n => n.Contains("Open World")).Distinct();
                foreach(var encounter in openWorldEncounters) {
                    bossesSavedForEncounter.AddRange(await API_Connection.GetBossesFromEncounterWithEntries(encounter));
                }
            }
            else
            {
                bossesSavedForEncounter = await API_Connection.GetBossesFromEncounterWithEntries(SelectedEncounter.Name);
            }

            var namesAndDifficulties = bossesSavedForEncounter.Select(s => s.Split('{'));
            var names = namesAndDifficulties.Select(nd => nd[0].Trim()).Distinct();

            var bossesForEncounter = EncounterLister.GetBossesForEncounter(SelectedEncounter.Name);

            AvailableBosses = bossesForEncounter.Where(b => names.Contains(b)).ToList();
            if (SelectedEncounter.Name == "Parsing")
                AvailableBosses = names.Distinct().ToList();
            if (AvailableBosses.Any())
                SelectedBoss = AvailableBosses[0];
            OnPropertyChanged("AvailableBosses");
        }
        private async void SetSelectedBoss()
        {
            var namesAndDifficulties = bossesSavedForEncounter.Where(b => b.Contains(SelectedBoss)).ToList();
            var difficultiesAndPlayers = namesAndDifficulties.Select(s => s.Split('{')).Select(s => s[1]);
            var cleaned = difficultiesAndPlayers.Select(d => d.Replace("}", "").Split(" "));

            var difficulties = new List<string>();
            var counts = new List<string>();

            foreach (var combo in cleaned)
            {
                if (SelectedEncounter.Name == "Parsing")
                {
                    difficulties.Add(combo[0].Trim().Replace("HP", ""));
                }
                else
                {
                    if (combo.Length > 1)
                    {
                        if (!counts.Contains(combo[0].Trim()))
                            counts.Add(combo[0].Trim());
                        difficulties.Add(combo[1].Trim());
                    }
                    else
                    {
                        difficulties.Add(combo[0].Trim());
                    }
                }
            }
            if (SelectedEncounter.Name == "Parsing")
            {
                AvailableDifficulties = new ObservableCollection<string>(_parsingLevels.Where(d => difficulties.Contains(d.Value.ToString())).Select(kv => kv.Key));
            }
            else
                AvailableDifficulties = new ObservableCollection<string>(_allDifficulties.Where(d => difficulties.Contains(d)));


            if(SelectedEncounter.Name == "Open World")
            {
                AvailableDifficulties = new ObservableCollection<string> { "Open World" };
                AvailablePlayerCounts = new ObservableCollection<string>() { "Open World"};
            }
            else
            {
                AvailablePlayerCounts = new ObservableCollection<string>(_allPlayerCounts.Where(c => counts.Contains(c)));
            }

            OnPropertyChanged("AvailableDifficulties");
            OnPropertyChanged("AvailablePlayerCounts");

            if (!AvailableDifficulties.Contains(selectedDifficulty))
                selectedDifficulty = AvailableDifficulties[0];
            if (AvailablePlayerCounts.Any() && !AvailablePlayerCounts.Contains(selectedPlayerCount))
                selectedPlayerCount = AvailablePlayerCounts[0];
            OnPropertyChanged("SelectedDifficulty");
            OnPropertyChanged("SelectedPlayerCount");


            _viewModels.ForEach(vm => vm.Populate(SelectedEncounter.Name, SelectedBoss, selectedDifficulty == "Open World" ? "" : selectedDifficulty, selectedPlayerCount, SelectedEncounter.Name == "Parsing", _parsingLevels[selectedDifficulty]));
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
