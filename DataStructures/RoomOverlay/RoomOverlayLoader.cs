using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SWTORCombatParser.DataStructures.RoomOverlay
{
    public static class RoomOverlayLoader
    {
        private static string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DubaTech", "SWTORCombatParser");
        private static string _settingsPath = Path.Combine(appDataPath, "roomOverlayInstances_v2.json");
        public static List<RoomOverlaySettings> GetRoomOverlaySettings()
        {
            if (!File.Exists(_settingsPath))
            {
                File.Create(_settingsPath).Close();
                //File.WriteAllText(_settingsPath, JsonConvert.SerializeObject(new List<RoomOverlaySettings>() { new RoomOverlaySettings() { EncounterName = "Template", UpateObjects = new List<RoomOverlayUpdate>() { new RoomOverlayUpdate() { ImageOverlayPath = "Test"} } } }));
            }
            var stringInfo = File.ReadAllText(_settingsPath);
            var settings = JsonConvert.DeserializeObject<List<RoomOverlaySettings>>(stringInfo);

            if (settings == null)
                settings = new List<RoomOverlaySettings>();
            var modified = false;
            if (!settings.Any(s => s.EncounterName == "IP-CPT"))
            {
                var ipCPT = JsonConvert.DeserializeObject<RoomOverlaySettings>(File.ReadAllText("DataStructures/RoomOverlay/IPCPT.json"));
                settings.Add(ipCPT);
                modified = true;
            }
            if (!settings.Any(s => s.EncounterName == "NAHUT"))
            {
                var nahut = JsonConvert.DeserializeObject<RoomOverlaySettings>(File.ReadAllText("DataStructures/RoomOverlay/Nahut.json"));
                settings.Add(nahut);
                modified = true;
            }
            if(modified)
                File.WriteAllText(_settingsPath, JsonConvert.SerializeObject(settings));
            return settings;
        }
    }
}
