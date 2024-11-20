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
    public class SolidColorBrushConverter : JsonConverter<SolidColorBrush>
    {
        public override void WriteJson(JsonWriter writer, SolidColorBrush value, JsonSerializer serializer)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                // Serialize the color as a string (e.g., "#FF0000FF" for blue)
                writer.WriteValue(value.Color.ToString());
            });
        }

        public override SolidColorBrush ReadJson(JsonReader reader, Type objectType, SolidColorBrush existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            // Deserialize the color from a hex string
            var colorString = (string)reader.Value;
            if(colorString == null)
                return Dispatcher.UIThread.Invoke(() => new SolidColorBrush(Colors.White));
            var color = Color.Parse(colorString);
            var colorBrush =  Dispatcher.UIThread.Invoke(() => new SolidColorBrush(color));
            return colorBrush;
        }
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
        [JsonConverter(typeof(SolidColorBrushConverter))]
        public SolidColorBrush BackgroundBrush
        {
            get
            {
                var returnBrush = Dispatcher.UIThread.Invoke(() =>
                {
                    if(backgroundBrush != null)
                        return backgroundBrush;
                    var splitColor = BackgroundColor.Split(',').Select(v=>byte.Parse(v.Trim())).ToList();
                    return new SolidColorBrush(Color.FromRgb(splitColor[0], splitColor[1], splitColor[2]));
                });
                return returnBrush;
            }
            set => backgroundBrush = value;
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
