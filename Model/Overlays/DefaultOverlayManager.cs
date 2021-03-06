using Newtonsoft.Json;
using SWTORCombatParser.ViewModels.Overlays;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

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
        TankHealReactionTime,
        BurstEHPS,
        Shielding,
        Mitigation,
        ShieldAbsorb,
        DamageAvoided,
        Threat,
        DamageTaken,
        BurstDamageTaken,
        InterruptCount
    }
    public class DefaultOverlayInfo
    {
        public Point Position;
        public Point WidtHHeight;
        public bool Acive;
        public bool Locked;
    }
    public static class DefaultOverlayManager
    {
        private static string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "DubaTech", "SWTORCombatParser");
        private static string infoPath = Path.Combine(appDataPath, "character_overlay_info.json");
        public static void Init()
        {
            if (!Directory.Exists(appDataPath))
                Directory.CreateDirectory(appDataPath);
            if (!File.Exists(infoPath))
            {
                File.WriteAllText(infoPath, JsonConvert.SerializeObject(new Dictionary<string, Dictionary<OverlayType, DefaultOverlayInfo>>()));
            }
        }
        public static void SetDefaults(OverlayType type, Point position, Point widtHHeight, string characterName)
        {
            var currentDefaults = GetDefaults(characterName);
            currentDefaults[type] = new DefaultOverlayInfo() { Position = position, WidtHHeight = widtHHeight, Acive = currentDefaults[type].Acive };
            SaveResults(characterName, currentDefaults);
        }
        public static void SetLockedState(bool state, string characterName)
        {
            var currentDefaults = GetDefaults(characterName);
            foreach(var overlay in currentDefaults.Keys)
            {
                currentDefaults[overlay] = new DefaultOverlayInfo() { Position = currentDefaults[overlay].Position, WidtHHeight = currentDefaults[overlay].WidtHHeight, Locked=state,  Acive = currentDefaults[overlay].Acive };
            }
            SaveResults(characterName, currentDefaults);
        }
        public static void SetActiveState(OverlayType type, bool state, string characterName)
        {
            var currentDefaults = GetDefaults(characterName);
            if (!currentDefaults.ContainsKey(type))
            {
                currentDefaults[type] = new DefaultOverlayInfo() { Position = new Point(0, 0), WidtHHeight = new Point(100, 200), Acive=state };
            }
            var defaultModified = currentDefaults[type];
            currentDefaults[type] = new DefaultOverlayInfo() { Position = defaultModified.Position, WidtHHeight = defaultModified.WidtHHeight, Acive=state, Locked = defaultModified.Locked };
            SaveResults(characterName, currentDefaults);
        }
        public static Dictionary<OverlayType,DefaultOverlayInfo> GetDefaults(string characterName)
        {
            try
            {
                var currentDefaults = GetCurrentDefaults();
                if (!currentDefaults.ContainsKey(characterName))
                {
                    InitializeDefaults(characterName);
                }
                var defaultsForToon = currentDefaults[characterName];
                var enumVals = EnumUtil.GetValues<OverlayType>();
                foreach (var overlayType in enumVals)
                {
                    if(!defaultsForToon.ContainsKey(overlayType))
                        defaultsForToon[overlayType] = new DefaultOverlayInfo() { Position = new Point(), WidtHHeight = new Point() { X = 250, Y = 300 } };
                }
                return defaultsForToon;
            }
            catch(Exception e)
            {
                InitializeDefaults(characterName);
                return GetCurrentDefaults()[characterName];
            }

        }
        private static void SaveResults(string character, Dictionary<OverlayType, DefaultOverlayInfo> data)
        {
            var currentDefaults = GetCurrentDefaults();
            currentDefaults[character] = data;
            File.WriteAllText(infoPath, JsonConvert.SerializeObject(currentDefaults));
        }
        private static void InitializeDefaults(string characterName)
        {
            var currentDefaults = GetCurrentDefaults();
            var defaults = new Dictionary<OverlayType, DefaultOverlayInfo>();
            var enumVals = EnumUtil.GetValues<OverlayType>();
            foreach(var overlayType in enumVals)
            {
                defaults[overlayType] = new DefaultOverlayInfo() { Position = new Point(), WidtHHeight = new Point() { X = 250, Y = 100 } };
            }
            currentDefaults[characterName] = defaults;
            File.WriteAllText(infoPath, JsonConvert.SerializeObject(currentDefaults));
        }
        private static Dictionary<string, Dictionary<OverlayType, DefaultOverlayInfo>> GetCurrentDefaults()
        {
            var stringInfo = File.ReadAllText(infoPath);
            var typedDefaults = new Dictionary<string, Dictionary<OverlayType, DefaultOverlayInfo>>();
            var currentDefaults = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, DefaultOverlayInfo>>>(stringInfo);
            foreach(var player in currentDefaults.Keys)
            {
                var playerDefaults = currentDefaults[player];
                var playerTypedDefaults = typedDefaults[player] = new Dictionary<OverlayType, DefaultOverlayInfo>();
                foreach(var overlayType in playerDefaults.Keys)
                {
                    OverlayType typedResult;
                    var typeIsValid = Enum.TryParse<OverlayType>(overlayType, out typedResult);
                    if (typeIsValid)
                    {
                        playerTypedDefaults[typedResult] = playerDefaults[overlayType];
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
