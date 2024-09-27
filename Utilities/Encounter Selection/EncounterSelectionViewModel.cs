using SWTORCombatParser.DataStructures.EncounterInfo;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Threading;

namespace SWTORCombatParser.Utilities.Encounter_Selection
{
    public class EncounterSelectionViewModel : INotifyPropertyChanged
    {
        public EncounterInfo selectedEncounter;
        public string selectedBoss;
        private string selectedPlayerCount;
        private List<string> _allPlayerCounts = new List<string> { "8", "16" };

        public event Action<string, string> SelectionUpdated = delegate { };

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
        public (string, string) GetCurrentSelection()
        {
            return (selectedEncounter.Name, selectedBoss);
        }
        private List<string> _populatedEncounters = new List<string>();
        public EncounterSelectionViewModel(bool showPlayerCount = true, List<string> populatedEncounters = null)
        {
            _populatedEncounters = populatedEncounters ?? new List<string>();
            ShowPlayerCount = showPlayerCount;
            SetAvailableEncounters();
        }

        private void SetAvailableEncounters()
        {
            var allEncounters = EncounterLister.SortedEncounterInfos;
            Dispatcher.UIThread.Invoke(() =>
            {
                if (!_populatedEncounters.Any())
                    AvailableEncounters = allEncounters.ToList();
                else
                    AvailableEncounters = allEncounters.Where(e => _populatedEncounters.Any(pe => pe.Split("|")[0] == e.Name)).ToList();
                OnPropertyChanged("AvailableEncounters");
                SelectedEncounter = AvailableEncounters[1];
            });
        }
        private void SetSelectedEncounter()
        {
            var bossesForEncounter = EncounterLister.GetBossesForEncounter(SelectedEncounter.Name);
            if (!_populatedEncounters.Any())
                AvailableBosses = bossesForEncounter.ToList();
            else
                AvailableBosses = bossesForEncounter.Where(e => _populatedEncounters.Any(pe => pe.Split("|")[1] == e)).ToList();

            if (AvailableBosses.Any())
                SelectedBoss = AvailableBosses[0];
            OnPropertyChanged("AvailableBosses");
        }
        private void SetSelectedBoss()
        {
            AvailablePlayerCounts = new ObservableCollection<string>(_allPlayerCounts);
            OnPropertyChanged("AvailablePlayerCounts");

            if (AvailablePlayerCounts.Any() && !AvailablePlayerCounts.Contains(selectedPlayerCount))
                selectedPlayerCount = AvailablePlayerCounts[0];
            OnPropertyChanged("SelectedPlayerCount");
            SelectionUpdated(selectedEncounter.Name, selectedBoss);
        }
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
