using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace SWTORCombatParser.Model.Notes
{
    public static class RaidNotesReader
    {
        private static string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DubaTech", "SWTORCombatParser");
        private static string infoPath = Path.Combine(appDataPath, "raid_notes.json");
        public static void Init()
        {
            if (!Directory.Exists(appDataPath))
                Directory.CreateDirectory(appDataPath);
            if (!File.Exists(infoPath))
            {
                File.WriteAllText(infoPath, JsonConvert.SerializeObject(new Dictionary<string,string>()));
            }
        }
        public static void SetNotes(Dictionary<string,string> notes)
        {
            File.WriteAllText(infoPath, JsonConvert.SerializeObject(notes));
        }
        public static string GetNoteForRaid(string raid)
        {
            Dictionary<string,string> raidNotes = JsonConvert.DeserializeObject<Dictionary<string,string>>(File.ReadAllText(infoPath));
            return raidNotes[raid];
        }
        public static Dictionary<string,string> GetAllRaidNotes()
        {
            return JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(infoPath));
        }
    }
}
