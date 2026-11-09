using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SWTORCombatParser.Model.Overlays;

public static class OverlayPositionReset
{
    private static string _appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DubaTech", "SWTORCombatParser");
    public static event Action PositionsReset = delegate { };

    public static int ResetSavedPositions(Point position)
    {
        if (!double.IsFinite(position.X) || !double.IsFinite(position.Y))
            throw new ArgumentOutOfRangeException(nameof(position));

        var point = $"{position.X.ToString(CultureInfo.InvariantCulture)}, {position.Y.ToString(CultureInfo.InvariantCulture)}";
        var updates = new List<(string Path, string Json)>();
        var count = 0;
        foreach (var (name, depth) in new[]
        {
            ("global_overlay_info.json", 1), ("character_overlay_info.json", 2),
            ("raidframe_overlay_info.json", 1), ("bossframe_overlay_info.json", 0),
            ("room_overlay_info.json", 0)
        })
        {
            var path = Path.Combine(_appDataPath, name);
            if (!File.Exists(path)) continue;
            var document = JObject.Parse(File.ReadAllText(path));
            var fileCount = 0;
            foreach (var entry in EntriesAtDepth(document, depth))
            {
                if (entry.Property("Position") == null) continue;
                entry["Position"] = point;
                fileCount++;
            }
            if (fileCount == 0) continue;
            count += fileCount;
            updates.Add((path, document.ToString(Formatting.None)));
        }

        // Validate every document first. A malformed layout must not reset only part
        // of the configuration, and this operation never touches other settings files.
        foreach (var update in updates)
        {
            var temporary = update.Path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, update.Json);
                File.Move(temporary, update.Path, true);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
        if (count > 0) PositionsReset();
        return count;
    }

    private static IEnumerable<JObject> EntriesAtDepth(JObject document, int depth) => depth == 0
        ? new[] { document }
        : document.Properties().Select(p => p.Value).OfType<JObject>().SelectMany(child => EntriesAtDepth(child, depth - 1));
}
