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
    public class RaidFrameOverlayInfo
    {
        public Point Position;
        public Point WidtHHeight;
        public bool Acive;
    }
    public static class RaidFrameOverlayManager
    {
        private static string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DubaTech", "SWTORCombatParser");
        private static string infoPath = Path.Combine(appDataPath, "raidframe_overlay_info.json");
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
            currentDefaults = new RaidFrameOverlayInfo() { Position = position, WidtHHeight = widtHHeight, Acive = currentDefaults.Acive };
            SaveResults(characterName, currentDefaults);
        }
        public static void SetActiveState(bool state, string characterName)
        {
            var currentDefaults = GetDefaults(characterName);

            currentDefaults = new RaidFrameOverlayInfo() { Position = currentDefaults.Position, WidtHHeight = currentDefaults.WidtHHeight, Acive=state };
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

                return defaultsForToon;
            }
            catch(Exception e)
            {
                InitializeDefaults(characterName);
                return GetCurrentDefaults()[characterName];
            }

        }
        private static void SaveResults(string character, RaidFrameOverlayInfo data)
        {
            var currentDefaults = GetCurrentDefaults();
            currentDefaults[character] = data;
            File.WriteAllText(infoPath, JsonConvert.SerializeObject(currentDefaults));
        }
        private static void InitializeDefaults(string characterName)
        {
            var currentDefaults = GetCurrentDefaults();
            var defaults = new RaidFrameOverlayInfo() { Position = new Point(0, 0), WidtHHeight = new Point(400, 400) };
            if(characterName != "no character")
                defaults = new RaidFrameOverlayInfo() { Position = currentDefaults["no character"].Position, WidtHHeight = currentDefaults["no character"].WidtHHeight };
 
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
