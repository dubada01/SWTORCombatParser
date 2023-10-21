using SWTORCombatParser.Model.Overlays;
using System;
using System.Windows.Media;

namespace SWTORCombatParser.DataStructures
{
    public enum ChallengeType
    {
        DamageOut,
        DamageIn,
        AbilityCount,
        MetricDuringPhase,
        InterruptCount,
        EffectStacks
    }
    public class Challenge
    {
        public string Source { get; set; }
        public Guid Id { get; set; }
        public string Name { get; set; }
        public ChallengeType ChallengeType { get; set; }
        public SolidColorBrush BackgroundBrush { get; set; }
        public bool IsEnabled { get; set; }
        public string ChallengeSource { get; set; }
        public string ChallengeTarget { get; set; }
        public string Value { get; set; }
        public bool UseRawValues { get; set; }
        public bool UseMaxValue { get; set; }
        public Guid PhaseId { get; set; }
        public OverlayType PhaseMetric { get; set; }
    }
}
