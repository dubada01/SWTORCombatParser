using Newtonsoft.Json;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;
using System.IO;

namespace SWTORCombatParser.Model.DataGrid
{
    internal class DataGridDefaults
    {
        private static List<OverlayType> _selectedColumnTypes = new List<OverlayType>() { OverlayType.DPS, OverlayType.BurstDPS, OverlayType.EHPS, OverlayType.BurstEHPS, OverlayType.DamageTaken, OverlayType.BurstDamageTaken, OverlayType.APM };
        private static string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DubaTech", "SWTORCombatParser");
        private static string infoPath = Path.Combine(appDataPath, "datagrid_column_selections.json");
        public static void Init()
        {
            if (!Directory.Exists(appDataPath))
                Directory.CreateDirectory(appDataPath);
            if (!File.Exists(infoPath))
            {
                File.WriteAllText(infoPath, JsonConvert.SerializeObject(new Dictionary<string, List<OverlayType>>()));
            }
        }
        public static void SetDefaults(List<OverlayType> columns, string characterName)
        {
            SaveResults(characterName, columns);
        }

        public static List<OverlayType> GetDefaults(string characterName)
        {
            var currentDefaults = GetCurrentDefaults();

            if (!currentDefaults.ContainsKey(characterName))
            {
                if (characterName.Contains("_") && currentDefaults.ContainsKey(characterName.Split('_')[0]))
                {
                    CopyFromKey(characterName.Split('_')[0], characterName);
                    currentDefaults = GetCurrentDefaults();
                }
                else
                {
                    InitializeDefaults(characterName);
                }
            }
            if (!currentDefaults.ContainsKey(characterName))
            {
                Logging.LogInfo("Creating new source: " + characterName);
                InitializeDefaults(characterName);
                return GetCurrentDefaults()[characterName];
            }
            var defaultsForToon = currentDefaults[characterName];

            return defaultsForToon;
        }
        private static void SaveResults(string character, List<OverlayType> data)
        {
            var currentDefaults = GetCurrentDefaults();
            currentDefaults[character] = data;
            File.WriteAllText(infoPath, JsonConvert.SerializeObject(currentDefaults));
        }
        private static void CopyFromKey(string from, string to)
        {
            var currentDefaults = GetCurrentDefaults();
            var fromDefaults = currentDefaults[from];
            if (fromDefaults == null)
            {
                InitializeDefaults(to);
            }
            else
            {
                currentDefaults[to] = currentDefaults[from];
                File.WriteAllText(infoPath, JsonConvert.SerializeObject(currentDefaults));
            }
        }
        private static void InitializeDefaults(string characterName)
        {
            var currentDefaults = GetCurrentDefaults();
            currentDefaults[characterName] = _selectedColumnTypes;
            File.WriteAllText(infoPath, JsonConvert.SerializeObject(currentDefaults));
        }
        private static Dictionary<string, List<OverlayType>> GetCurrentDefaults()
        {
            var stringInfo = File.ReadAllText(infoPath);
            var currentDefaults = JsonConvert.DeserializeObject<Dictionary<string, List<OverlayType>>>(stringInfo);
            return currentDefaults;
        }
    }

}
