using Avalonia.Media.Imaging;
using Newtonsoft.Json;
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
        public static ConcurrentDictionary<string, Bitmap> IconDict = new ConcurrentDictionary<string, Bitmap>();
        public static Dictionary<string, string> _abilityToIconDict = new Dictionary<string, string>();

        public static void Init()
        {
            var lines = File.ReadAllLines("DataStructures/ability_to_icon.csv");
            _abilityToIconDict = lines.ToDictionary(kvp => kvp.Split(',')[0], kvp => kvp.Split(',')[1]);
        }

        public static bool HasIcon(string abilityId)
        {
            return _abilityToIconDict.ContainsKey(abilityId);
        }

        public static async Task<Bitmap> GetIconPathForLog(ParsedLogEntry log)
        {
            if (log == null || log.AbilityId == null)
            {
                return await LoadImageAsync(GetIconPathForId(""), "");
            }
            return await GetIconForId(log.AbilityId);
        }

        public static string GetIconPathForId(string id)
        {
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DubaTech", "SWTORCombatParser");
            if (_abilityToIconDict.TryGetValue(id, out var path))
                return Path.Combine(appDataPath, "resources", "icons", $"{path}.png");
            return Path.Combine(appDataPath, "resources", "icons", ".png");
        }

        public static async Task<Bitmap> InitIcon(string id)
        {
            var path = GetIconPathForId(id);
            if (File.Exists(path))
                return await LoadImageAsync(path, id);
            return await LoadImageAsync(GetIconPathForId(""), id);
        }

        public static async Task<Bitmap> GetIconForId(string id)
        {
            if (IconDict.TryGetValue(id, out var cachedImage))
                return cachedImage;
            return await LoadImageAsync(GetIconPathForId(id), id);
        }

        public static async Task<Bitmap> LoadImageAsync(string imagePath, string abilityId)
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
                return await LoadImageAsync(Path.Combine(appDataPath, "resources", "icons", ".png"), abilityId);
            }
        }
    }
}
