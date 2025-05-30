using Avalonia.Media.Imaging;
using SWTORCombatParser.DataStructures;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SWTORCombatParser.Utilities
{
    public static class IconGetter
    {
        public static ConcurrentDictionary<ulong, Bitmap> IconDict = new ConcurrentDictionary<ulong, Bitmap>();
        public static Dictionary<ulong, string> _abilityToIconDict = new Dictionary<ulong, string>();

        public static void Init()
        {
            var lines = File.ReadAllLines("DataStructures/ability_to_icon.csv");
            _abilityToIconDict = lines.Where(line=>!line.Contains("ability_id")).ToDictionary(kvp => ulong.Parse(kvp.Split(',')[0]), kvp => kvp.Split(',')[1]);
        }

        public static bool HasIcon(ulong abilityId)
        {
            return _abilityToIconDict.ContainsKey(abilityId);
        }

        public static async Task<Bitmap> GetIconPathForLog(ParsedLogEntry log)
        {
            if (log == null || log.AbilityId == null)
            {
                return await LoadImageAsync(GetIconPathForId(0), 0);
            }
            return await GetIconForId(log.AbilityId);
        }

        public static string GetIconPathForId(ulong id)
        {
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DubaTech", "SWTORCombatParser");
            if (_abilityToIconDict.TryGetValue(id, out var path))
                return Path.Combine(appDataPath, "resources", "icons", $"{path}.png");
            return Path.Combine(appDataPath, "resources", "icons", ".png");
        }

        public static async Task<Bitmap> InitIcon(ulong id)
        {
            var path = GetIconPathForId(id);
            if (File.Exists(path))
                return await LoadImageAsync(path, id);
            return await LoadImageAsync(GetIconPathForId(0), id);
        }

        public static async Task<Bitmap> GetIconForId(ulong id)
        {
            if (IconDict.TryGetValue(id, out var cachedImage))
                return cachedImage;
            return await LoadImageAsync(GetIconPathForId(id), id);
        }

        public static async Task<Bitmap> LoadImageAsync(string imagePath, ulong abilityId)
        {
            try
            {
                return await Task.Run(() =>
                {
                    using (var stream = File.OpenRead(imagePath))
                    {
                        var bitmap = new Bitmap(stream);
                        IconDict.TryAdd(abilityId, bitmap);
                        return bitmap;
                    }
                });
            }
            catch
            {
                string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DubaTech", "SWTORCombatParser");
                var icon =  await LoadImageAsync(Path.Combine(appDataPath, "resources", "icons", ".png"), abilityId);
                IconDict.TryAdd(abilityId, icon);
                return icon;
            }
        }
    }
}
