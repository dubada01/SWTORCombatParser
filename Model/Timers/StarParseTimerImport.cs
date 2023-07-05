using Microsoft.VisualBasic.ApplicationServices;
using SWTORCombatParser.DataStructures;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Media;
using System.Xml.Serialization;
using Timer = SWTORCombatParser.DataStructures.Timer;

namespace SWTORCombatParser.Model.Timers
{
    public class ConfigTimers
    {
        [XmlArray("timers")]
        [XmlArrayItem("ConfigTimer", typeof(ConfigTimer))]
        public ConfigTimer[] Timers { get; set; }
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
            XmlRootAttribute xRoot = new XmlRootAttribute();
            xRoot.ElementName = "ConfigTimers";
            xRoot.Namespace = "com.ixale.starparse.domain";
            xRoot.IsNullable = true;

            XmlSerializer xs = new XmlSerializer(typeof(ConfigTimers), xRoot);
            ConfigTimers timers;

            using (StringReader reader = new StringReader(xmlText))
            {
                timers = (ConfigTimers)xs.Deserialize(reader);
            }
            return timers.Timers.Select(ConvertTimer).ToList();
        }
        public static Timer ConvertTimer(ConfigTimer spTimer)
        {
            return new Timer()
            {
                TriggerType = GetTriggerType(spTimer.Trigger.Type),
                Name = spTimer.Name,
                Id = Guid.NewGuid().ToString(),
                Source = spTimer.Trigger.Source == "@Self" ? "LocalPlayer" : spTimer.Trigger.Source,
                Target = spTimer.Trigger.Target == "@Self" ? "LocalPlayer" : spTimer.Trigger.Target,
                Effect = spTimer.Trigger.EffectGuid,
                Ability = spTimer.Trigger.AbilityGuid,
                TimerColor = (Color)ColorConverter.ConvertFromString(spTimer.Color),
                DurationSec = spTimer.Interval,
                SpecificBoss = spTimer.Trigger.Boss,
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
                default:
                    return TimerKeyType.CombatStart;
            }
        }

        private static string GetFileText()
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                // Create a new file dialog
                OpenFileDialog openFileDialog = new OpenFileDialog();

                // Set the default file filter and title
                openFileDialog.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
                openFileDialog.Title = "Select a text file";

                // Show the file dialog and get the result
                DialogResult result = openFileDialog.ShowDialog();

                // Check if the user clicked "OK"
                if (result == DialogResult.OK)
                {
                    // Get the selected file path
                    string filePath = openFileDialog.FileName;

                    // Read the contents of the file
                    string fileContents = File.ReadAllText(filePath);

                    // Pass the file contents to the processing function
                    return fileContents;
                }
                return "";
            });
            return "";
        }
    }
}