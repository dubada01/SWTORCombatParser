using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using ReactiveUI;

namespace SWTORCombatParser.ViewModels.Combat_Monitoring
{
    public class EncounterCombat : ReactiveObject
    {
        public event Action<PastCombat> PastCombatSelected = delegate { };
        public event Action<PastCombat> PastCombatUnselected = delegate { };
        public event Action UnselectAll = delegate { };

        private ObservableCollection<Combat> combats;
        private bool combatsAreVisible = false;
        private bool viewingTrash = false;
        private object combatAddLock = new object();
        private ObservableCollection<PastCombat> _encounterCombats = new ObservableCollection<PastCombat>();
        private Bitmap _expandIconSource = collapseIcon;
        public EncounterInfo Info { get; set; }
        public string PPHInfo => Info.IsBossEncounter && combats.Count > 1 ? $"PPH {Combats.Count / (combats.Last().StartTime - combats.First().StartTime).TotalHours:N2}" : "";
        public int NumberOfBossBattles => EncounterCombats.Count(c => !c.IsTrash);
        public int NumberOfTrashBattles => EncounterCombats.Count(c => c.IsTrash);
        public GridLength DetailsHeight => Info.IsBossEncounter ? new GridLength(0.5, GridUnitType.Star) : new GridLength(0, GridUnitType.Star);

        public Bitmap ExpandIconSource
        {
            get => _expandIconSource;
            set => this.RaiseAndSetIfChanged(ref _expandIconSource, value);
        }

        private static readonly Bitmap collapseIcon = new Bitmap(AssetLoader.Open(new Uri("avares://Orbs/resources/ExpandUp.png")));
        private static readonly Bitmap expandIcon = new Bitmap(AssetLoader.Open(new Uri("avares://Orbs/resources/ExpandDown.png")));
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
        }

        public void Collapse()
        {
            foreach (var combat in EncounterCombats)
            {
                combat.IsVisible = false;
            }
            combatsAreVisible = false;
            ExpandIconSource = collapseIcon;
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
            ExpandIconSource = expandIcon;
        }
        public Combat OverallCombat => GetOverallCombat();
        public ObservableCollection<Combat> Combats
        {
            get => combats; set
            {
                this.RaiseAndSetIfChanged(ref combats, value);
            }
        }

        public ObservableCollection<PastCombat> EncounterCombats
        {
            get => _encounterCombats;
            set => this.RaiseAndSetIfChanged(ref _encounterCombats, value);
        }

        public void AddOngoingCombat(string location)
        {
            //UnselectAll();
            var ongoingCombatDisplay = new PastCombat()
            {
                CombatStartTime = TimeUtility.CorrectedTime,
                IsCurrentCombat = true,
                IsSelected = true,
                IsVisible = true,
                CombatLabel = location + " ongoing...",
            };
            ongoingCombatDisplay.PastCombatSelected += SelectCombat;
            ongoingCombatDisplay.PastCombatUnSelected += UnselectCombat;
            ongoingCombatDisplay.UnselectAll += UnselectAllCombats;
            Dispatcher.UIThread.Invoke(() =>
            {
                EncounterCombats.Add(ongoingCombatDisplay);
                EncounterCombats = new ObservableCollection<PastCombat>(EncounterCombats.OrderByDescending(c => c.CombatStartTime));
            });

        }
        public void RemoveOngoing()
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                var currentcombat = EncounterCombats.FirstOrDefault(c => c.IsCurrentCombat);
                if (currentcombat == null)
                    return;
                EncounterCombats.Remove(currentcombat);
            });
        }
        public PastCombat UpdateOngoing(Combat combat)
        {
            var currentcombat = EncounterCombats.FirstOrDefault(c => c.IsCurrentCombat);
            if (currentcombat == null)
                return new PastCombat();
            currentcombat.CombatDuration = TimeSpan.FromSeconds(combat.DurationSeconds).ToString(@"mm\:ss");
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
                    CombatDuration = TimeSpan.FromSeconds(combat.DurationSeconds).ToString(@"mm\:ss"),
                    CombatLabel = combat.IsCombatWithBoss ? combat.EncounterBossInfo : combat.IsPvPCombat ? GetPVPCombatText(combat) : string.Join(',', combat.Targets.Select(t => t.Name).Distinct()),
                };
                pastCombatDisplay.PastCombatSelected += SelectCombat;
                pastCombatDisplay.PastCombatUnSelected += UnselectCombat;
                pastCombatDisplay.UnselectAll += UnselectAllCombats;
                Dispatcher.UIThread.Invoke(() =>
                {
                    EncounterCombats.Add(pastCombatDisplay);
                    EncounterCombats = new ObservableCollection<PastCombat>(EncounterCombats.OrderByDescending(c => c.CombatStartTime));
                });
            }
            this.RaisePropertyChanged(nameof(PPHInfo));
            this.RaisePropertyChanged(nameof(NumberOfBossBattles));
            this.RaisePropertyChanged(nameof(NumberOfTrashBattles));
        }

        private string GetPVPCombatText(Combat combat)
        {
            return
                $"Team Kills: {combat.AllLogs.Count(l => l.Effect.EffectId == _7_0LogParsing.DeathCombatId && CombatLogStateBuilder.CurrentState.IsPvpOpponentAtTime(l.Target, l.TimeStamp))}\r\n" +
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
                var overallCombat = CombatIdentifier.GenerateNewCombatFromLogs(Combats.SelectMany(c => c.AllLogs).ToList());
                overallCombat.StartTime = overallCombat.StartTime.AddSeconds(-1);
                return overallCombat;
            }

        }
        private void SelectCombat(PastCombat combat)
        {
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
    }
}
