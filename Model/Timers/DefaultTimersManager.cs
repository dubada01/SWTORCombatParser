using Newtonsoft.Json;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.ClassInfos;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace SWTORCombatParser.Model.Timers
{
    public class DefaultTimersData
    {
        public string TimerSource;

        public Point Position;
        public Point WidtHHeight;
        
        public List<Timer> Timers = new List<Timer>();
    }
    public class TimersActive
    {
        public bool DisciplineActive { get; set; }
        public bool EncounterActive { get; set; }
    }
    public static class DefaultTimersManager
    {
        private static string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "DubaTech", "SWTORCombatParser");
        private static string infoPath = Path.Combine(appDataPath, "timers_info.json");
        private static string activePath = Path.Combine(appDataPath, "timers_active.json");
        public static void UpdateTimersActive(bool disciplineActive, bool encounterActive)
        {
            File.WriteAllText(activePath, JsonConvert.SerializeObject(new TimersActive() { DisciplineActive = disciplineActive,EncounterActive=encounterActive }));
        }
        public static TimersActive GetTimersActive()
        {
            return JsonConvert.DeserializeObject<TimersActive>(File.ReadAllText(activePath));
        }
        public static void Init()
        {
            if (!Directory.Exists(appDataPath))
                Directory.CreateDirectory(appDataPath);
            if (!File.Exists(infoPath))
            {
                File.WriteAllText(infoPath, JsonConvert.SerializeObject(new List<DefaultTimersData>()));
            }
            if (!File.Exists(activePath))
            {
                File.WriteAllText(activePath, JsonConvert.SerializeObject(new TimersActive()));
            }
        }
        public static void SetDefaults(Point position, Point widtHHeight, string characterName)
        {
            var currentDefaults = GetDefaults(characterName);
            currentDefaults.WidtHHeight = widtHHeight;
            currentDefaults.Position = position;
            SaveResults(characterName, currentDefaults);
        }
        public static void SetIdForTimer(Timer timer, string character, string id)
        {
            var currentDefaults = GetDefaults(character);
            var valueToUpdate = currentDefaults.Timers.First(t => TimerEquality.Equals(timer, t));
            valueToUpdate.ShareId = id;
            SaveResults(character, currentDefaults);
        }
        public static void SetSavedTimers(List<Timer> currentTimers, string character)
        {
            var currentDefaults = GetDefaults(character);
            currentDefaults.Timers = currentTimers;
            SaveResults(character, currentDefaults);
        }
        public static void AddTimerForSource(Timer timer, string source)
        {
            var currentDefaults = GetDefaults(source);
            currentDefaults.Timers.Add(timer);
            SaveResults(source, currentDefaults);
        }
        public static void RemoveTimerForCharacter(Timer timer, string character)
        {
            var currentDefaults = GetDefaults(character);
            var valueToRemove = currentDefaults.Timers.First(t => TimerEquality.Equals(timer,t));
            currentDefaults.Timers.Remove(valueToRemove);
            SaveResults(character, currentDefaults);
        }
        public static void SetTimerEnabled(bool state, Timer timer)
        {
            var currentDefaults = GetDefaults(timer.TimerSource);
            var timerToModify = currentDefaults.Timers.First(t => t.Id == timer.Id);
            timerToModify.IsEnabled = state;
            SaveResults(timerToModify.TimerSource, currentDefaults);
        }
        public static DefaultTimersData GetDefaults(string timerSource)
        {
            var stringInfo = File.ReadAllText(infoPath);
            try
            {
                var currentDefaults = JsonConvert.DeserializeObject<List<DefaultTimersData>>(stringInfo);
                if (!currentDefaults.Any(c=>c.TimerSource == timerSource))
                {
                    InitializeDefaults(timerSource);
                }

                var initializedInfo = File.ReadAllText(infoPath);
                currentDefaults = JsonConvert.DeserializeObject<List<DefaultTimersData>>(initializedInfo);
                return currentDefaults.First(t=>t.TimerSource == timerSource);
            }
            catch (Exception e)
            {
                InitializeDefaults(timerSource);
                var resetDefaults = File.ReadAllText(infoPath);
                return JsonConvert.DeserializeObject<List<DefaultTimersData>>(resetDefaults).First(t=>t.TimerSource == timerSource);
            }
        }
        public static List<DefaultTimersData> GetAllDefaults()
        {
            var stringInfo = File.ReadAllText(infoPath);
            if (string.IsNullOrEmpty(stringInfo))
            {
                return new List<DefaultTimersData>();
            }
            try
            {
                var currentDefaults = JsonConvert.DeserializeObject<List<DefaultTimersData>>(stringInfo);
                var classes = ClassLoader.LoadAllClasses();
                var validSources = classes.Select(c => c.Discipline);
                var validDefaults = currentDefaults.Where(c => validSources.Contains(c.TimerSource) || c.TimerSource == "Shared" || c.TimerSource == "HOTS" || c.TimerSource.Contains('|')).ToList();
                return validDefaults;
            }
            catch(JsonSerializationException e)
            {
                File.WriteAllText(infoPath, "");
                return new List<DefaultTimersData>();
            }
        }
        private static void SaveResults(string timerSource, DefaultTimersData data)
        {
            var stringInfo = File.ReadAllText(infoPath);
            var currentDefaults = JsonConvert.DeserializeObject<List<DefaultTimersData>>(stringInfo);

            currentDefaults.Remove(currentDefaults.First(cd => cd.TimerSource == timerSource));
            currentDefaults.Add(data);
            var classes = ClassLoader.LoadAllClasses();
            var validSources = classes.Select(c => c.Discipline);
            var validDefaults = currentDefaults.Where(c => validSources.Contains(c.TimerSource) || c.TimerSource == "Shared" || c.TimerSource == "HOTS" || c.TimerSource.Contains('|'));

            File.WriteAllText(infoPath, JsonConvert.SerializeObject(validDefaults));
        }
        private static void InitializeDefaults(string timerSource)
        {
            var stringInfo = File.ReadAllText(infoPath);
            var currentDefaults = new List<DefaultTimersData>();
            if (!string.IsNullOrEmpty(stringInfo))
            {
                currentDefaults = JsonConvert.DeserializeObject<List<DefaultTimersData>>(stringInfo);
            }

            var defaults = new DefaultTimersData() {TimerSource = timerSource, Position = new Point(0, 0), WidtHHeight = new Point(100, 200)};

            currentDefaults.Add(defaults);
            var classes = ClassLoader.LoadAllClasses();
            var validSources = classes.Select(c => c.Discipline);
            var validDefaults = currentDefaults.Where(c => validSources.Contains(c.TimerSource) || c.TimerSource == "Shared" || c.TimerSource == "HOTS" || c.TimerSource.Contains('|'));
            File.WriteAllText(infoPath, JsonConvert.SerializeObject(validDefaults));
        }
    }
}
