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
        public Point Position;
        public Point WidtHHeight;
        public bool Acive;
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
                File.WriteAllText(infoPath, JsonConvert.SerializeObject(new Dictionary<string, DefaultTimersData>()));
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
            var valueToRemove = currentDefaults.Timers.First(t => TimerEquality.Equals(timer, t));
            currentDefaults.Timers.Remove(valueToRemove);
            SaveResults(character, currentDefaults);
        }
        public static void SetTimerEnabled(bool state, Timer timer)
        {
            var currentDefaults = GetDefaults(timer.CharacterOwner);
            var timerToModify = currentDefaults.Timers.First(t => t.Id == timer.Id);
            timerToModify.IsEnabled = state;
            SaveResults(timerToModify.CharacterOwner, currentDefaults);
        }
        public static void SetActiveState(bool state, string characterName)
        {
            var currentDefaults = GetDefaults(characterName);
            currentDefaults.Acive = state;
            SaveResults(characterName, currentDefaults);
        }
        public static DefaultTimersData GetDefaults(string characterName)
        {
            var stringInfo = File.ReadAllText(infoPath);
            try
            {
                var currentDefaults = JsonConvert.DeserializeObject<Dictionary<string, DefaultTimersData>>(stringInfo);
                if (!currentDefaults.ContainsKey(characterName) || currentDefaults[characterName] == null)
                {
                    InitializeDefaults(characterName);
                }
                var initializedInfo = File.ReadAllText(infoPath);
                currentDefaults = JsonConvert.DeserializeObject<List<DefaultTimersData>>(initializedInfo);
                return currentDefaults.First(t=>t.TimerSource == timerSource);
            }
            catch (Exception e)
            {
                InitializeDefaults(characterName);
                var resetDefaults = File.ReadAllText(infoPath);
                return JsonConvert.DeserializeObject<Dictionary<string, DefaultTimersData>>(resetDefaults)[characterName];
            }

        }
        public static Dictionary<string, DefaultTimersData> GetAllDefaults()
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
        private static void SaveResults(string character, DefaultTimersData data)
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
        private static void InitializeDefaults(string characterName)
        {
            var stringInfo = File.ReadAllText(infoPath);
            var currentDefaults = JsonConvert.DeserializeObject<Dictionary<string, DefaultTimersData>>(stringInfo);

            currentDefaults.Add(defaults);
            var classes = ClassLoader.LoadAllClasses();
            var validSources = classes.Select(c => c.Discipline);
            var validDefaults = currentDefaults.Where(c => validSources.Contains(c.TimerSource) || c.TimerSource == "Shared" || c.TimerSource == "HOTS" || c.TimerSource.Contains('|'));
            File.WriteAllText(infoPath, JsonConvert.SerializeObject(validDefaults));
        }
    }
}