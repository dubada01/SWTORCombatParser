using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.Challenge;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace SWTORCombatParser.ViewModels.Challenges
{
    public class ChallengeInstanceViewModel : INotifyPropertyChanged
    {
        private string metricTotal;
        private Challenge _sourceChallenge;
        private SolidColorBrush challengeColor;
        private double scale = 1;

        public double Scale
        {
            get => scale; set
            {
                scale = value;
                foreach (var bar in MetricBars)
                {
                    bar.SizeScalar = scale;
                }
                OnPropertyChanged();
            }
        }
        public Guid SourceChallengeId => _sourceChallenge != null ? _sourceChallenge.Id : Guid.Empty;
        public event PropertyChangedEventHandler PropertyChanged;
        public ChallengeType Type { get; set; }
        public ConcurrentDictionary<(string, bool), ChallengeOverlayMetricInfo> _metricBarsDict { get; set; } = new ConcurrentDictionary<(string, bool), ChallengeOverlayMetricInfo>();
        public List<ChallengeOverlayMetricInfo> MetricBars { get; set; } = new List<ChallengeOverlayMetricInfo>();
        public string MetricTotal
        {
            get => metricTotal; set
            {
                metricTotal = value;
                OnPropertyChanged();
            }
        }
        public string ChallengeName { get; set; }
        public ChallengeInstanceViewModel(Challenge sourceChallenge)
        {
            _sourceChallenge = sourceChallenge;
            ChallengeName = sourceChallenge.Name;
            Type = sourceChallenge.ChallengeType;
            challengeColor = _sourceChallenge.BackgroundBrush;
        }
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        private void UpdateMetric(ChallengeType type, ChallengeOverlayMetricInfo metricToUpdate, Combat obj, Entity participant, Challenge sourceChallenge)
        {
            var value = ChallengeMetrics.GetValueForChallenege(type, obj, participant, sourceChallenge);
            metricToUpdate.Value = value;
        }
        public void UpdateMetrics(Combat obj, Challenge sourceChallenge)
        {
            _sourceChallenge = sourceChallenge;
            RefreshBarViews(obj, sourceChallenge);
            double sum = _metricBarsDict.Where(b => !b.Key.Item2).Sum(b => b.Value.Value);
            MetricTotal = sum.ToString("N0");
        }

        private void RefreshBarViews(Combat combatToDisplay, Challenge sourceChallenge)
        {

            ChallengeOverlayMetricInfo metricToUpdate;
            if (combatToDisplay.CharacterParticipants.Count == 0)
                return;
            foreach (var participant in combatToDisplay.CharacterParticipants)
            {
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

                UpdateMetric(Type, metricToUpdate, combatToDisplay, participant, sourceChallenge);
            }
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
                    ChallengeOverlayMetricInfo bar = new ChallengeOverlayMetricInfo(_sourceChallenge.BackgroundBrush);
                    var worked = _metricBarsDict.TryGetValue(keys[i], out bar);
                    if (worked)
                        listOfBars.Add(bar);
                }

                MetricBars = new List<ChallengeOverlayMetricInfo>(listOfBars.OrderByDescending(mb => mb.RelativeLength));

                OnPropertyChanged("MetricBars");

            }
            catch (Exception ex)
            {
                Logging.LogError("Failed to order overlay metrics: " + ex.Message);
            }

        }
        internal void Reset()
        {
            MetricBars.ForEach(mb => mb.Reset());
        }
    }
}
