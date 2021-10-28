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
        DPS,
        FocusDPS,
        HPS,
        EHPS,
        Tank_Shielding,
        Shielding,
        Mitigation,
        ShieldAbsorb,
        DamageAvoided,
        Threat,
        DamageTaken,
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
        private static string infoPath = Path.Combine(appDataPath, "overlay_info.json");
        public static void SetDefaults(OverlayType type, Point position, Point widtHHeight)
        {
            if (!Directory.Exists(appDataPath))
                Directory.CreateDirectory(appDataPath);
            var currentDefaults = GetDefaults();
            currentDefaults[type] = new DefaultOverlayInfo() { Position = position, WidtHHeight = widtHHeight, Acive = currentDefaults[type].Acive };
            File.WriteAllText(infoPath, JsonConvert.SerializeObject(currentDefaults));

        }
        public static void SetActiveState(OverlayType type, bool state)
        {
            var currentDefaults = GetDefaults();
            if (!currentDefaults.ContainsKey(type))
            {
                currentDefaults[type] = new DefaultOverlayInfo() { Position = new Point(0,0), WidtHHeight = new Point(100,50), Acive=state };
            }
            var defaultModified = currentDefaults[type];
            currentDefaults[type] = new DefaultOverlayInfo() { Position = defaultModified.Position, WidtHHeight = defaultModified.WidtHHeight, Acive=state };
            File.WriteAllText(infoPath, JsonConvert.SerializeObject(currentDefaults));
        }
        public static Dictionary<OverlayType,DefaultOverlayInfo> GetDefaults()
        {
            if (!Directory.Exists(appDataPath))
                Directory.CreateDirectory(appDataPath);
            if (!File.Exists(infoPath))
            {
                InitializeDefaults();
            }
            var stringInfo = File.ReadAllText(infoPath);
            try
            {
                var currentDefaults = JsonConvert.DeserializeObject<Dictionary<OverlayType, DefaultOverlayInfo>>(stringInfo);
                var enumVals = EnumUtil.GetValues<OverlayType>();
                foreach (var overlayType in enumVals)
                {
                    if(!currentDefaults.ContainsKey(overlayType))
                        currentDefaults[overlayType] = new DefaultOverlayInfo() { Position = new Point(), WidtHHeight = new Point() { X = 250, Y = 100 } };
                }
                return currentDefaults;
            }
            catch(Exception e)
            {
                InitializeDefaults();
                var resetDefaults = File.ReadAllText(infoPath);
                return JsonConvert.DeserializeObject<Dictionary<OverlayType, DefaultOverlayInfo>>(resetDefaults);
            }

        }
        private static void InitializeDefaults()
        {
            var defaults = new Dictionary<OverlayType, DefaultOverlayInfo>();
            var enumVals = EnumUtil.GetValues<OverlayType>();
            foreach(var overlayType in enumVals)
            {
                defaults[overlayType] = new DefaultOverlayInfo() { Position = new Point(), WidtHHeight = new Point() { X = 250, Y = 100 } };
            }
            File.Create(infoPath).Close();
            File.WriteAllText(infoPath, JsonConvert.SerializeObject(defaults));
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
