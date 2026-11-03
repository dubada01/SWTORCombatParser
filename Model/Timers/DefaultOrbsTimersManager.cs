using Newtonsoft.Json;
using SWTORCombatParser.DataStructures.ClassInfos;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Utilities;
using Timer = SWTORCombatParser.DataStructures.Timer;

namespace SWTORCombatParser.Model.Timers
{
    public class DefaultTimersData
    {
        public string TimerSource;
        public bool IsBossSource;
        [JsonConverter(typeof(AvaloniaPointConverter))]
        public Point Position;
        [JsonConverter(typeof(AvaloniaPointConverter))]
        public Point WidtHHeight;

        public List<Timer> Timers = new List<Timer>();
    }
    public static class DefaultOrbsTimersManager
    {
        private static string _currentDefaults = "";
        private static string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DubaTech", "SWTORCombatParser");

        private static string infoPath = Path.Combine(appDataPath, "timers_info_v3.json");
        private static string activePath = Path.Combine(appDataPath, "timers_active_v2.json");
        private static List<DefaultTimersData> _defaults;

        public static void UpdateTimersActive(bool timersActive, string timerSource)
        {
            var currentActives = GetAllTimersActiveInfo();
            if (currentActives != null)
            {
                currentActives[timerSource] = timersActive;
                SaveActiveTimersInfo(currentActives);
            }
        }
        public static bool GetTimersActive(string currentCharacter)
        {
            var activeInfo = GetAllTimersActiveInfo();
            if (activeInfo != null && activeInfo.TryAdd(currentCharacter, false))
            {
                SaveActiveTimersInfo(activeInfo);
            }
            return activeInfo != null && activeInfo[currentCharacter];
        }
        private static void SaveActiveTimersInfo(Dictionary<string, bool> activesInfo)
        {
            File.WriteAllText(activePath, JsonConvert.SerializeObject(activesInfo));
        }
        private static Dictionary<string, bool>? GetAllTimersActiveInfo()
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
                    if (sourceToUpdate.Timers.Any(t => t.Id == timer.Id) || timer.Id == "")
                        continue;
                    sourceToUpdate.Timers.Add(timer);
                }
            }
            else
                defaults.Add(source);
            UpdateConfig(JsonConvert.SerializeObject(defaults));
        }
        public static void AddSources(List<DefaultTimersData> sources)
        {
            var defaults = GetAllDefaults();
            foreach(var source in sources)
            {
                if (source.TimerSource == "")
                    return;

                if (defaults.Any(t => t.TimerSource == source.TimerSource))
                {
                    var sourceToUpdate = defaults.First(t => t.TimerSource == source.TimerSource);
                    foreach (var timer in source.Timers)
                    {
                        if (sourceToUpdate.Timers.Any(t => t.Id == timer.Id) || timer.Id == "")
                            continue;
                        sourceToUpdate.Timers.Add(timer);
                    }
                }
                else
                    defaults.Add(source);
            }
            UpdateConfig(JsonConvert.SerializeObject(defaults));
        }
        public static void ClearBuiltinMechanics(int currentrev)
        {
            var allTimers = GetAllDefaults();
            foreach (var source in allTimers)
            {
                source.Timers.RemoveAll(t => t.IsMechanic && t.TimerRev < currentrev && (!t.IsUserAddedTimer || t.TimerRev != 0) && !t.IsHot && !t.IsBuiltInDot && !t.IsBuiltInDefensive && !t.IsBuiltInDot && !t.IsImportedFromSP);
            }
            UpdateConfig(JsonConvert.SerializeObject(allTimers));
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
                {
                    continue;
                }
                currentDefaults.Timers.Add(timer);
            }

            SaveResults(source, currentDefaults);
        }
        public static void RemoveTimerForCharacter(Timer timer, string character)
        {
            var currentDefaults = GetAllDefaults();
            var valueToRemove = currentDefaults.SelectMany(s => s.Timers).FirstOrDefault(t => TimerEquality.Equals(timer, t));
            if (valueToRemove == null)
                return;
            foreach (var source in currentDefaults)
            {
                source.Timers.Remove(valueToRemove);
            }
            UpdateConfig(JsonConvert.SerializeObject(currentDefaults));
        }
        public static void SetTimerEnabled(bool state, Timer timer)
        {
            var currentDefaults = GetDefaults(timer.TimerSource);
            var timerToModify = currentDefaults.Timers.First(t => t.Id == timer.Id);
            timerToModify.IsEnabled = state;
            SaveResults(timerToModify.TimerSource, currentDefaults);
        }
        public static void SetTimerVisibility(bool state, Timer timer)
        {
            var currentDefaults = GetDefaults(timer.TimerSource);
            var timerToModify = currentDefaults.Timers.First(t => t.Id == timer.Id);
            timerToModify.IsSubTimer = state;
            SaveResults(timerToModify.TimerSource, currentDefaults);
        }
        public static void SetTimerAudio(bool state, Timer timer)
        {
            var currentDefaults = GetDefaults(timer.TimerSource);
            var timerToModify = currentDefaults.Timers.First(t => t.Id == timer.Id);
            timerToModify.UseAudio = state;
            SaveResults(timerToModify.TimerSource, currentDefaults);
        }
        public static void SetTimersAudioForSource(List<Timer> timers, string source)
        {
            var currentDefaults = GetDefaults(source);
            foreach (var timer in currentDefaults.Timers)
            {
                timer.UseAudio = timers.First(t => t.Id == timer.Id).UseAudio;
            }
            //var timerToModify = currentDefaults.Timers.First(t => t.Id == timer.Id);
            //timerToModify.UseAudio = state;
            SaveResults(source, currentDefaults);
        }
        public static void SetTimersVisibilityForSource(List<Timer> timers, string source)
        {
            var currentDefaults = GetDefaults(source);
            foreach (var timer in currentDefaults.Timers)
            {
                timer.IsSubTimer = timers.First(t => t.Id == timer.Id).IsSubTimer;
            }
            //var timerToModify = currentDefaults.Timers.First(t => t.Id == timer.Id);
            //timerToModify.UseAudio = state;
            SaveResults(source, currentDefaults);
        }
        public static void SetTimersEnabledForSource(List<Timer> timers, string source)
        {
            var currentDefaults = GetDefaults(source);
            foreach (var timer in currentDefaults.Timers)
            {
                timer.IsEnabled = timers.First(t => t.Id == timer.Id).IsEnabled;
            }
            SaveResults(source, currentDefaults);
        }
        public static DefaultTimersData GetDefaults(string timerSource)
        {
            var stringInfo = File.ReadAllText(infoPath);
            try
            {
                var currentDefaults = JsonConvert.DeserializeObject<List<DefaultTimersData>>(stringInfo);
                if (!currentDefaults.Any(c => c.TimerSource == timerSource))
                {
                    InitializeDefaults(timerSource);
                }


                var initializedInfo = File.ReadAllText(infoPath);
                currentDefaults = JsonConvert.DeserializeObject<List<DefaultTimersData>>(initializedInfo);
                return currentDefaults.First(t => t.TimerSource == timerSource);
            }
            catch (Exception)
            {
                InitializeDefaults(timerSource);
                var resetDefaults = File.ReadAllText(infoPath);
                return JsonConvert.DeserializeObject<List<DefaultTimersData>>(resetDefaults).First(t => t.TimerSource == timerSource);
            }
        }
        public static List<DefaultTimersData> GetAllMechanicsDefaults()
        {
            return GetAllDefaults().Where(d => d.IsBossSource).ToList();
        }
        public static List<DefaultTimersData> GetAllDefaults()
        {
            var stringInfo = File.ReadAllText(infoPath);
            if (_currentDefaults == stringInfo)
            {
                return _defaults;
            }
            else
            {
                _currentDefaults = stringInfo;
            }
            if (string.IsNullOrEmpty(stringInfo))
            {
                return new List<DefaultTimersData>();
            }
            try
            {
                var currentDefaults = JsonConvert.DeserializeObject<List<DefaultTimersData>>(stringInfo);
                var classes = ClassLoader.LoadAllClasses();
                var validSources = classes.Select(c => c.Discipline);
                var validDefaults = currentDefaults.Where(c => c != null && c.TimerSource != null && validSources.Contains(c.TimerSource) ||
                c.TimerSource == "Shared" ||
                c.TimerSource == "HOTS" ||
                c.TimerSource == "DOTS" ||
                c.TimerSource == "DCD" ||
                c.TimerSource == "OCD" ||
                c.IsBossSource ||
            c.TimerSource == "StarParse Import").ToList();
                _defaults = validDefaults;
                return validDefaults;
            }
            catch (JsonSerializationException)
            {
                //File.WriteAllText(infoPath, "");
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
            var validDefaults = currentDefaults.Where(c => validSources.Contains(c.TimerSource) ||
            c.TimerSource == "Shared" ||
            c.TimerSource == "HOTS" ||
            c.TimerSource == "DOTS" ||
            c.TimerSource == "DCD" ||
            c.TimerSource == "OCD" ||
            c.IsBossSource ||
            c.TimerSource == "StarParse Import");

            UpdateConfig(JsonConvert.SerializeObject(validDefaults));
        }
        private static void InitializeDefaults(string timerSource)
        {
            var stringInfo = File.ReadAllText(infoPath);
            var currentDefaults = new List<DefaultTimersData>();
            if (!string.IsNullOrEmpty(stringInfo))
            {
                currentDefaults = JsonConvert.DeserializeObject<List<DefaultTimersData>>(stringInfo);
            }

            var defaults = new DefaultTimersData() { TimerSource = timerSource, Position = new Point(0, 0), WidtHHeight = new Point(300, 200) };
            if (!string.IsNullOrEmpty(timerSource) && timerSource.Contains("|"))
                defaults.IsBossSource = true;
            currentDefaults.Add(defaults);
            var classes = ClassLoader.LoadAllClasses();
            var validSources = classes.Select(c => c.Discipline);
            var validDefaults = currentDefaults.Where(c => validSources.Contains(c.TimerSource) ||
            c.TimerSource == "Shared" ||
            c.TimerSource == "HOTS" ||
            c.TimerSource == "DOTS" ||
            c.TimerSource == "DCD" ||
            c.TimerSource == "OCD" ||
            c.IsBossSource ||
            c.TimerSource == "StarParse Import");
            UpdateConfig(JsonConvert.SerializeObject(validDefaults));
        }
        private static object fileModLock = new object();
        private static void UpdateConfig(string textToWrite)
        {
            string tempFileName = infoPath + ".temp";

            try
            {
                lock (fileModLock)
                {
                    File.WriteAllText(tempFileName, textToWrite);

                    // Replace the original file with the temporary file atomically
                    File.Move(tempFileName, infoPath, true);
                }
            }
            catch (Exception e)
            {
                // Handle the error, e.g., log it or inform the user
                Logging.LogError($"Error writing config to file: {e.Message}");
            }
        }
    }
}
