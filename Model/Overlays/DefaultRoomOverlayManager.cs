using Newtonsoft.Json;
using System;
using System.IO;
using Avalonia;

namespace SWTORCombatParser.Model.Overlays
{
    public class RoomOverlayManager
    {
        [JsonConverter(typeof(AvaloniaPointConverter))]
        public Point Position;
        [JsonConverter(typeof(AvaloniaPointConverter))]
        public Point WidtHHeight;
        public bool Acive;
        public bool ViewExtraData;
        public bool Locked;
    }
    public class DefaultRoomOverlayManager
    {
        private static string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DubaTech", "SWTORCombatParser");
        private static string infoPath = Path.Combine(appDataPath, "room_overlay_info.json");
        public static void Init()
        {
            if (!Directory.Exists(appDataPath))
                Directory.CreateDirectory(appDataPath);
            if (!File.Exists(infoPath))
            {
                File.WriteAllText(infoPath, JsonConvert.SerializeObject(new RoomOverlayManager() { WidtHHeight = new Point(100, 100), Position = new Point(100, 100), Acive = false, ViewExtraData = true }));
            }
        }
        internal static void SetDefaults(Point point1, Point point2)
        {
            var currentdefaults = GetDefaults();
            currentdefaults.Position = point1;
            currentdefaults.WidtHHeight = point2;
            SaveDefaults(currentdefaults);
        }
        internal static void SetActiveState(bool v)
        {
            var currentdefaults = GetDefaults();
            currentdefaults.Acive = v;
            SaveDefaults(currentdefaults);
        }
        internal static void SetViewExtra(bool value)
        {
            var currentdefaults = GetDefaults();
            currentdefaults.ViewExtraData = value;
            SaveDefaults(currentdefaults);
        }
        internal static void SetLockedState(bool locked)
        {
            var currentdefaults = GetDefaults();
            currentdefaults.Locked = locked;
            SaveDefaults(currentdefaults);
        }
        public static RoomOverlayManager GetDefaults()
        {
            if (!File.Exists(infoPath))
                Init();
            var text = File.ReadAllText(infoPath);
            return JsonConvert.DeserializeObject<RoomOverlayManager>(text);
        }
        public static void SaveDefaults(RoomOverlayManager toSave)
        {
            File.WriteAllText(infoPath, JsonConvert.SerializeObject(toSave));
        }


    }
}
