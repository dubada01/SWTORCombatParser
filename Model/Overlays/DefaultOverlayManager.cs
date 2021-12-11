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
        BurstEHPS,
        Tank_Shielding,
        Shielding,
        Mitigation,
        ShieldAbsorb,
        DamageAvoided,
        Threat,
        DamageTaken,
        BurstDamageTaken,
        CompanionDPS,
        CompanionEHPS,
        PercentOfFightBelowFullHP,
        InterruptCount
    }
    public class DefaultOverlayInfo
    {
        public Point Position;
        public Point WidtHHeight;
        public bool Acive;
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
        public static void SetActiveState(OverlayType type, bool state, string characterName)
        {
            var currentDefaults = GetDefaults(characterName);
            if (!currentDefaults.ContainsKey(type))
            {
                currentDefaults[type] = new DefaultOverlayInfo() { Position = new Point(0,0), WidtHHeight = new Point(100,200), Acive=state };
            }
            var defaultModified = currentDefaults[type];
            currentDefaults[type] = new DefaultOverlayInfo() { Position = defaultModified.Position, WidtHHeight = defaultModified.WidtHHeight, Acive=state };
            SaveResults(characterName, currentDefaults);
        }
        public static Dictionary<OverlayType,DefaultOverlayInfo> GetDefaults(string characterName)
        {
            var stringInfo = File.ReadAllText(infoPath);
            try
            {
                var currentDefaults = JsonConvert.DeserializeObject<Dictionary<string,Dictionary<OverlayType, DefaultOverlayInfo>>>(stringInfo);
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
                var resetDefaults = File.ReadAllText(infoPath);
                return JsonConvert.DeserializeObject<Dictionary<string,Dictionary<OverlayType, DefaultOverlayInfo>>>(resetDefaults)[characterName];
            }

        }
        private static void SaveResults(string character, Dictionary<OverlayType, DefaultOverlayInfo> data)
        {
            var stringInfo = File.ReadAllText(infoPath);
            var currentDefaults = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<OverlayType, DefaultOverlayInfo>>>(stringInfo);
            currentDefaults[character] = data;
            File.WriteAllText(infoPath, JsonConvert.SerializeObject(currentDefaults));
        }
        private static void InitializeDefaults(string characterName)
        {
            var stringInfo = File.ReadAllText(infoPath);
            var currentDefaults = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<OverlayType, DefaultOverlayInfo>>>(stringInfo);
            
            var defaults = new Dictionary<OverlayType, DefaultOverlayInfo>();
            var enumVals = EnumUtil.GetValues<OverlayType>();
            foreach(var overlayType in enumVals)
            {
                defaults[overlayType] = new DefaultOverlayInfo() { Position = new Point(), WidtHHeight = new Point() { X = 250, Y = 100 } };
            }
            currentDefaults[characterName] = defaults;
            File.WriteAllText(infoPath, JsonConvert.SerializeObject(currentDefaults));
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
