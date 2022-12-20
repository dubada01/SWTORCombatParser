using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;

namespace SWTORCombatParser.ViewModels.Combat_Monitoring
{
    public class EncounterCombat:INotifyPropertyChanged
    {
        public event Action<PastCombat> PastCombatSelected = delegate { };
        public event Action<PastCombat> PastCombatUnselected = delegate { };
        public event Action UnselectAll = delegate { };
        public event PropertyChangedEventHandler PropertyChanged;

        private ObservableCollection<Combat> combats;
        private bool combatsAreVisible = false;
        private bool viewingTrash = false;
        private object combatAddLock = new object();
        public EncounterInfo Info { get; set; }
        public int NumberOfBossBattles => EncounterCombats.Count(c => !c.IsTrash);
        public int NumberOfTrashBattles => EncounterCombats.Count(c => c.IsTrash);
        public GridLength DetailsHeight => Info.IsBossEncounter ? new GridLength(0.5, GridUnitType.Star):new GridLength(0,GridUnitType.Star);
        public string ExpandIconSource { get; set; } = "../../../resources/ExpandUp.png";
        internal void ToggleCombatVisibility()
        {
            if (combatsAreVisible)
            {
                Collapse();
            }
            else
            {
                Expand();
            }
            OnPropertyChanged("ExpandIconSource");
        }

        public void Collapse()
        {
            foreach (var combat in EncounterCombats)
            {
                combat.IsVisible = false;
            }
            combatsAreVisible = false;
            ExpandIconSource = "../../../resources/ExpandUp.png";
        }
        public void Expand()
        {
            foreach (var combat in EncounterCombats)
            {
                if (combat.IsTrash && viewingTrash)
                    combat.IsVisible = true;
                if (!combat.IsTrash)
                    combat.IsVisible = true;
            }
            combatsAreVisible = true;
            ExpandIconSource = "../../../resources/ExpandDown.png";
        }
        public Combat OverallCombat => GetOverallCombat();
        public ObservableCollection<Combat> Combats
        {
            get => combats; set
            {
                combats = value;
            }
        }
        public ObservableCollection<PastCombat> EncounterCombats { get; set; } =  new ObservableCollection<PastCombat>();
        public void AddOngoingCombat(string location)
        {
            //UnselectAll();
            var ongoingCombatDisplay = new PastCombat()
            {
                CombatStartTime = DateTime.Now,
                IsCurrentCombat = true,
                IsSelected = true,
                IsVisible = true,
                CombatLabel = location + " ongoing...",
            };
            ongoingCombatDisplay.PastCombatSelected += SelectCombat;
            ongoingCombatDisplay.PastCombatUnSelected += UnselectCombat;
            ongoingCombatDisplay.UnselectAll += UnselectAllCombats;
            App.Current.Dispatcher.Invoke(() => {
                EncounterCombats.Add(ongoingCombatDisplay);
                EncounterCombats = new ObservableCollection<PastCombat>(EncounterCombats.OrderByDescending(c => c.CombatStartTime));
                OnPropertyChanged("EncounterCombats");
            });

        }
        public void RemoveOngoing()
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                var currentcombat = EncounterCombats.FirstOrDefault(c => c.IsCurrentCombat);
                if (currentcombat == null)
                    return;
                EncounterCombats.Remove(currentcombat);
                OnPropertyChanged("EncounterCombats");
            });
        }
        public PastCombat UpdateOngoing(Combat combat)
        {
            var currentcombat = EncounterCombats.FirstOrDefault(c => c.IsCurrentCombat);
            if (currentcombat == null)
                return new PastCombat();
            currentcombat.CombatDuration = combat.DurationSeconds.ToString("0.00");
            currentcombat.Combat = combat;
            return currentcombat;
        }
        public void AddCombat(Combat combat, bool isReplacingOngoing)
        {
            lock (combatAddLock)
            {
                Combats.Add(combat);
                var pastCombatDisplay = new PastCombat()
                {
                    IsSelected = isReplacingOngoing,
                    Combat = combat,
                    IsVisible = combatsAreVisible,
                    CombatStartTime = combat.StartTime,
                    CombatDuration = combat.DurationSeconds.ToString("0"),
                    CombatLabel = combat.IsCombatWithBoss? combat.EncounterBossInfo : combat.IsPvPCombat ? GetPVPCombatText(combat) : string.Join(',', combat.Targets.Select(t => t.Name).Distinct()),
                };
                pastCombatDisplay.PastCombatSelected += SelectCombat;
                pastCombatDisplay.PastCombatUnSelected += UnselectCombat;
                pastCombatDisplay.UnselectAll += UnselectAllCombats;
                App.Current.Dispatcher.Invoke(() =>
                {
                    EncounterCombats.Add(pastCombatDisplay);
                    EncounterCombats = new ObservableCollection<PastCombat>(EncounterCombats.OrderByDescending(c => c.CombatStartTime));
                    OnPropertyChanged("EncounterCombats");
                });
            }
            OnPropertyChanged("NumberOfBossBattles");
            OnPropertyChanged("NumberOfTrashBattles");
        }

        private string GetPVPCombatText(Combat combat)
        {
            return
                $"Team Kills: {combat.AllLogs.Count(l => l.Effect.EffectId == _7_0LogParsing.DeathCombatId && CombatLogStateBuilder.CurrentState.IsPvpOpponentAtTime(l.Target, l.TimeStamp))}\r\n"+
                $"Team Deaths: {combat.AllLogs.Count(l => l.Effect.EffectId == _7_0LogParsing.DeathCombatId && !CombatLogStateBuilder.CurrentState.IsPvpOpponentAtTime(l.Target, l.TimeStamp))}";
        }

        public void HideTrash()
        {
            viewingTrash = false;
            foreach (var pastCombat in EncounterCombats)
            {
                if (pastCombat.IsTrash)
                    pastCombat.IsVisible = false;
            }
        }
        public void ShowTrash()
        {
            viewingTrash = true;
            foreach (var pastCombat in EncounterCombats)
            {
                if (pastCombat.IsTrash && combatsAreVisible)
                    pastCombat.IsVisible = true;
            }
        }
        private Combat GetOverallCombat()
        {
            lock (combatAddLock)
            {
                Trace.WriteLine("Creating Encounter Combat for: "+ Combats.Count + " combats with " + Combats.SelectMany(c => c.AllLogs).Count() + " logs");
                var overallCombat = CombatIdentifier.GenerateNewCombatFromLogs(Combats.SelectMany(c => c.AllLogs).ToList());
                overallCombat.StartTime = overallCombat.StartTime.AddSeconds(-1);
                return overallCombat;
            }

        }
        private void SelectCombat(PastCombat combat)
        {
            Debug.WriteLine("Selected");
            PastCombatSelected(combat);
        }
        private void UnselectCombat(PastCombat combat)
        {
            PastCombatUnselected(combat);
        }
        private void UnselectAllCombats()
        {
            UnselectAll();
        }
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }


    }
}
