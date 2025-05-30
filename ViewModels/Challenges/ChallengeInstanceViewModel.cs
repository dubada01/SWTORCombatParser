using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.Challenge;
using SWTORCombatParser.Model.Phases;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media;
using ReactiveUI;

namespace SWTORCombatParser.ViewModels.Challenges
{
    public class ChallengeInstanceViewModel : ReactiveObject
    {
        private string metricTotal;

        private SolidColorBrush challengeColor;
        private double scale = 1;
        private ConcurrentDictionary<Guid,PhaseInstance> _phaseOfInterest = new ConcurrentDictionary<Guid,PhaseInstance>();
                private double defaultValueWidth = 60;
        public double Scale
        {
            get => scale; set
            {
                this.RaiseAndSetIfChanged(ref scale, value);
                foreach (var bar in MetricBars)
                {
                    bar.SizeScalar = scale;
                }
            }
        }
        public Challenge SourceChallenge { get; set; }
        public Guid SourceChallengeId => SourceChallenge != null ? SourceChallenge.Id : Guid.Empty;
        public event PropertyChangedEventHandler PropertyChanged;
        public ChallengeType Type { get; set; }
        public ConcurrentDictionary<(string, bool), ChallengeOverlayMetricInfo> _metricBarsDict { get; set; } = new ConcurrentDictionary<(string, bool), ChallengeOverlayMetricInfo>();
        public List<ChallengeOverlayMetricInfo> MetricBars { get; set; } = new List<ChallengeOverlayMetricInfo>();
        public string MetricTotal
        {
            get => metricTotal; set
            {
                this.RaiseAndSetIfChanged(ref metricTotal, value);
            }
        }
        public string ChallengeName { get; set; }
        public ChallengeInstanceViewModel(Challenge sourceChallenge)
        {
            SourceChallenge = sourceChallenge;
            ChallengeName = sourceChallenge.Name;
            Type = sourceChallenge.ChallengeType;
            challengeColor = SourceChallenge.BackgroundBrush;
        }
        public void UpdatePhase(ConcurrentDictionary<Guid,PhaseInstance> phases)
        {
            _phaseOfInterest = phases;
        }
        private void UpdateMetric(ChallengeType type, ChallengeOverlayMetricInfo metricToUpdate, Combat obj, Entity participant, Challenge sourceChallenge, Combat phaseCombat)
        {
            var value = ChallengeMetrics.GetValueForChallenege(type, obj, participant, sourceChallenge, phaseCombat);
            metricToUpdate.Value = value;
        }
        public void UpdateMetrics(Combat obj, Challenge sourceChallenge)
        {
            SourceChallenge = sourceChallenge;
            RefreshBarViews(obj, sourceChallenge);
            double sum = _metricBarsDict.Where(b => !b.Key.Item2).Sum(b => b.Value.Value);
            MetricTotal = sum.ToString("N0");
        }

        private async void RefreshBarViews(Combat combatToDisplay, Challenge sourceChallenge)
        {
            if (combatToDisplay.AllEntities.Count == 0)
                return;
            Combat phaseCombat = new Combat();
            if (_phaseOfInterest != null && _phaseOfInterest.Any())
                phaseCombat = combatToDisplay.GetPhaseCopy(_phaseOfInterest);
            List<Task> metricUpdateTasks = new List<Task>();
            foreach (var participant in combatToDisplay.AllEntities)
            {
                ChallengeOverlayMetricInfo metricToUpdate;
                if (_metricBarsDict.Any(m => m.Key.Item1 == participant.Name))
                {
                    metricToUpdate = _metricBarsDict.FirstOrDefault(mb => mb.Key.Item1 == participant.Name && !mb.Key.Item2).Value;
                    if (metricToUpdate == null)
                        continue;
                }
                else
                {
                    metricToUpdate = new ChallengeOverlayMetricInfo(challengeColor) { Player = participant, Type = Type, SizeScalar = Scale };
                    _metricBarsDict.TryAdd((participant.Name, false), metricToUpdate);
                }
                metricUpdateTasks.Add(Task.Run(() =>
                {
                    UpdateMetric(Type, metricToUpdate, combatToDisplay, participant, sourceChallenge, phaseCombat);
                }));

            }
            await Task.WhenAll(metricUpdateTasks);
            OrderMetricBars();
        }
        private void OrderMetricBars()
        {
            if (!_metricBarsDict.Any())
                return;
            try
            {
                var maxValue = _metricBarsDict.MaxBy(m => double.Parse(m.Value.TotalValue, CultureInfo.InvariantCulture)).Value.TotalValue;
                foreach (var metric in _metricBarsDict)
                {
                    if (double.Parse(metric.Value.TotalValue, CultureInfo.InvariantCulture) == 0 || double.IsInfinity(metric.Value.Value) || double.IsNaN(metric.Value.Value))
                        metric.Value.RelativeLength = 0;
                    else
                        metric.Value.RelativeLength = double.Parse(maxValue, CultureInfo.InvariantCulture) == 0 ? 0 : (double.Parse(metric.Value.TotalValue, CultureInfo.InvariantCulture) / double.Parse(maxValue, CultureInfo.InvariantCulture));
                }

                var listOfBars = new List<ChallengeOverlayMetricInfo>();
                var keys = _metricBarsDict.Keys.ToList();
                for (var i = 0; i < keys.Count; i++)
                {
                    ChallengeOverlayMetricInfo bar = new ChallengeOverlayMetricInfo(SourceChallenge.BackgroundBrush);
                    var worked = _metricBarsDict.TryGetValue(keys[i], out bar);
                    if (worked)
                        listOfBars.Add(bar);
                }

                MetricBars = new List<ChallengeOverlayMetricInfo>(listOfBars.Where(b => b.Value != 0).OrderByDescending(mb => mb.RelativeLength));

                this.RaisePropertyChanged(nameof(MetricBars));

            }
            catch (Exception ex)
            {
                Logging.LogError("Failed to order overlay metrics: " + ex.Message);
            }

        }
        internal void Reset()
        {
            MetricBars.ForEach(mb => mb.Reset());
            _phaseOfInterest = null;
        }
    }
}
