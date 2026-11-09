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
        private static readonly object FileLock = new object();
        private static string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DubaTech", "SWTORCombatParser");
        private static string infoPath = Path.Combine(appDataPath, "raidframe_overlay_info.json");
        private static readonly JsonSerializerSettings _settings = new JsonSerializerSettings
        {
            Culture = CultureInfo.InvariantCulture
        };

        public static bool IsValidGridDimension(double value) =>
            double.IsFinite(value) && value >= 1 && value <= 24 && value == Math.Truncate(value);

        public static void Init()
        {
            lock (FileLock)
            {
                Directory.CreateDirectory(appDataPath);
                if (!File.Exists(infoPath))
                    Save(new Dictionary<string, RaidFrameOverlayInfo>());
            }
        }

        public static void SetDefaults(Point position, Point widtHHeight, string characterName)
        {
            Update(characterName, defaults =>
            {
                defaults.Position = position;
                defaults.WidtHHeight = widtHHeight;
            });
        }

        public static void SetActiveState(bool state, string characterName) =>
            Update(characterName, defaults => defaults.Acive = state);

        public static void SetRowsColumns(double rows, double columns, string characterName)
        {
            if (!IsValidGridDimension(rows))
                throw new ArgumentOutOfRangeException(nameof(rows), "Choose between 1 and 24 rows.");
            if (!IsValidGridDimension(columns))
                throw new ArgumentOutOfRangeException(nameof(columns), "Choose between 1 and 24 columns.");
            Update(characterName, defaults =>
            {
                defaults.Rows = rows;
                defaults.Columns = columns;
            });
        }

        public static RaidFrameOverlayInfo GetDefaults(string characterName)
        {
            lock (FileLock)
            {
                var allDefaults = GetCurrentDefaults();
                var defaults = GetOrCreateDefaults(allDefaults, characterName, out var changed);
                if (changed)
                    Save(allDefaults);
                return defaults;
            }
        }

        private static void Update(string characterName, Action<RaidFrameOverlayInfo> update)
        {
            // Read, modify, and write under one lock: resize, enabled state, and
            // row/column changes must not discard one another's saved values.
            lock (FileLock)
            {
                var allDefaults = GetCurrentDefaults();
                var defaults = GetOrCreateDefaults(allDefaults, characterName, out _);
                update(defaults);
                Save(allDefaults);
            }
        }

        private static RaidFrameOverlayInfo GetOrCreateDefaults(
            Dictionary<string, RaidFrameOverlayInfo> allDefaults, string characterName, out bool changed)
        {
            changed = false;
            if (!allDefaults.TryGetValue(characterName, out var defaults) || defaults == null)
            {
                allDefaults.TryGetValue("no character", out var template);
                defaults = new RaidFrameOverlayInfo
                {
                    Position = template?.Position ?? new Point(0, 0),
                    WidtHHeight = template?.WidtHHeight ?? new Point(400, 400),
                    Rows = template?.Rows ?? 4,
                    Columns = template?.Columns ?? 2
                };
                allDefaults[characterName] = defaults;
                changed = true;
            }
            if (!IsValidGridDimension(defaults.Rows))
            {
                defaults.Rows = 4;
                changed = true;
            }
            if (!IsValidGridDimension(defaults.Columns))
            {
                defaults.Columns = 2;
                changed = true;
            }
            return defaults;
        }

        private static Dictionary<string, RaidFrameOverlayInfo> GetCurrentDefaults()
        {
            Init();
            return JsonConvert.DeserializeObject<Dictionary<string, RaidFrameOverlayInfo>>(File.ReadAllText(infoPath), _settings)
                ?? new Dictionary<string, RaidFrameOverlayInfo>();
        }

        private static void Save(Dictionary<string, RaidFrameOverlayInfo> defaults) =>
            File.WriteAllText(infoPath, JsonConvert.SerializeObject(defaults, _settings));
    }
}
