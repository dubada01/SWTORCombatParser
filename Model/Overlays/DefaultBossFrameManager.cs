using Newtonsoft.Json;
using System;
using System.IO;
using Avalonia;

namespace SWTORCombatParser.Model.Overlays
{
    public class BossFrameDefaults
    {
        [JsonConverter(typeof(AvaloniaPointConverter))]
        public Point Position;
        [JsonConverter(typeof(AvaloniaPointConverter))]
        public Point WidtHHeight;
        public bool Acive;
        public bool Locked;
        public bool TrackDOTS;
        public bool PredictMechs;
        public bool RaidChallenges;
        public double Scale;
    }
    public class DefaultBossFrameManager
    {
        private static string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DubaTech", "SWTORCombatParser");
        private static string infoPath = Path.Combine(appDataPath, "bossframe_overlay_info.json");
        public static event Action DefaultsUpdated = delegate { };
        public static void Init()
        {
            if (!Directory.Exists(appDataPath))
                Directory.CreateDirectory(appDataPath);
            if (!File.Exists(infoPath))
            {
                File.WriteAllText(infoPath, JsonConvert.SerializeObject(new BossFrameDefaults() { WidtHHeight = new Point(200, 300), Position = new Point(), TrackDOTS = true, PredictMechs = true, Acive=true }));
            }
        }
        internal static void SetDefaults(Point point1, Point point2)
        {
            var currentdefaults = GetDefaults();
            currentdefaults.Position = point1;
            currentdefaults.WidtHHeight = point2;
            SaveDefaults(currentdefaults);
        }
        internal static void SetScale(double scale)
        {
            var currentdefaults = GetDefaults();
            currentdefaults.Scale = scale;
            SaveDefaults(currentdefaults);
        }
        internal static void SetDotTracking(bool track)
        {
            var currentdefaults = GetDefaults();
            currentdefaults.TrackDOTS = track;
            SaveDefaults(currentdefaults);
        }
        internal static void SetPredictMechs(bool track)
        {
            var currentdefaults = GetDefaults();
            if (currentdefaults.PredictMechs == track)
                return;
            currentdefaults.PredictMechs = track;
            SaveDefaults(currentdefaults);
        }
        internal static void SetRaidChallenges(bool track)
        {
            var currentdefaults = GetDefaults();
            if (currentdefaults.RaidChallenges == track)
                return;
            currentdefaults.RaidChallenges = track;
            SaveDefaults(currentdefaults);
        }
        internal static void SetActiveState(bool v)
        {
            var currentdefaults = GetDefaults();
            currentdefaults.Acive = v;
            SaveDefaults(currentdefaults);
        }
        internal static void SetLockedState(bool locked)
        {
            var currentdefaults = GetDefaults();
            currentdefaults.Locked = locked;
            SaveDefaults(currentdefaults);
        }
        public static BossFrameDefaults GetDefaults()
        {
            if (!File.Exists(infoPath))
                Init();
            var text = File.ReadAllText(infoPath);
            if (text == "")
                return new BossFrameDefaults();
            return JsonConvert.DeserializeObject<BossFrameDefaults>(text);
        }
        public static void SaveDefaults(BossFrameDefaults toSave)
        {
            File.WriteAllText(infoPath, JsonConvert.SerializeObject(toSave));
            DefaultsUpdated();
        }
    }
}
