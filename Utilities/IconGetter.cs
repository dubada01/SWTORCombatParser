using Newtonsoft.Json;
using SWTORCombatParser.DataStructures;
using System;
using System.Collections.Generic;
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
        public static Dictionary<string, BitmapImage> IconDict = new Dictionary<string, BitmapImage>();
        public static void Init()
        {
            var lines = File.ReadAllLines("DataStructures/ability_to_icon.csv");
            IconDict = lines.ToDictionary(kvp =>kvp.Split(',')[0], kvp => InitIcon(kvp.Split(',')[1]));
        }
        public static BitmapImage GetIconPathForLog(ParsedLogEntry log)
        {
            if (log == null || log.AbilityId == null) {
                return new BitmapImage(new Uri("\\resources\\icons\\.png", UriKind.Relative));
            }
            return GetIconForId(log.AbilityId);
        }
        public static string GetIconPathForId(string path)
        {

            return $"\\resources\\icons\\{path}.png";
        }
        public static BitmapImage InitIcon(string id)
        {
            var path = GetIconPathForId(id);
            var uri = new Uri(path, UriKind.Relative);
            if (!File.Exists(path))
                return new BitmapImage(uri);
            return new BitmapImage(new Uri("\\resources\\icons\\.png", UriKind.Relative));
        }
        public static BitmapImage GetIconForId(string id)
        {
            if (!IconDict.ContainsKey(id))
                return new BitmapImage(new Uri("\\resources\\icons\\.png", UriKind.Relative));
            return IconDict[id];
        }
    }
}
