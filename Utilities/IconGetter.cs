using Avalonia.Media.Imaging;
using SWTORCombatParser.DataStructures;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SWTORCombatParser.Utilities
{
    public static class IconGetter
    {
        public static ConcurrentDictionary<ulong, Bitmap> IconDict = new ConcurrentDictionary<ulong, Bitmap>();
        public static Dictionary<ulong, string> _abilityToIconDict = new Dictionary<ulong, string>();
        private static Task _iconsReady = Task.CompletedTask;

        public static void SetIconsReadyTask(Task task) => _iconsReady = task;

        public static void Init()
        {
            var mappings = new Dictionary<ulong, string>();
            try
            {
                foreach (var line in File.ReadLines(Path.Combine(AppContext.BaseDirectory, "DataStructures", "ability_to_icon.csv")))
                {
                    var fields = line.Split(',');
                    if (fields.Length >= 2 && ulong.TryParse(fields[0], out var id))
                        mappings[id] = fields[1];
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Debug.WriteLine("Could not load icon mappings: " + ex.Message);
            }
            _abilityToIconDict = mappings;
        }

        public static bool HasIcon(ulong abilityId)
        {
            return _abilityToIconDict.ContainsKey(abilityId);
        }

        public static async Task<Bitmap?> GetIconPathForLog(ParsedLogEntry log)
        {
            if (log == null || log.AbilityId == null)
            {
                return await LoadImageAsync(GetIconPathForId(0), 0);
            }
            return await GetIconForId(log.AbilityId);
        }

        public static string GetIconPathForId(ulong id)
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DubaTech", "SWTORCombatParser");

            var iconsDir = Path.Combine(appDataPath, "resources", "icons");

            if (_abilityToIconDict.TryGetValue(id, out var key) && !string.IsNullOrWhiteSpace(key))
            {
                var file = key.Trim().ToLowerInvariant() + ".png";
                return Path.Combine(iconsDir, file);
            }

            // Pick an actual known placeholder filename if you have one:
            return Path.Combine(iconsDir, ".png");
        }

        public static async Task<Bitmap?> InitIcon(ulong id)
        {
            return await GetIconForId(id);
        }

        public static async Task<Bitmap?> GetIconForId(ulong id)
        {
            if (IconDict.TryGetValue(id, out var cachedImage))
                return cachedImage;
            return await LoadImageAsync(GetIconPathForId(id), id);
        }

        public static async Task<Bitmap?> LoadImageAsync(string imagePath, ulong abilityId)
        {
            try
            {
                await _iconsReady;
            }
            catch (Exception ex)
            {
                // Some existing icons may still be usable after extraction fails.
                Debug.WriteLine("Could not finish extracting icons: " + ex.Message);
            }
            return await Task.Run<Bitmap?>(() =>
            {
                if (IconDict.TryGetValue(abilityId, out var cached))
                    return cached;
                foreach (var path in new[] { imagePath, GetIconPathForId(0) }.Distinct(StringComparer.Ordinal))
                {
                    try
                    {
                        using var stream = File.OpenRead(path);
                        var bitmap = new Bitmap(stream);
                        var result = IconDict.GetOrAdd(abilityId, bitmap);
                        if (!ReferenceEquals(result, bitmap))
                            bitmap.Dispose();
                        return result;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Could not load icon: " + ex.Message);
                    }
                }
                // Images are optional. A missing/corrupt placeholder must not
                // fault the async UI operation that requested an icon.
                return null;
            });
        }
    }
}
