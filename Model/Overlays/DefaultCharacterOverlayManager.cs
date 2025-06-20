using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Avalonia;

namespace SWTORCombatParser.Model.Overlays
{
    public enum OverlayType
    {
        None,
        APM,
        DPS,
        BurstDPS,
        FocusDPS,
        HPS,
        EHPS,
        HealReactionTime,
        HealReactionTimeRatio,
        TankHealReactionTime,
        BurstEHPS,
        ProvidedAbsorb,
        Mitigation,
        DamageSavedDuringCD,
        ShieldAbsorb,
        AbsorbProvided,
        DamageAvoided,
        Threat,
        DamageTaken,
        BurstDamageTaken,
        InterruptCount,
        ThreatPerSecond,
        NonEDPS,
        Damage,
        RawDamage,
        EffectiveHealing,
        RawHealing,
        CritPercent,
        CustomVariable,
        SingleTargetDPS,
        SingleTargetEHPS,
        CleanseCount,
        CleanseSpeed,
        CombatTimer,
        InstantaneousDPS,
        InstantaneousEHPS,
        InstantaneousDPTS,
        FluffDPS,
        EHPSNoShielding
    }

    public class AvaloniaPointConverter : JsonConverter<Point>
    {
        public override void WriteJson(JsonWriter writer, Point value, JsonSerializer serializer)
        {
            writer.WriteValue($"{value.X.ToString(CultureInfo.InvariantCulture)}, {value.Y.ToString(CultureInfo.InvariantCulture)}");
        }

        public override Point ReadJson(JsonReader reader, Type objectType, Point existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var value = ((string)reader.Value).Trim();
            var parts = value.Contains(" ")
                ? value.Split(new[] { ", " }, StringSplitOptions.None)
                : value.Split(new[] { "," }, StringSplitOptions.None);

            if (parts.Length == 2 &&
                double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
                double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
            {
                return new Point(x, y);
            }

            throw new JsonSerializationException("Invalid format for Avalonia Point");
        }
    }

    public class AvaloniaPixelPointConverter : JsonConverter<PixelPoint>
    {
        public override void WriteJson(JsonWriter writer, PixelPoint value, JsonSerializer serializer)
        {
            writer.WriteValue($"{value.X}, {value.Y}");
        }

        public override PixelPoint ReadJson(JsonReader reader, Type objectType, PixelPoint existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var value = ((string)reader.Value);
            var parts = value.Split(',');

            if (parts.Length == 2 && int.TryParse(parts[0], out var x) && int.TryParse(parts[1], out var y))
            {
                return new PixelPoint(x, y);
            }

            throw new JsonSerializationException("Invalid format for Avalonia Pixel Point");
        }
    }

    public class OverlayInfo
    {
        [JsonConverter(typeof(AvaloniaPointConverter))]
        public Point Position;
        [JsonConverter(typeof(AvaloniaPointConverter))]
        public Point WidtHHeight;
        public bool Acive;
        public bool Locked;
        public bool UseAsWindow;
    }

    public static class DefaultCharacterOverlays
    {
        private static readonly object _fileLock = new object();
        private static string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DubaTech", "SWTORCombatParser");
        private static string infoPath = Path.Combine(appDataPath, "character_overlay_info.json");
        private static JsonSerializerSettings _settings = new JsonSerializerSettings
        {
            Culture = CultureInfo.InvariantCulture
        };

        public static void Init()
        {
            lock (_fileLock)
            {
                if (!Directory.Exists(appDataPath))
                    Directory.CreateDirectory(appDataPath);

                if (!File.Exists(infoPath))
                {
                    File.WriteAllText(infoPath, JsonConvert.SerializeObject(new Dictionary<string, Dictionary<string, OverlayInfo>>(), _settings));
                }
            }
        }

        public static void SetCharacterDefaults(string type, Point position, Point widtHHeight, string characterName)
        {
            var currentDefaults = GetCharacterDefaults(characterName);
            var useAsWindow = type == "RaidFrame";
            var active = true;

            if (currentDefaults.ContainsKey(type))
            {
                useAsWindow = currentDefaults[type].UseAsWindow;
                active = currentDefaults[type].Acive;
            }

            currentDefaults[type] = new OverlayInfo { UseAsWindow = useAsWindow, Position = position, WidtHHeight = widtHHeight, Acive = active };
            SaveCharacterDefaults(characterName, currentDefaults);
        }

        public static void SetCharacterWindowState(string type, bool useAsWindow, string characterName)
        {
            var currentDefaults = GetCharacterDefaults(characterName);
            currentDefaults[type] = new OverlayInfo
            {
                UseAsWindow = useAsWindow,
                Position = currentDefaults[type].Position,
                WidtHHeight = currentDefaults[type].WidtHHeight,
                Acive = currentDefaults[type].Acive
            };
            SaveCharacterDefaults(characterName, currentDefaults);
        }

        public static void SetLockedStateCharacter(bool state, string characterName)
        {
            var currentDefaults = GetCharacterDefaults(characterName);
            foreach (var overlay in currentDefaults.Keys.ToList())
            {
                var info = currentDefaults[overlay];
                currentDefaults[overlay] = new OverlayInfo
                {
                    UseAsWindow = info.UseAsWindow,
                    Position = info.Position,
                    WidtHHeight = info.WidtHHeight,
                    Locked = state,
                    Acive = info.Acive
                };
            }
            SaveCharacterDefaults(characterName, currentDefaults);
        }

