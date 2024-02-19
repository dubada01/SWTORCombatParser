using Newtonsoft.Json;
using SWTORCombatParser.DataStructures;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Resources;

namespace SWTORCombatParser.Utilities
{
    public static class IconGetter
    {
        public static ConcurrentDictionary<string, BitmapImage> IconDict = new ConcurrentDictionary<string, BitmapImage>();
        public static Dictionary<string,string> _abilityToIconDict = new Dictionary<string, string>();
        private static string currentPath;
        public static void Init()
        {
            currentPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            var lines = File.ReadAllLines("DataStructures/ability_to_icon.csv");
            _abilityToIconDict = lines.ToDictionary(kvp => kvp.Split(',')[0], kvp =>kvp.Split(',')[1]);
        }
        public static bool HasIcon(string abilityId)
        {
            return _abilityToIconDict.ContainsKey(abilityId);
        }
        public static async Task<BitmapImage> GetIconPathForLog(ParsedLogEntry log)
        {
            if (log == null || log.AbilityId == null) {
                return await LoadImageAsync(GetIconPathForId(""),"");
            }
            return await GetIconForId(log.AbilityId);
        }
        public static string GetIconPathForId(string id)
        {
            if(_abilityToIconDict.TryGetValue(id,out var path))
                return currentPath + @$"\resources\icons\{path}.png";
            return currentPath + @$"\resources\icons\.png";
        }
        public static async Task<BitmapImage> InitIcon(string id)
        {
            var path = GetIconPathForId(id);
            if (File.Exists(path))
                return await LoadImageAsync(path, id); 
            return await LoadImageAsync(GetIconPathForId(""), id);
        }
        public static async Task<BitmapImage> GetIconForId(string id)
        {
            if(IconDict.TryGetValue(id, out var cachedImage)) 
                return cachedImage;
            return await LoadImageAsync(GetIconPathForId(id),id);
        }
        public static async Task<BitmapImage> LoadImageAsync(string imagePath, string abilityId)
        {
            return await Task.Run(() =>
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(imagePath, UriKind.Absolute);
                image.EndInit();
                image.Freeze(); // Make it cross-thread accessible
                IconDict.TryAdd(abilityId, image);
                return image;
            });
        }
    }
}
