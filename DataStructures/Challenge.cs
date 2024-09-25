using SWTORCombatParser.Model.Overlays;
using System;
using System.Linq;
using Avalonia.Media;

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
        private SolidColorBrush backgroundBrush;

        public string Source { get; set; }
        public Guid Id { get; set; }
        public bool IsBuiltIn { get; set; }
        public int BuiltInRev { get; set; }
        public string ShareId { get; set; }
        public string Name { get; set; }
        public ChallengeType ChallengeType { get; set; }
        public SolidColorBrush BackgroundBrush
        {
            get
            {
                if(backgroundBrush != null)
                    return backgroundBrush;
                var splitColor = BackgroundColor.Split(',').Select(v=>byte.Parse(v.Trim())).ToList();
                return new SolidColorBrush(Color.FromRgb(splitColor[0], splitColor[1], splitColor[2]));
            }
            set => backgroundBrush = value;
        }
        public string BackgroundColor { get; set; }
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
