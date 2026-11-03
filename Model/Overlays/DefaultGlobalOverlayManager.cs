using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Avalonia;

namespace SWTORCombatParser.Model.Overlays
{
    public static class DefaultGlobalOverlays
    {
        private static string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DubaTech", "SWTORCombatParser");
        private static string infoPath = Path.Combine(appDataPath, "global_overlay_info.json");
        // Serialize with invariant culture
        private static JsonSerializerSettings _settings = new JsonSerializerSettings
        {
            Culture = CultureInfo.InvariantCulture
        };
        public static void Init()
        {
            if (!Directory.Exists(appDataPath))
                Directory.CreateDirectory(appDataPath);

            if (!File.Exists(infoPath))
            {
                File.WriteAllText(infoPath, JsonConvert.SerializeObject(new Dictionary<string, OverlayInfo>()));
            }
        }
        public static void SetDefault(string type, Point position, Point widtHHeight)
        {
            var currentDefaults = ReadDefaultsFromFile();
            if (!currentDefaults.ContainsKey(type))
            {
                currentDefaults[type] = new OverlayInfo() { Position = position, WidtHHeight = widtHHeight };
            }
            var defaultModified = currentDefaults[type];
            currentDefaults[type] = new OverlayInfo() { Position = position, WidtHHeight = widtHHeight, Acive = defaultModified.Acive };
            SaveDefaults(currentDefaults);
        }
        public static void SetActive(string type, bool state)
        {
            var currentDefaults = ReadDefaultsFromFile();
            if (!currentDefaults.ContainsKey(type))
            {
                currentDefaults[type] = new OverlayInfo() { Position = new Point(0, 0), WidtHHeight = new Point(100, 200), Acive = state };
            }
            var defaultModified = currentDefaults[type];
            currentDefaults[type] = new OverlayInfo() { Position = defaultModified.Position, WidtHHeight = defaultModified.WidtHHeight, Acive = state, Locked = defaultModified.Locked };
            SaveDefaults(currentDefaults);
        }
        public static OverlayInfo GetOverlayInfoForType(string type)
        {
            var currentInfos = ReadDefaultsFromFile();
            if (!currentInfos.ContainsKey(type))
                InitDefaults(type);
            return ReadDefaultsFromFile()[type];

        }
        private static void SaveDefaults(Dictionary<string, OverlayInfo> data)
        {
            File.WriteAllText(infoPath, JsonConvert.SerializeObject(data,_settings));
        }
        private static void InitDefaults(string type)
        {
            var currentDefaults = ReadDefaultsFromFile();
            currentDefaults[type] = new OverlayInfo() { Position = new Point(), WidtHHeight = new Point(250,100), Acive = false };
            SaveDefaults(currentDefaults);
        }
        private static Dictionary<string, OverlayInfo> ReadDefaultsFromFile()
        {
            var stringInfo = File.ReadAllText(infoPath);
            var currentDefaults = JsonConvert.DeserializeObject<Dictionary<string, OverlayInfo>>(stringInfo);
            return currentDefaults;
        }
    }
}
