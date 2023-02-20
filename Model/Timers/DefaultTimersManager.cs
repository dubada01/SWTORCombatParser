using Newtonsoft.Json;
using ScottPlot.Styles;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.ClassInfos;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace SWTORCombatParser.Model.Timers
{
    public class DefaultTimersData
    {
        public string TimerSource;
        public bool IsBossSource;
        public Point Position;
        public Point WidtHHeight;
        
        public List<Timer> Timers = new List<Timer>();
    }
    public static class DefaultTimersManager
    {
        private static string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DubaTech", "SWTORCombatParser");

        private static string infoPath = Path.Combine(appDataPath, "timers_info_v3.json");
        private static string activePath = Path.Combine(appDataPath, "timers_active_v2.json");
        public static void UpdateTimersActive(bool timersActive, string timerSource)
        {
            var currentActives = GetAllTimersActiveInfo();
            
            currentActives[timerSource] = timersActive;
            SaveActiveTimersInfo(currentActives);
        }
        public static bool GetTimersActive(string currentCharacter)
        {
            var activeInfo = GetAllTimersActiveInfo();
            if (!activeInfo.ContainsKey(currentCharacter))
            {
                activeInfo[currentCharacter] = false;
                SaveActiveTimersInfo(activeInfo);
            }
            return activeInfo[currentCharacter];
        }
        private static void SaveActiveTimersInfo(Dictionary<string,bool> activesInfo)
        {
            File.WriteAllText(activePath, JsonConvert.SerializeObject(activesInfo));
        }
        private static Dictionary<string,bool> GetAllTimersActiveInfo()
        {
            return JsonConvert.DeserializeObject<Dictionary<string, bool>>(File.ReadAllText(activePath));
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
                File.WriteAllText(activePath, JsonConvert.SerializeObject(new Dictionary<string, bool>()));
            }
            CorrectBossSourceNames();
        }

        private static void CorrectBossSourceNames()
        {
            var stringInfo = File.ReadAllText(infoPath);

            var currentDefaults = JsonConvert.DeserializeObject<List<DefaultTimersData>>(stringInfo);
            foreach (var mech in currentDefaults)
            {
                var parts = mech.TimerSource.Split("|");
                if (parts.Length == 2 || parts.Length == 1)
                    continue;
                mech.TimerSource = string.Join("|", parts[0], parts[1]);
            }
            File.WriteAllText(infoPath, JsonConvert.SerializeObject(currentDefaults));
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
        public static void AddSource(DefaultTimersData source)
        {
            if (source.TimerSource == "")
                return;
            var defaults = GetAllDefaults();
            if (defaults.Any(t => t.TimerSource == source.TimerSource))
            {
                var sourceToUpdate = defaults.First(t => t.TimerSource == source.TimerSource);
                foreach (var timer in source.Timers)
                {
                    if(sourceToUpdate.Timers.Any(t=>t.Id == timer.Id) || timer.Id == "")
                        continue;
                    sourceToUpdate.Timers.Add(timer);
                }
            }
            else
                defaults.Add(source);
            File.WriteAllText(infoPath, JsonConvert.SerializeObject(defaults));
        }

        public static void ClearBuiltinMechanics(int currentrev)
        {
            var allTimers = GetAllDefaults();
            allTimers.RemoveAll(s => s.Timers.Any(t => t.IsBuiltInMechanic) || s.Timers.Any(t=>t.BuiltInMechanicRev < currentrev && !(t.IsHot || t.IsBuiltInDot || t.BuiltInMechanicRev == 0)));
            File.WriteAllText(infoPath, JsonConvert.SerializeObject(allTimers));
        }
        public static void ResetTimersForSource(string source)
        {
            var currentDefaults = GetDefaults(source);
            currentDefaults.Timers.Clear();
            SaveResults(source, currentDefaults);
        }
        public static void AddTimersForSource(List<Timer> timers, string source)
        {
            var currentDefaults = GetDefaults(source);
            foreach (var timer in timers)
            {
                if (currentDefaults.Timers.Any(t => t.Id == timer.Id))
                    return;
                currentDefaults.Timers.Add(timer);
            }

            SaveResults(source, currentDefaults);
        }
        public static void RemoveTimerForCharacter(Timer timer, string character)
        {
            var currentDefaults = GetAllDefaults();
            var valueToRemove = currentDefaults.SelectMany(s=>s.Timers).First(t => TimerEquality.Equals(timer, t));
            foreach(var source in currentDefaults)
            {
                source.Timers.Remove(valueToRemove);
            }
            File.WriteAllText(infoPath, JsonConvert.SerializeObject(currentDefaults));
        }
        public static void SetTimerEnabled(bool state, Timer timer)
        {
            var currentDefaults = GetDefaults(timer.TimerSource);
            var timerToModify = currentDefaults.Timers.First(t => t.Id == timer.Id);
            timerToModify.IsEnabled = state;
            SaveResults(timerToModify.TimerSource, currentDefaults);
        }
        public static void SetTimerAudio(bool state, Timer timer)
        {
            var currentDefaults = GetDefaults(timer.TimerSource);
            var timerToModify = currentDefaults.Timers.First(t => t.Id == timer.Id);
            timerToModify.UseAudio = state;
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
            catch (Exception)
            {
                InitializeDefaults(timerSource);
                var resetDefaults = File.ReadAllText(infoPath);
                return JsonConvert.DeserializeObject<List<DefaultTimersData>>(resetDefaults).First(t=>t.TimerSource == timerSource);
            }
        }
        public static List<DefaultTimersData> GetAllMechanicsDefaults()
        {
            return GetAllDefaults().Where(d => d.IsBossSource).ToList();
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
                var validDefaults = currentDefaults.Where(c => validSources.Contains(c.TimerSource) || c.TimerSource == "Shared" || c.TimerSource == "HOTS"|| c.TimerSource == "DOTS" || c.IsBossSource).ToList();
                return validDefaults;
            }
            catch(JsonSerializationException)
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
            var validDefaults = currentDefaults.Where(c => validSources.Contains(c.TimerSource) || c.TimerSource == "Shared" || c.TimerSource == "HOTS" || c.TimerSource == "DOTS" || c.IsBossSource);

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

            var defaults = new DefaultTimersData() {TimerSource = timerSource, Position = new Point(0, 0), WidtHHeight = new Point(300, 200)};
            if (timerSource.Contains("|"))
                defaults.IsBossSource = true;
            currentDefaults.Add(defaults);
            var classes = ClassLoader.LoadAllClasses();
            var validSources = classes.Select(c => c.Discipline);
            var validDefaults = currentDefaults.Where(c => validSources.Contains(c.TimerSource) || c.TimerSource == "Shared" || c.TimerSource == "HOTS" || c.TimerSource == "DOTS" || c.IsBossSource);
            File.WriteAllText(infoPath, JsonConvert.SerializeObject(validDefaults));
        }
    }
}
