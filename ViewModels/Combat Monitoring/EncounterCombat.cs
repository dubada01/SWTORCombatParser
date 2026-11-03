using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.CombatCaching;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
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
        private bool combatsAreVisible;
        private bool viewingTrash;
        private readonly object combatAddLock = new();
        private ObservableCollection<PastCombat> _encounterCombats = new();
        private Bitmap _expandIconSource = collapseIcon;
        private string _encounterDurationText;

        public EncounterInfo Info { get; set; }
        public string PPHInfo =>
            Info.IsBossEncounter && combats.Count > 1
                ? $"PPH {Combats.Count / (combats.OrderBy(c => c.StartTime).Last().StartTime - combats.OrderBy(c => c.StartTime).First().StartTime).TotalHours:N2}"
                : "";

        public int NumberOfBossBattles => EncounterCombats.Count(c => !c.IsTrash);
        public int NumberOfTrashBattles => EncounterCombats.Count(c => c.IsTrash);
        public GridLength DetailsHeight => Info.IsBossEncounter ? new GridLength(0.5, GridUnitType.Star) : new GridLength(0, GridUnitType.Star);

        public string EncounterDurationText
        {
            get => _encounterDurationText;
            set => this.RaiseAndSetIfChanged(ref _encounterDurationText, value);
        }

        public Bitmap ExpandIconSource
        {
            get => _expandIconSource;
            set => this.RaiseAndSetIfChanged(ref _expandIconSource, value);
        }

        private static readonly Bitmap collapseIcon = new(AssetLoader.Open(new Uri("avares://Orbs/resources/ExpandUp.png")));
        private static readonly Bitmap expandIcon = new(AssetLoader.Open(new Uri("avares://Orbs/resources/ExpandDown.png")));

        public ObservableCollection<Combat> Combats
        {
            get => combats;
            set => this.RaiseAndSetIfChanged(ref combats, value);
        }

        public ObservableCollection<PastCombat> EncounterCombats
        {
            get => _encounterCombats;
            set => this.RaiseAndSetIfChanged(ref _encounterCombats, value);
        }

        internal void ToggleCombatVisibility()
        {
            if (combatsAreVisible)
                Collapse();
            else
                Expand();
        }

        public ReactiveCommand<Unit, Unit> OpenOrbsLeaderboardCommand => ReactiveCommand.Create(OpenOrbsLeaderboard);

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

        public void AddOngoingCombat(string location)
        {
            var ongoingCombatDisplay = new PastCombat
            {
                ParentEncounter = this,
                CombatStartTime = TimeUtility.CorrectedTime,
                IsCurrentCombat = true,
                IsSelected = true,
                IsVisible = true,
                CombatLabel = location + " ongoing..."
            };
            ongoingCombatDisplay.PastCombatSelected += SelectCombat;
            ongoingCombatDisplay.PastCombatUnSelected += UnselectCombat;
            ongoingCombatDisplay.UnselectAll += UnselectAllCombats;

            Dispatcher.UIThread.Invoke(() =>
            {
                EncounterCombats.Add(ongoingCombatDisplay);
                EncounterCombats = new ObservableCollection<PastCombat>(EncounterCombats.OrderByDescending(c => c.CombatStartTime));
            });
            CalculateEncounterLength();
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

            currentcombat.SetCombat(combat);
            CalculateEncounterLength();
            return currentcombat;
        }

        public void AddCombat(Combat combat, bool isReplacingOngoing)
        {
            lock (combatAddLock)
            {
                Combats.Add(CombatDiskCacheManager.CreateCombatShell(combat));

                var pastCombatDisplay = new PastCombat
                {
                    ParentEncounter = this,
                    IsSelected = isReplacingOngoing,
                    IsVisible = combatsAreVisible,
                    CombatStartTime = combat.StartTime,
                    CombatDuration = TimeSpan.FromSeconds(combat.DurationSeconds).ToString(@"mm\:ss"),
                    CombatLabel = combat.IsCombatWithBoss
                        ? combat.EncounterBossDifficultyParts.Item1
                        : combat.IsPvPCombat
                            ? GetPVPCombatText(combat)
                            : string.Join(',', combat.Targets.Values.Select(t => t.Name).Distinct())
                };
                pastCombatDisplay.CacheCombat(combat, keepLoaded: isReplacingOngoing);
                pastCombatDisplay.PastCombatSelected += SelectCombat;
                pastCombatDisplay.PastCombatUnSelected += UnselectCombat;
                pastCombatDisplay.UnselectAll += UnselectAllCombats;

                Dispatcher.UIThread.Invoke(() =>
                {
                    EncounterCombats.Add(pastCombatDisplay);
                    EncounterCombats = new ObservableCollection<PastCombat>(EncounterCombats.OrderByDescending(c => c.CombatStartTime));
                });
            }

            if (isReplacingOngoing)
                EncounterMonitor.FireEncounterUpdated();

            CalculateEncounterLength();
            this.RaisePropertyChanged(nameof(PPHInfo));
            this.RaisePropertyChanged(nameof(NumberOfBossBattles));
            this.RaisePropertyChanged(nameof(NumberOfTrashBattles));
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

        public Task<Combat> GetOverallCombat()
        {
            List<PastCombat> combatsToLoad;
            int totalDuration;

            lock (combatAddLock)
            {
                combatsToLoad = EncounterCombats.Where(c => !c.IsCurrentCombat).OrderBy(c => c.CombatStartTime).ToList();
                totalDuration = Combats.Sum(c => c.DurationSeconds);
            }

            var overallLogs = combatsToLoad.SelectMany(c => c.LoadLogs().OrderBy(l => l.TimeStamp)).ToList();
            CombatDiskCacheManager.NormalizeLogEntities(overallLogs);
            var overallCombat = CombatIdentifier.GenerateOverallCombat(overallLogs, true, false, totalDuration);
            if (overallCombat.StartTime != DateTime.MinValue)
                overallCombat.StartTime = overallCombat.StartTime.AddSeconds(-1);
            return Task.FromResult(overallCombat);
        }

        private void OpenOrbsLeaderboard()
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = $"https://orbs-stats.com/encounterView/{Uri.EscapeDataString(Info.Name)}",
                UseShellExecute = true
            });
        }

        private void CalculateEncounterLength()
        {
            if (Combats.Count < 1)
                return;

            var start = Combats.First().StartTime;
            var end = Combats.Last().EndTime;
            var duration = end - start;
            EncounterDurationText =
                duration.TotalHours >= 1
                    ? $"{(int)duration.TotalHours}:{duration:mm\\:ss}"
                    : duration.ToString(@"mm\:ss");
        }

        private string GetPVPCombatText(Combat combat)
        {
            return
                $"Team Kills: {combat.AllLogs.Values.Count(l => l.Effect.EffectId == _7_0LogParsing.DeathCombatId && CombatLogStateBuilder.CurrentState.IsPvpOpponentAtTime(l.Target, l.TimeStamp))}\r\n" +
                $"Team Deaths: {combat.AllLogs.Values.Count(l => l.Effect.EffectId == _7_0LogParsing.DeathCombatId && !CombatLogStateBuilder.CurrentState.IsPvpOpponentAtTime(l.Target, l.TimeStamp))}";
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
