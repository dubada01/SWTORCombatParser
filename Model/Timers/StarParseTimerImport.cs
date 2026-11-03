using SWTORCombatParser.DataStructures;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Timer = SWTORCombatParser.DataStructures.Timer;

namespace SWTORCombatParser.Model.Timers
{
    [XmlRoot("com.ixale.starparse.domain.ConfigTimers")]
    public class ConfigTimers
    {
        [XmlArray("timers")]
        [XmlArrayItem("ConfigTimer", typeof(ConfigTimer))]
        public ConfigTimer[] timers { get; set; }
    }

    public class ConfigTimer
    {
        public string Name { get; set; }

        public Trigger Trigger { get; set; }
        public Cancel Cancel { get; set; }

        public double Interval { get; set; }

        public string Color { get; set; }

        public string Audio { get; set; }

        public string Slot { get; set; }

        public bool Enabled { get; set; }

        public bool IgnoreRepeated { get; set; }

        public bool ShowSource { get; set; }
    }
    public class Cancel
    {
        public string Type { get; set; }
        public string Timer { get; set; }
    }
    public class Trigger
    {
        public string Type { get; set; }

        public string Source { get; set; }

        public string AbilityGuid { get; set; }

        public string Boss { get; set; }

        public string Target { get; set; }

        public string EffectGuid { get; set; }
        public string Timer { get; set; }
    }

    public static class ImportSPTimers
    {
        public static List<Timer> Import()
        {
            // Use this code to deserialize the XML string into the ConfigTimers object
            var xmlString = GetFileText();
            if (string.IsNullOrEmpty(xmlString))
                return new List<Timer>();
            return ConvertXML(xmlString);
        }
        public static List<Timer> ConvertXML(string xmlText)
        {
            XmlSerializer xs = new XmlSerializer(typeof(SPTimersContainer));
            SPTimersContainer timers;

            using (StringReader reader = new StringReader(xmlText))
            {
                timers = (SPTimersContainer)xs.Deserialize(reader);
            }
            return timers.Items.First().comixalestarparsedomainConfigTimer.Where(sp => sp.trigger != null).Select(v => ConvertTimer(v)).ToList();
        }
        public static Timer ConvertTimer(SPTimer spTimer)
        {
            var trigger = spTimer.trigger.FirstOrDefault();
            return new Timer()
            {
                TriggerType = GetTriggerType(trigger.type),
                Name = spTimer.name,
                Id = Guid.NewGuid().ToString(),
                Source = trigger.source == "@Self" ? "LocalPlayer" : trigger.source,
                Target = trigger.target == "@Self" ? "LocalPlayer" : trigger.target,
                Effect = string.IsNullOrEmpty(trigger.effectGuid) ? trigger.effect : trigger.effectGuid,
                Ability = string.IsNullOrEmpty(trigger.abilityGuid) ? trigger.ability : trigger.abilityGuid,
                TimerColor = string.IsNullOrEmpty(spTimer.color) ? Colors.Red : Color.Parse("#" + spTimer.color.Split('x')[1]),
                DurationSec = string.IsNullOrEmpty(spTimer.interval) ? double.Parse(spTimer.countdownCount) : double.Parse(spTimer.interval),
                SpecificBoss = trigger.boss,
                IsImportedFromSP = true,
                TimerSource = "StarParse Import"
            };
        }

        private static TimerKeyType GetTriggerType(string trigger)
        {
            switch (trigger)
            {
                case "EFFECT_GAINED":
                    return TimerKeyType.EffectGained;
                case "EFFECT_LOST":
                    return TimerKeyType.EffectLost;
                case "TIMER_FINISHED":
                    return TimerKeyType.TimerExpired;
                case "ABILITY_ACTIVATED":
                    return TimerKeyType.AbilityUsed;
                case "COMBAT_START":
                    return TimerKeyType.CombatStart;
                case "DAMAGE":
                    return TimerKeyType.DamageTaken;
                case "HEALING":
                    return TimerKeyType.AbilityUsed;
                default:
                    return TimerKeyType.CombatStart;
            }
        }

        private static string GetFileText()
        {
            Dispatcher.UIThread.Invoke(async() =>
            {
                if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    var topLevel = desktop.MainWindow;
                    var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
                    {
                        Title = "Select a StarParse Timer file",
                        AllowMultiple = false,
                    });
                    if (files.Count > 1)
                    {
                        // Read the contents of the file
                        Stream fileContents = await files[0].OpenReadAsync();
                        StreamReader reader = new StreamReader(fileContents);
                        string text = reader.ReadToEnd();
                        return text;
                    }
                }
                return "";
            });
            return "";
        }
    }
}