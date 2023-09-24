using Newtonsoft.Json;
using System;
using System.IO;
using System.Windows;

namespace SWTORCombatParser.Utilities
{
    public class OrbsWindowInfo
    {
        public Point TopLeft { get; set; } = new Point(0, 0);
        public double Width { get; set; } = 960;
        public double Height { get; set; } = 540;
    }
    public static class OrbsWindowManager
    {
        private static string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DubaTech", "SWTORCombatParser");
        private static string infoPath = Path.Combine(appDataPath, "main_window_position.json");
        public static void SaveWindowSizeAndPosition(OrbsWindowInfo windowInfo)
        {
            Init();
            File.WriteAllText(infoPath, JsonConvert.SerializeObject(windowInfo));
        }
        public static OrbsWindowInfo GetWindowSizeAndPosition()
        {
            Init();
            var savedData = JsonConvert.DeserializeObject<OrbsWindowInfo>(File.ReadAllText(infoPath));

            return savedData != null ? savedData : new OrbsWindowInfo();
        }
        private static void Init()
        {
            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }
            if (!File.Exists(infoPath))
            {
                var newFile = File.Create(infoPath);
                newFile.Close();
            }
        }
    }
}
