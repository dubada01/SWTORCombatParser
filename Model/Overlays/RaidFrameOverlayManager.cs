using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Avalonia;

namespace SWTORCombatParser.Model.Overlays
{
    public class RaidFrameOverlayInfo
    {
        [JsonConverter(typeof(AvaloniaPointConverter))]
        public Point Position;
        [JsonConverter(typeof(AvaloniaPointConverter))]
        public Point WidtHHeight;
        public double Rows;
        public double Columns;
        public bool Acive;
    }
    public static class RaidFrameOverlayManager
    {
        private static string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DubaTech", "SWTORCombatParser");
        private static string infoPath = Path.Combine(appDataPath, "raidframe_overlay_info.json");
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
                File.WriteAllText(infoPath, JsonConvert.SerializeObject(new Dictionary<string, RaidFrameOverlayInfo>()));
            }
        }
        public static void SetDefaults(Point position, Point widtHHeight, string characterName)
        {
            var currentDefaults = GetDefaults(characterName);
            currentDefaults = new RaidFrameOverlayInfo() { Position = position, WidtHHeight = widtHHeight, Acive = currentDefaults.Acive, Columns = currentDefaults.Columns, Rows = currentDefaults.Rows };
            SaveResults(characterName, currentDefaults);
        }
        public static void SetActiveState(bool state, string characterName)
        {
            var currentDefaults = GetDefaults(characterName);

            currentDefaults = new RaidFrameOverlayInfo() { Position = currentDefaults.Position, WidtHHeight = currentDefaults.WidtHHeight, Acive = state, Columns = currentDefaults.Columns, Rows = currentDefaults.Rows };
            SaveResults(characterName, currentDefaults);
        }
        public static void SetRowsColumns(double rows, double columns, string characterName)
        {
            var currentDefaults = GetDefaults(characterName);

            currentDefaults = new RaidFrameOverlayInfo() { Position = currentDefaults.Position, WidtHHeight = currentDefaults.WidtHHeight, Acive = currentDefaults.Acive, Columns = columns, Rows = rows };
            SaveResults(characterName, currentDefaults);
        }
        public static RaidFrameOverlayInfo GetDefaults(string characterName)
        {
            try
            {
                var currentDefaults = GetCurrentDefaults();
                if (!currentDefaults.ContainsKey(characterName))
                {
                    InitializeDefaults(characterName);
                }
                var defaultsForToon = currentDefaults[characterName];
                if (defaultsForToon.Rows == 0)
                {
                    defaultsForToon.Rows = 4;
                    defaultsForToon.Columns = 2;
                }
                return defaultsForToon;
            }
            catch (Exception)
            {
                InitializeDefaults(characterName);
                var freshDefaults = GetCurrentDefaults()[characterName];
                if (freshDefaults.Rows == 0)
                {
                    freshDefaults.Rows = 4;
                    freshDefaults.Columns = 2;
                }
                return freshDefaults;
            }

        }
        private static void SaveResults(string character, RaidFrameOverlayInfo data)
        {
            var currentDefaults = GetCurrentDefaults();
            currentDefaults[character] = data;
            File.WriteAllText(infoPath, JsonConvert.SerializeObject(currentDefaults, _settings));
        }
        private static void InitializeDefaults(string characterName)
        {
            var currentDefaults = GetCurrentDefaults();
            var defaults = new RaidFrameOverlayInfo() { Position = new Point(0, 0), WidtHHeight = new Point(400, 400), Rows = 4, Columns = 2 };
            if (characterName != "no character")
                defaults = new RaidFrameOverlayInfo() { Position = currentDefaults["no character"].Position, WidtHHeight = currentDefaults["no character"].WidtHHeight, Columns = 2, Rows = 4 };

            currentDefaults[characterName] = defaults;
            File.WriteAllText(infoPath, JsonConvert.SerializeObject(currentDefaults));
        }
        private static Dictionary<string, RaidFrameOverlayInfo> GetCurrentDefaults()
        {
            var stringInfo = File.ReadAllText(infoPath);
            var currentDefaults = JsonConvert.DeserializeObject<Dictionary<string, RaidFrameOverlayInfo>>(stringInfo);

            return currentDefaults;
        }
    }
}
