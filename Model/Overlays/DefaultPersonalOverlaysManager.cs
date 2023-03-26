using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace SWTORCombatParser.Model.Overlays
{
    public class PersonalOverlaySettings
    {
        public List<OverlayType> Metrics { get; set; } = new List<OverlayType>();
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
                    {"Damage", new PersonalOverlaySettings(){ Metrics = new List<OverlayType>{OverlayType.DPS,OverlayType.Damage,OverlayType.APM,OverlayType.CritPercent } } },
                    {"Heals", new PersonalOverlaySettings(){ Metrics = new List<OverlayType>{OverlayType.EHPS,OverlayType.EffectiveHealing,OverlayType.APM,OverlayType.BurstEHPS } } },
                    {"Tank", new PersonalOverlaySettings(){ Metrics = new List<OverlayType>{OverlayType.DPS,OverlayType.Mitigation,OverlayType.APM,OverlayType.DamageSavedDuringCD } } },
                };
                File.WriteAllText(infoPath, JsonConvert.SerializeObject(defaults));
            }
        }
        public static PersonalOverlaySettings GetSettingsForOwner(string owner)
        {
            var currentSettings = JsonConvert.DeserializeObject<Dictionary<string,PersonalOverlaySettings>>(File.ReadAllText(infoPath));
            if(currentSettings.ContainsKey(owner)) return currentSettings[owner];
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
