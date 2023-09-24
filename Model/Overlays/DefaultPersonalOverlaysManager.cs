using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SWTORCombatParser.Model.Overlays
{
    public class CellInfo
    {
        public OverlayType CellType { get; set; }
        public string CustomVariable { get; set; }
    }
    public class PersonalOverlaySettings
    {
        public List<CellInfo> CellInfos { get; set; } = new List<CellInfo>();
    }
    public static class DefaultPersonalOverlaysManager
    {
        private static string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DubaTech", "SWTORCombatParser");
        private static string infoPath = Path.Combine(appDataPath, "personal_overlay.json");
        public static event Action<string> NewDefaultSelected = delegate { };
        public static void Init()
        {
            if (!Directory.Exists(appDataPath))
                Directory.CreateDirectory(appDataPath);

            if (!File.Exists(infoPath))
            {
                var defaults = new Dictionary<string, PersonalOverlaySettings>()
                {
                    {"Damage", new PersonalOverlaySettings(){ CellInfos = new List<OverlayType>{OverlayType.DPS,OverlayType.Damage,OverlayType.APM,OverlayType.CritPercent }.Select(m=>new CellInfo{CellType = m }).ToList()} },
                    {"Heals", new PersonalOverlaySettings(){ CellInfos = new List<OverlayType>{OverlayType.EHPS,OverlayType.EffectiveHealing,OverlayType.APM,OverlayType.BurstEHPS }.Select(m=>new CellInfo{CellType = m }).ToList() } },
                    {"Tank", new PersonalOverlaySettings(){ CellInfos = new List<OverlayType>{OverlayType.DPS,OverlayType.Mitigation,OverlayType.APM,OverlayType.DamageSavedDuringCD }.Select(m=>new CellInfo{CellType = m }).ToList() } },
                };
                File.WriteAllText(infoPath, JsonConvert.SerializeObject(defaults));
            }
        }
        public static PersonalOverlaySettings GetSettingsForOwner(string owner)
        {
            var currentSettings = JsonConvert.DeserializeObject<Dictionary<string, PersonalOverlaySettings>>(File.ReadAllText(infoPath));
            if (currentSettings.ContainsKey(owner)) return currentSettings[owner];
            return new PersonalOverlaySettings();
        }
        public static void SetSettingsForOwner(string owner, PersonalOverlaySettings settings)
        {
            var currentSettings = JsonConvert.DeserializeObject<Dictionary<string, PersonalOverlaySettings>>(File.ReadAllText(infoPath));
            currentSettings[owner] = settings;
            File.WriteAllText(infoPath, JsonConvert.SerializeObject(currentSettings));
        }
        public static void SelectNewDefault(string name)
        {
            NewDefaultSelected(name);
        }

    }
}
