using SWTORCombatParser.DataStructures.RaidInfos;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser.Utilities
{
    public class EncounterSelectionViewModel:INotifyPropertyChanged
    {
        private EncounterInfo selectedEncounter;
        private string selectedBoss;
        private string selectedDifficulty;
        private string selectedPlayerCount;
        private List<string> _allDifficulties = new List<string> { "Story", "Veteran", "Master" };
        private List<string> _allPlayerCounts = new List<string> { "8", "16" };

        public event Action<string,string,string> SelectionUpdated = delegate { };

        public event PropertyChangedEventHandler PropertyChanged;
        public List<EncounterInfo> AvailableEncounters { get; set; }
        public EncounterInfo SelectedEncounter
        {
            get => selectedEncounter;
            set
            {
                if (value.Name.Contains("--"))
                    return;
                selectedEncounter = value;
                ShowPlayerCount = ShowPlayerCount && selectedEncounter.EncounterType != EncounterType.Flashpoint;
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
                SelectionUpdated(selectedEncounter.Name, selectedBoss, selectedDifficulty);
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
            }
        }
        public (string,string,string) GetCurrentSelection()
        {
            return (selectedEncounter.Name,selectedBoss, selectedDifficulty);
        }
        public EncounterSelectionViewModel(bool showPlayerCount = true)
        {
            ShowPlayerCount = showPlayerCount;
            SetAvailableEncounters();
        }

        private void SetAvailableEncounters()
        {
            var allEncounters = EncounterLister.SortedEncounterInfos;
            App.Current.Dispatcher.Invoke(() => {
                AvailableEncounters = allEncounters.ToList();
                OnPropertyChanged("AvailableEncounters");
                SelectedEncounter = AvailableEncounters[1];
            });
        }
        private void SetSelectedEncounter()
        {
            var bossesForEncounter = EncounterLister.GetBossesForEncounter(SelectedEncounter.Name);
            AvailableBosses = bossesForEncounter.ToList();
            if (AvailableBosses.Any())
                SelectedBoss = AvailableBosses[0];
            OnPropertyChanged("AvailableBosses");
        }
        private void SetSelectedBoss()
        {
            AvailableDifficulties = new ObservableCollection<string>(_allDifficulties);
            AvailablePlayerCounts = new ObservableCollection<string>(_allPlayerCounts);
            OnPropertyChanged("AvailableDifficulties");
            OnPropertyChanged("AvailablePlayerCounts");

            if (!AvailableDifficulties.Contains(selectedDifficulty))
                selectedDifficulty = AvailableDifficulties[0];
            if (AvailablePlayerCounts.Any() && !AvailablePlayerCounts.Contains(selectedPlayerCount))
                selectedPlayerCount = AvailablePlayerCounts[0];
            OnPropertyChanged("SelectedDifficulty");
            OnPropertyChanged("SelectedPlayerCount");
            SelectionUpdated(selectedEncounter.Name, selectedBoss, selectedDifficulty);
        }
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
