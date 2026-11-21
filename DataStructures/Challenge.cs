using SWTORCombatParser.Model.Overlays;
using System;
using System.Linq;
using Avalonia.Media;
using Avalonia.Threading;
using Newtonsoft.Json;

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
        private Color? _explicitColor;
        private SolidColorBrush? _backgroundBrush;

        public string Source { get; set; }
        public Guid Id { get; set; }
        public bool IsBuiltIn { get; set; }
        public int BuiltInRev { get; set; }
        public string ShareId { get; set; }
        public string Name { get; set; }
        public ChallengeType ChallengeType { get; set; }
        [JsonIgnore]
        public SolidColorBrush BackgroundBrush
        {
            get => Dispatcher.UIThread.Invoke(() =>
            {
                var color = ResolveBackgroundColor();
                if (_backgroundBrush == null || _backgroundBrush.Color != color)
                    _backgroundBrush = new SolidColorBrush(color);
                return _backgroundBrush;
            });
            set
            {
                _explicitColor = value?.Color;
                _backgroundBrush = value;
            }
        }

        // Keep the existing JSON field, but never dispatch to the UI while a config lock is held.
        [JsonProperty("BackgroundBrush")]
        private string SerializedBackgroundBrush
        {
            get => ResolveBackgroundColor().ToString();
            set => _explicitColor = value == null ? Colors.White : Color.Parse(value);
        }

        private Color ResolveBackgroundColor()
        {
            if (_explicitColor.HasValue) return _explicitColor.Value;
            var rgb = BackgroundColor.Split(',').Select(byte.Parse).ToArray();
            return Color.FromRgb(rgb[0], rgb[1], rgb[2]);
        }

        public string BackgroundColor { get; set; } = "245,245,245";
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
