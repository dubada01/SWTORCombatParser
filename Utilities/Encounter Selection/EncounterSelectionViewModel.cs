using SWTORCombatParser.DataStructures.EncounterInfo;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using ReactiveUI;

namespace SWTORCombatParser.Utilities.Encounter_Selection
{
    public class EncounterSelectionViewModel : ReactiveObject
    {
        public EncounterInfo selectedEncounter;
        public string selectedBoss;
        private string selectedPlayerCount;
        private List<string> _allPlayerCounts = new List<string> { "8", "16" };
        private List<string> _populatedEncounters = new List<string>();
        private bool _showPlayerCount;
        private ObservableCollection<string> _availablePlayerCounts = new ObservableCollection<string>();
        private List<string> _availableBosses;
        private List<EncounterInfo> _availableEncounters;
        public event Action<string, string> SelectionUpdated = delegate { };

        public List<EncounterInfo> AvailableEncounters
        {
            get => _availableEncounters;
            set => this.RaiseAndSetIfChanged(ref _availableEncounters, value);
        }

        public EncounterInfo SelectedEncounter
        {
            get => selectedEncounter;
            set
            {
                if(value == null)
                    return;
                if (value.Name.Contains("--"))
                    return;
                this.RaiseAndSetIfChanged(ref selectedEncounter, value);
                ShowPlayerCount = ShowPlayerCount && selectedEncounter.EncounterType != EncounterType.Flashpoint;
                if (!ShowPlayerCount)
                    selectedPlayerCount = "";
                SetSelectedEncounter();
            }
        }

        public bool ShowPlayerCount
        {
            get => _showPlayerCount;
            set => this.RaiseAndSetIfChanged(ref _showPlayerCount, value);
        }

        public List<string> AvailableBosses
        {
            get => _availableBosses;
            set => this.RaiseAndSetIfChanged(ref _availableBosses, value);
        }

        public string SelectedBoss
        {
            get => selectedBoss;
            set
            {
                this.RaiseAndSetIfChanged(ref selectedBoss, value);
                SetSelectedBoss();
            }
        }

        public ObservableCollection<string> AvailablePlayerCounts
        {
            get => _availablePlayerCounts;
            set => this.RaiseAndSetIfChanged(ref _availablePlayerCounts, value);
        }

        public string SelectedPlayerCount
        {
            get => selectedPlayerCount; set
            {
                this.RaiseAndSetIfChanged(ref selectedPlayerCount, value);
                if (selectedPlayerCount == null)
                    return;
            }
        }
        public (string, string) GetCurrentSelection()
        {
            return (selectedEncounter.Name, selectedBoss);
        }
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
        }
        private void SetSelectedBoss()
        {
            AvailablePlayerCounts = new ObservableCollection<string>(_allPlayerCounts);

            if (AvailablePlayerCounts.Any() && !AvailablePlayerCounts.Contains(selectedPlayerCount))
                selectedPlayerCount = AvailablePlayerCounts[0];
            SelectionUpdated.InvokeSafely(selectedEncounter.Name, selectedBoss);
        }
    }
}
