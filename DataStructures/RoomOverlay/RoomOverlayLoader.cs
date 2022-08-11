using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser.DataStructures.RoomOverlay
{
    public static class RoomOverlayLoader
    {
        private static string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "DubaTech", "SWTORCombatParser");
        private static string _settingsPath = Path.Combine(appDataPath, "roomOverlayInstances.json");
        public static List<RoomOverlaySettings> GetRoomOverlaySettings()
        {
            if (!File.Exists(_settingsPath))
            {
                File.Create(_settingsPath).Close();
                File.WriteAllText(_settingsPath, JsonConvert.SerializeObject(new List<RoomOverlaySettings>() { new RoomOverlaySettings() { EncounterName = "Template", UpateObjects = new List<RoomOverlayUpdate>() { new RoomOverlayUpdate() { ImageOverlayPath = "Test"} } } }));
            }
            var stringInfo = File.ReadAllText(_settingsPath);
            return JsonConvert.DeserializeObject<List<RoomOverlaySettings>>(stringInfo);
        }
    }
}
