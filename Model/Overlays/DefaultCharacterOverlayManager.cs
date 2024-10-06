using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        CombatTimer
    }
    public class AvaloniaPointConverter : JsonConverter<Point>
    {
        public override void WriteJson(JsonWriter writer, Point value, JsonSerializer serializer)
        {
            // Serialize as "X, Y"
            writer.WriteValue($"{value.X}, {value.Y}");
        }

        public override Point ReadJson(JsonReader reader, Type objectType, Point existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            // Deserialize from "X, Y"
            var value = (string)reader.Value;
            var parts = value.Split(',');

            if (parts.Length == 2 && double.TryParse(parts[0], out double x) && double.TryParse(parts[1], out double y))
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
            // Serialize as "X, Y"
            writer.WriteValue($"{value.X}, {value.Y}");
        }

        public override PixelPoint ReadJson(JsonReader reader, Type objectType, PixelPoint existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            // Deserialize from "X, Y"
            var value = (string)reader.Value;
            var parts = value.Split(',');

            if (parts.Length == 2 && double.TryParse(parts[0], out double x) && double.TryParse(parts[1], out double y))
            {
                return new PixelPoint((int)x, (int)y);
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
        private static string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DubaTech", "SWTORCombatParser");
        private static string infoPath = Path.Combine(appDataPath, "character_overlay_info.json");
        public static void Init()
        {
            if (!Directory.Exists(appDataPath))
                Directory.CreateDirectory(appDataPath);

            if (!File.Exists(infoPath))
            {
                File.WriteAllText(infoPath, JsonConvert.SerializeObject(new Dictionary<string, Dictionary<string, OverlayInfo>>()));
            }
        }
        public static void SetCharacterDefaults(string type, Point position, Point widtHHeight, string characterName)
        {
            var currentDefaults = GetCharacterDefaults(characterName);
            currentDefaults[type] = new OverlayInfo() { UseAsWindow = currentDefaults[type].UseAsWindow, Position = position, WidtHHeight = widtHHeight, Acive = currentDefaults[type].Acive };
            SaveCharacterDefaults(characterName, currentDefaults);
        }
        public static void SetCharacterWindowState(string type, bool useAsWindow, string characterName)
        {
            var currentDefaults = GetCharacterDefaults(characterName);
            currentDefaults[type] = new OverlayInfo() { UseAsWindow = useAsWindow, Position = currentDefaults[type].Position, WidtHHeight = currentDefaults[type].WidtHHeight, Acive = currentDefaults[type].Acive };
            SaveCharacterDefaults(characterName, currentDefaults);
        }
        public static void SetLockedStateCharacter(bool state, string characterName)
        {
            var currentDefaults = GetCharacterDefaults(characterName);
            foreach (var overlay in currentDefaults.Keys)
            {
                currentDefaults[overlay] = new OverlayInfo() { UseAsWindow = currentDefaults[overlay].UseAsWindow, Position = currentDefaults[overlay].Position, WidtHHeight = currentDefaults[overlay].WidtHHeight, Locked = state, Acive = currentDefaults[overlay].Acive };
            }
            SaveCharacterDefaults(characterName, currentDefaults);
        }
        public static void SetActiveStateCharacter(string type, bool state, string characterName)
        {
            var currentDefaults = GetCharacterDefaults(characterName);
            if (!currentDefaults.ContainsKey(type))
            {
                currentDefaults[type] = new OverlayInfo() { Position = new Point(0, 0), WidtHHeight = new Point(100, 200), Acive = state };
            }
            var defaultModified = currentDefaults[type];
            currentDefaults[type] = new OverlayInfo() { UseAsWindow = defaultModified.UseAsWindow, Position = defaultModified.Position, WidtHHeight = defaultModified.WidtHHeight, Acive = state, Locked = defaultModified.Locked };
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
                return "";
            return currentDefaults.MaxBy(v => v.Value.Values.Count(o => o.Acive)).Key;
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
                var enumVals = EnumUtil.GetValues<OverlayType>();
                foreach (var overlayType in enumVals)
                {
                    if (!defaultsForToon.ContainsKey(overlayType.ToString()))
                        defaultsForToon[overlayType.ToString()] = new OverlayInfo() { Position = new Point(), WidtHHeight = new Point(250,300) };
                }
                return defaultsForToon;
            }
            catch (Exception)
            {
                InitializeCharacterDefaults(characterName);
                return GetCurrentCharacterDefaults()[characterName];
            }

        }
        private static void SaveCharacterDefaults(string character, Dictionary<string, OverlayInfo> data)
        {
            var currentDefaults = GetCurrentCharacterDefaults();
            currentDefaults[character] = data;
            File.WriteAllText(infoPath, JsonConvert.SerializeObject(currentDefaults));
        }
        public static void CopyFromKey(string from, string to)
        {
            var currentDefaults = GetCurrentCharacterDefaults();
            var fromDefaults = currentDefaults[from];
            if (fromDefaults == null)
            {
                InitializeCharacterDefaults(to);
            }
            else
            {
                currentDefaults[to] = currentDefaults[from];
                File.WriteAllText(infoPath, JsonConvert.SerializeObject(currentDefaults));
            }
        }
        public static void InitializeCharacterDefaults(string characterName)
        {
            var currentDefaults = GetCurrentCharacterDefaults();
            var defaults = new Dictionary<string, OverlayInfo>();
            if (characterName != "All")
            {
                var enumVals = EnumUtil.GetValues<OverlayType>();
                foreach (var overlayType in enumVals)
                {
                    defaults[overlayType.ToString()] = new OverlayInfo() { Position = new Point(), WidtHHeight = new Point(250,100) };
                }
            }
            else
            {
                defaults["Alerts"] = new OverlayInfo() { Position = new Point(), WidtHHeight = new Point(250,100) };
            }
            currentDefaults[characterName] = defaults;
            File.WriteAllText(infoPath, JsonConvert.SerializeObject(currentDefaults));
        }
        private static Dictionary<string, Dictionary<string, OverlayInfo>> GetCurrentCharacterDefaults()
        {
            var stringInfo = File.ReadAllText(infoPath);
            var typedDefaults = new Dictionary<string, Dictionary<string, OverlayInfo>>();
            var currentDefaults = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, OverlayInfo>>>(stringInfo);
            foreach (var player in currentDefaults.Keys)
            {
                var playerDefaults = currentDefaults[player];
                var playerTypedDefaults = typedDefaults[player] = new Dictionary<string, OverlayInfo>();
                foreach (var overlayType in playerDefaults.Keys)
                {
                    OverlayType typedResult;
                    var typeIsValid = Enum.TryParse(overlayType, out typedResult);
                    if (typeIsValid)
                    {
                        playerTypedDefaults[typedResult.ToString()] = playerDefaults[overlayType];
                    }
                    else
                    {
                        playerTypedDefaults[overlayType] = playerDefaults[overlayType];
                    }
                }
            }
            return typedDefaults;
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
