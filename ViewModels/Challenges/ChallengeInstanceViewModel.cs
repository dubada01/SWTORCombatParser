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
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Threading;
using ReactiveUI;

namespace SWTORCombatParser.ViewModels.Challenges
{
    public class ChallengeInstanceViewModel : ReactiveObject
    {
        private string metricTotal;
        private int _updateVersion;
        private readonly object _refreshLock = new();
        private bool _refreshRunning;
        private MetricRequest? _pendingRefresh;
        private Combat? _requestedCombat;
        private sealed record MetricRequest(Combat Combat, Challenge Challenge,
            ConcurrentDictionary<Guid, PhaseInstance> Phases, int Version);

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
        public string ResultLabel => Type switch
        {
            ChallengeType.DamageOut => SourceChallenge.UseRawValues ? "DAMAGE DEALT" : "DPS",
            ChallengeType.DamageIn => SourceChallenge.UseRawValues ? "DAMAGE TAKEN" : "DTPS",
            ChallengeType.AbilityCount => "ABILITY USES",
            ChallengeType.InterruptCount => "INTERRUPTS",
            ChallengeType.EffectStacks => SourceChallenge.UseMaxValue ? "MAXIMUM STACKS" : "CURRENT STACKS",
            _ => "PHASE METRIC"
        };
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
        public void UpdateMetrics(Combat obj, Challenge sourceChallenge)
        {
            lock (_refreshLock)
            {
                if (!ReferenceEquals(_requestedCombat, obj))
                {
                    _requestedCombat = obj;
                    Interlocked.Increment(ref _updateVersion);
                }
                _pendingRefresh = new MetricRequest(obj, sourceChallenge, _phaseOfInterest, _updateVersion);
                if (_refreshRunning)
                    return;
                _refreshRunning = true;
            }
            _ = ProcessPendingUpdates();
        }

        private async Task ProcessPendingUpdates()
        {
            // Begin before the first await so full UI readiness includes queued
            // metrics and their eventual UI publication, including live coalescing.
            using var timing = LogLoadDiagnostics.Measure("Challenge metrics update");
            while (true)
            {
                MetricRequest? request;
                lock (_refreshLock)
                {
                    request = _pendingRefresh;
                    _pendingRefresh = null;
                    if (request == null)
                    {
                        _refreshRunning = false;
                        return;
                    }
                }
                // Only one calculation runs at a time. New frames replace the
                // pending request, without invalidating every in-flight result.
                await RefreshBarViews(request.Combat, request.Challenge, request.Phases, request.Version);
            }
        }

        private async Task RefreshBarViews(Combat combatToDisplay, Challenge sourceChallenge, ConcurrentDictionary<Guid, PhaseInstance> phases, int version)
        {
            try
            {
                var values = await Task.Run(() =>
                {
                    var phaseCombat = phases != null && !phases.IsEmpty ? combatToDisplay.GetPhaseCopy(phases) : new Combat();
                    return combatToDisplay.AllEntities.Values
                        .Where(participant => participant.Name != null)
                        .Select(participant => (participant, value: ChallengeMetrics.GetValueForChallenege(sourceChallenge.ChallengeType, combatToDisplay, participant, sourceChallenge, phaseCombat)))
                        .ToList();
                });
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (version != Volatile.Read(ref _updateVersion))
                        return;
                    SourceChallenge = sourceChallenge;
                    foreach (var (participant, value) in values)
                    {
                        var metric = _metricBarsDict.GetOrAdd((participant.Name, false), _ =>
                            new ChallengeOverlayMetricInfo(challengeColor) { Player = participant, Type = Type, SizeScalar = Scale });
                        metric.Player = participant;
                        metric.Value = value;
                    }
                    MetricTotal = _metricBarsDict.Where(b => !b.Key.Item2).Sum(b => b.Value.Value).ToString("N0");
                    OrderMetricBars();
                });
            }
            catch (Exception ex)
            {
                Logging.LogError("Failed to update challenge metrics: " + ex.Message);
            }
        }
        private void OrderMetricBars()
        {
            if (!_metricBarsDict.Any())
                return;
            try
            {
                var maxValue = _metricBarsDict.Values.Where(m => double.IsFinite(m.Value)).Select(m => Math.Max(0, m.Value)).DefaultIfEmpty().Max();
                foreach (var metric in _metricBarsDict)
                {
                    if (metric.Value.Value <= 0 || !double.IsFinite(metric.Value.Value) || maxValue == 0)
                        metric.Value.RelativeLength = 0;
                    else
                        metric.Value.RelativeLength = metric.Value.Value / maxValue;
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
            lock (_refreshLock)
            {
                Interlocked.Increment(ref _updateVersion);
                _pendingRefresh = null;
                _requestedCombat = null;
            }
            MetricBars.ForEach(mb => mb.Reset());
            MetricTotal = 0d.ToString("N0");
            _phaseOfInterest = null;
        }
    }
}
