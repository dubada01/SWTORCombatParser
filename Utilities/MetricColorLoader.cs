using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Avalonia.Media;
using SWTORCombatParser.Model.Overlays;


namespace SWTORCombatParser.Utilities
{
    public static class MetricColorLoader
    {
        private static string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DubaTech", "SWTORCombatParser");
        private static string infoPath = Path.Combine(appDataPath, "metric_colors.json");
        private static string defaultsPath = Path.Combine(appDataPath, "default_metric_colors.json");

        private static object _fileLock = new object();
        public static event Action<OverlayType> OnOverlayTypeColorUpdated = delegate { };
        public static Dictionary<OverlayType, SolidColorBrush> CurrentMetricBrushDict = new Dictionary<OverlayType, SolidColorBrush>();

        public static void Init()
        {
            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }
            if(!File.Exists(infoPath))
            {
                Dictionary<string, string> metricColors = new Dictionary<string, string>();
                foreach (var metric in Enum.GetValues<OverlayType>())
                {
                    metricColors[metric.ToString()] = GetMetricDefaultColor(metric).ToString();
                }
                File.WriteAllText(infoPath,JsonConvert.SerializeObject(metricColors));
            }
            if (!File.Exists(defaultsPath))
            {
                Dictionary<string, string> metricColors = new Dictionary<string, string>();
                foreach (var metric in Enum.GetValues<OverlayType>())
                {
                    metricColors[metric.ToString()] = GetMetricDefaultColor(metric).ToString();
                }
                File.WriteAllText(defaultsPath, JsonConvert.SerializeObject(metricColors));
            }
        }
        public static void SetCurrentBrushDict()
        {
            var currentSettings = GetAllColors();
            CurrentMetricBrushDict = currentSettings.ToDictionary(kvp=>Enum.Parse<OverlayType>(kvp.Key), kvp => new SolidColorBrush(Color.Parse(kvp.Value)));
        }
        public static void SetColorForMetric(OverlayType type, string color)
        {
            var currentSettings = GetAllColors();
            currentSettings[type.ToString()] = color;
            WriteNewColors(currentSettings);
            CurrentMetricBrushDict[type] = new SolidColorBrush(Color.Parse(color));
            OnOverlayTypeColorUpdated.InvokeSafely(type);
        }
        public static Color GetDefaultColorForMetric(OverlayType type)
        {
            try
            {
                var allColors = GetDefaultColors();
                return Color.Parse(allColors[type.ToString()]);
            }
            catch
            {
                return Colors.AliceBlue;
            }
        }
        public static Color GetMetricCurrentColor(OverlayType type) 
        {
            try
            {
                var allColors = GetAllColors();
                return Color.Parse(allColors[type.ToString()]);
            }
            catch
            {
                return Colors.AliceBlue;
            }
        }
        private static Dictionary<string,string> GetAllColors()
        {
            lock(_fileLock)
            {
                var allColors = JsonConvert.DeserializeObject<Dictionary<string,string>>(File.ReadAllText(infoPath));
                foreach (var color in Enum.GetValues<OverlayType>())
                {
                    if (!allColors.ContainsKey(color.ToString()))
                    {
                        allColors[color.ToString()] = GetMetricDefaultColor(color).ToString();
                    }
                }

                return allColors;
            }
        }
        private static Dictionary<string, string> GetDefaultColors()
        {
            lock (_fileLock)
            {
                return JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(defaultsPath));
            }
        }
        private static void WriteNewColors(Dictionary<string, string> colors)
        {
            lock(_fileLock)
            {
                File.WriteAllText(infoPath, JsonConvert.SerializeObject(colors));
            }
        }
        private static Color GetMetricDefaultColor(OverlayType overlay)
        {
            switch ((OverlayType)overlay)
            {
                case OverlayType.APM:
                    return Colors.MediumPurple;
                case OverlayType.BurstDPS:
                    return Colors.Tomato;
                case OverlayType.DPS:
                case OverlayType.Damage:
                    return Colors.IndianRed;
                case OverlayType.NonEDPS:
                case OverlayType.RawDamage:
                    return Color.Parse("#d44c73");
                case OverlayType.FocusDPS:
                    return Colors.OrangeRed;
                case OverlayType.BurstEHPS:
                    return Colors.LimeGreen;
                case OverlayType.EHPS:
                case OverlayType.EffectiveHealing:
                    return ResourceFinder.GetColorFromResourceName("EHPSColor");
                case OverlayType.HPS:
                case OverlayType.RawHealing:
                    return Colors.Green;
                case OverlayType.ProvidedAbsorb:
                    return Colors.CadetBlue;
                case OverlayType.Threat:
                    return Colors.Orchid;
                case OverlayType.ThreatPerSecond:
                    return Colors.DarkOrchid;
                case OverlayType.BurstDamageTaken:
                    return Colors.DarkGoldenrod;
                case OverlayType.DamageTaken:
                    return Colors.Peru;
                case OverlayType.Mitigation:
                    return Colors.Sienna;
                case OverlayType.DamageSavedDuringCD:
                    return Colors.DarkSlateBlue;
                case OverlayType.ShieldAbsorb:
                    return Colors.SkyBlue;
                case OverlayType.DamageAvoided:
                    return Colors.DeepSkyBlue;
                case OverlayType.InterruptCount:
                    return Colors.SteelBlue;
                case OverlayType.CleanseCount:
                    return Color.Parse("#357fa1");
                case OverlayType.CleanseSpeed:
                    return Color.Parse("#5b8bd9");
                case OverlayType.HealReactionTime:
                    return ResourceFinder.GetColorFromResourceName("YellowGrayColor");
                case OverlayType.HealReactionTimeRatio:
                    return Colors.Brown;
                case OverlayType.TankHealReactionTime:
                    return Colors.RosyBrown;
                case OverlayType.CritPercent:
                    return Color.Parse("#13ad7d");
                case OverlayType.SingleTargetDPS:
                    return Color.Parse("#d834eb");
                case OverlayType.SingleTargetEHPS:
                    return Color.Parse("#00c497");
                default:
                    return Colors.Gray;
            }
        }
    }
}
