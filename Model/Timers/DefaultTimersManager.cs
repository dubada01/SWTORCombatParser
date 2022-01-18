using Newtonsoft.Json;
using SWTORCombatParser.DataStructures;
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
    public static class DefaultTimersManager
    {
        private static string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "DubaTech", "SWTORCombatParser");
        private static string infoPath = Path.Combine(appDataPath, "character_timers_info.json");
        public static void Init()
        {
            if (!Directory.Exists(appDataPath))
                Directory.CreateDirectory(appDataPath);
            if (!File.Exists(infoPath))
            {
                File.WriteAllText(infoPath, JsonConvert.SerializeObject(new Dictionary<string, DefaultTimersData>()));
            }
        }
        public static Timer GetTimerById(string id)
        {
            var allTimers = GetAllDefaults().Values.SelectMany(d=>d.Timers);
            return allTimers.FirstOrDefault(t => t.Id == id);
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
        public static void AddTimerForCharacter(Timer timer, string character)
        {
            var currentDefaults = GetDefaults(character);
            currentDefaults.Timers.Add(timer);
            SaveResults(character, currentDefaults);
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
                return currentDefaults[characterName];
            }
            catch (Exception e)
            {
                InitializeDefaults(characterName);
                var resetDefaults = File.ReadAllText(infoPath);
                return JsonConvert.DeserializeObject<Dictionary<string, DefaultTimersData>>(resetDefaults)[characterName];
            }

        }
        public static Dictionary<string,DefaultTimersData> GetAllDefaults()
        {
            var stringInfo = File.ReadAllText(infoPath);
            var currentDefaults = JsonConvert.DeserializeObject<Dictionary<string, DefaultTimersData>>(stringInfo);
            return currentDefaults;
        }
        private static void SaveResults(string character, DefaultTimersData data)
        {
            var stringInfo = File.ReadAllText(infoPath);
            var currentDefaults = JsonConvert.DeserializeObject<Dictionary<string, DefaultTimersData>>(stringInfo);
            currentDefaults[character] = data;
            File.WriteAllText(infoPath, JsonConvert.SerializeObject(currentDefaults));
        }
        private static void InitializeDefaults(string characterName)
        {
            var stringInfo = File.ReadAllText(infoPath);
            var currentDefaults = JsonConvert.DeserializeObject<Dictionary<string, DefaultTimersData>>(stringInfo);

            var defaults = new DefaultTimersData() { Position = new Point(0, 0), WidtHHeight = new Point(100, 200), Acive = true };

            currentDefaults[characterName] = defaults;
            File.WriteAllText(infoPath, JsonConvert.SerializeObject(currentDefaults));
        }
    }
}
