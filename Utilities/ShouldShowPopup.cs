using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace SWTORCombatParser.Utilities
{
    public static class ShouldShowPopup
    {
        private static string appDataPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DubaTech", "SWTORCombatParser");
        private static string _checkedStateFile => Path.Combine(appDataPath, "popupShowAgainStates.json");

        public static void SaveShouldShowPopup(string key, bool state)
        {
            if (!File.Exists(_checkedStateFile))
            {
                var file = File.Create(_checkedStateFile);
                file.Close();
            }
            var currentPopupStates = JsonConvert.DeserializeObject<Dictionary<string, bool>>(File.ReadAllText(_checkedStateFile));
            if (currentPopupStates == null)
            {
                currentPopupStates = new Dictionary<string, bool>();
            }

            currentPopupStates[key] = !state;

            File.WriteAllText(_checkedStateFile, JsonConvert.SerializeObject(currentPopupStates));
        }
        public static bool ReadShouldShowPopup(string key)
        {
            if (!File.Exists(_checkedStateFile))
            {
                var file = File.Create(_checkedStateFile);
                file.Close();
            }

            var currentPopupStates = JsonConvert.DeserializeObject<Dictionary<string, bool>>(File.ReadAllText(_checkedStateFile));
            if (currentPopupStates == null)
            {
                currentPopupStates = new Dictionary<string, bool>();
            }
            if (!currentPopupStates.ContainsKey(key))
                return true;
            else
            {
                return currentPopupStates[key];
            }
        }
    }
}