        public static void SetActiveStateCharacter(string type, bool state, string characterName)
        {
            var currentDefaults = GetCharacterDefaults(characterName);
            if (!currentDefaults.ContainsKey(type))
            {
                currentDefaults[type] = new OverlayInfo { Position = new Point(0, 0), WidtHHeight = new Point(100, 200), Acive = state };
            }

            var defaultModified = currentDefaults[type];
            currentDefaults[type] = new OverlayInfo
            {
                UseAsWindow = defaultModified.UseAsWindow,
                Position = defaultModified.Position,
                WidtHHeight = defaultModified.WidtHHeight,
                Acive = state,
                Locked = defaultModified.Locked
            };
            SaveCharacterDefaults(characterName, currentDefaults);
        }

        public static bool DoesKeyExist(string key)
        {
            var currentDefaults = GetCurrentCharacterDefaults();
            return currentDefaults.ContainsKey(key);
        }

        public static string GetMostUsedLayout()
        {
            var currentDefaults = GetCurrentCharacterDefaults();
            if (!currentDefaults.Any())
                return string.Empty;

            return currentDefaults
                .MaxBy(v => v.Value.Values.Count(o => o.Acive))
                .Key;
        }

        public static Dictionary<string, OverlayInfo> GetCharacterDefaults(string characterName)
        {
            try
            {
                var currentDefaults = GetCurrentCharacterDefaults();

                if (!currentDefaults.ContainsKey(characterName))
                {
                    if (characterName.Contains("_") && currentDefaults.ContainsKey(characterName.Split('_')[0]))
                    {
                        CopyFromKey(characterName.Split('_')[0], characterName);
                        currentDefaults = GetCurrentCharacterDefaults();
                    }
                    else
                    {
                        InitializeCharacterDefaults(characterName);
                        currentDefaults = GetCurrentCharacterDefaults();
                    }
                }

                var defaultsForToon = currentDefaults[characterName];
                foreach (var overlayType in EnumUtil.GetValues<OverlayType>())
                {
                    if (!defaultsForToon.ContainsKey(overlayType.ToString()))
                    {
                        defaultsForToon[overlayType.ToString()] = new OverlayInfo { Position = new Point(), WidtHHeight = new Point(250, 300) };
                    }
                }

                return defaultsForToon;
            }
            catch
            {
                InitializeCharacterDefaults(characterName);
                return GetCurrentCharacterDefaults()[characterName];
            }
        }

        private static void SaveCharacterDefaults(string character, Dictionary<string, OverlayInfo> data)
        {
            lock (_fileLock)
            {
                var currentDefaults = GetCurrentCharacterDefaults();
                currentDefaults[character] = data;
                File.WriteAllText(infoPath, JsonConvert.SerializeObject(currentDefaults, _settings));
            }
        }

        public static void CopyFromKey(string from, string to)
        {
            lock (_fileLock)
            {
                var currentDefaults = GetCurrentCharacterDefaults();
                if (currentDefaults.TryGetValue(from, out var fromDefaults) && fromDefaults != null)
                {
                    currentDefaults[to] = new Dictionary<string, OverlayInfo>(fromDefaults);
                }
                else
                {
                    InitializeCharacterDefaults(to);
                    currentDefaults = GetCurrentCharacterDefaults();
                }

                File.WriteAllText(infoPath, JsonConvert.SerializeObject(currentDefaults, _settings));
            }
        }

        public static void InitializeCharacterDefaults(string characterName)
        {
            lock (_fileLock)
            {
                var currentDefaults = GetCurrentCharacterDefaults();
                var defaults = new Dictionary<string, OverlayInfo>();

                if (characterName != "All")
                {
                    foreach (var overlayType in EnumUtil.GetValues<OverlayType>())
                    {
                        defaults[overlayType.ToString()] = new OverlayInfo { Position = new Point(), WidtHHeight = new Point(250, 100) };
                    }
                }
                else
                {
                    defaults["Alerts"] = new OverlayInfo { Position = new Point(), WidtHHeight = new Point(250, 100) };
                }

                currentDefaults[characterName] = defaults;
                File.WriteAllText(infoPath, JsonConvert.SerializeObject(currentDefaults, _settings));
            }
        }

        private static Dictionary<string, Dictionary<string, OverlayInfo>> GetCurrentCharacterDefaults()
        {
            lock (_fileLock)
            {
                var stringInfo = File.ReadAllText(infoPath);
                var currentDefaults = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, OverlayInfo>>>(stringInfo) 
                    ?? new Dictionary<string, Dictionary<string, OverlayInfo>>();

                var typedDefaults = new Dictionary<string, Dictionary<string, OverlayInfo>>();
                foreach (var player in currentDefaults.Keys)
                {
                    var playerDefaults = currentDefaults[player];
                    var playerTypedDefaults = new Dictionary<string, OverlayInfo>();

                    foreach (var overlayType in playerDefaults.Keys)
                    {
                        if (Enum.TryParse<OverlayType>(overlayType, out var typed))
                        {
                            playerTypedDefaults[typed.ToString()] = playerDefaults[overlayType];
                        }
                        else
                        {
                            playerTypedDefaults[overlayType] = playerDefaults[overlayType];
                        }
                    }

                    typedDefaults[player] = playerTypedDefaults;
                }

                return typedDefaults;
            }
        }
    }

    public static class EnumUtil
    {
        public static IEnumerable<T> GetValues<T>()
        {
            return Enum.GetValues(typeof(T)).Cast<T>();
        }
    }
}
