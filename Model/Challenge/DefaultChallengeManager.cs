using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Utilities;


namespace SWTORCombatParser.Model.Challenge
{
    public class DefaultChallengeData
    {
        public string ChallengeSource;
        [JsonConverter(typeof(AvaloniaPointConverter))]
        public Point Position;
        [JsonConverter(typeof(AvaloniaPointConverter))]
        public Point WidtHHeight;

        public List<DataStructures.Challenge> Challenges = new List<DataStructures.Challenge>();
    }
    public static class DefaultChallengeManager
    {
        private static string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DubaTech", "SWTORCombatParser");

        private static string infoPath = Path.Combine(appDataPath, "challengeInfo.json");
        private static string activePath = Path.Combine(appDataPath, "challengeActive.json");
        private static object _fileLock = new object();
        private static JsonSerializerSettings _settings = new JsonSerializerSettings
        {
            Culture = CultureInfo.InvariantCulture
        };
        public static void UpdateChallengeActive(bool challengeActive, string source)
        {
            var currentActives = GetAllChallengeActive();

            currentActives[source] = challengeActive;
            SaveActiveChallengeInfo(currentActives);
        }
        public static bool GetChallengeActive(string source)
        {
            var activeInfo = GetAllChallengeActive();
            if (!activeInfo.ContainsKey(source))
            {
                activeInfo[source] = false;
                SaveActiveChallengeInfo(activeInfo);
            }
            return activeInfo[source];
        }
        private static void SaveActiveChallengeInfo(Dictionary<string, bool> activesInfo)
        {
            File.WriteAllText(activePath, JsonConvert.SerializeObject(activesInfo));
        }
        private static Dictionary<string, bool> GetAllChallengeActive()
        {
            return JsonConvert.DeserializeObject<Dictionary<string, bool>>(File.ReadAllText(activePath));
        }
        public static void SetIdForChallenge(DataStructures.Challenge timer, string source, string id)
        {
            var currentDefaults = GetDefaults(source);
            var valueToUpdate = currentDefaults.Challenges.First(t => timer.Id == t.Id);
            valueToUpdate.ShareId = id;
            SaveResults(source, currentDefaults);
        }
        public static void Init()
        {
            lock (_fileLock)
            {
                if (!Directory.Exists(appDataPath))
                    Directory.CreateDirectory(appDataPath);
                if (!File.Exists(infoPath))
                {
                    File.WriteAllText(infoPath, JsonConvert.SerializeObject(new List<DefaultChallengeData>()));
                }
                if (!File.Exists(activePath))
                {
                    File.WriteAllText(activePath, JsonConvert.SerializeObject(new Dictionary<string, bool>()));
                }
            }
        }

        public static void SetDefaults(Point position, Point widtHHeight, string characterName)
        {
            var currentDefaults = GetDefaults(characterName);
            currentDefaults.WidtHHeight = widtHHeight;
            currentDefaults.Position = position;
            SaveResults(characterName, currentDefaults);
        }
        public static void SetSavedChallenges(List<DataStructures.Challenge> currentChallenges, string character)
        {
            var currentDefaults = GetDefaults(character);
            currentDefaults.Challenges = currentChallenges;
            SaveResults(character, currentDefaults);
        }
        public static void AddSources(List<DefaultChallengeData> sources)
        {
            var defaults = GetAllDefaults();
            foreach (var source in sources)
            {
                if (source.ChallengeSource == "")
                    continue;
                if (defaults.Any(t => t.ChallengeSource == source.ChallengeSource))
                {
                    var sourceToUpdate = defaults.First(t => t.ChallengeSource == source.ChallengeSource);
                    foreach (var challenge in source.Challenges)
                    {
                        if (sourceToUpdate.Challenges.Any(t => t.Id == challenge.Id) || Guid.Empty == challenge.Id)
                            continue;
                        sourceToUpdate.Challenges.Add(challenge);
                    }
                }
                else
                    defaults.Add(source);
            }

            UpdateConfig(JsonConvert.SerializeObject(defaults, _settings));
        }
        public static void AddSource(DefaultChallengeData source)
        {
            if (source.ChallengeSource == "")
                return;
            var defaults = GetAllDefaults();
            if (defaults.Any(t => t.ChallengeSource == source.ChallengeSource))
            {
                var sourceToUpdate = defaults.First(t => t.ChallengeSource == source.ChallengeSource);
                foreach (var challenge in source.Challenges)
                {
                    if (sourceToUpdate.Challenges.Any(t => t.Id == challenge.Id) || Guid.Empty == challenge.Id)
                        continue;
                    sourceToUpdate.Challenges.Add(challenge);
                }
            }
            else
                defaults.Add(source);
            UpdateConfig(JsonConvert.SerializeObject(defaults, _settings));
        }

        public static void ResetChallengesForSouce(string source)
        {
            var currentDefaults = GetDefaults(source);
            currentDefaults.Challenges.Clear();
            SaveResults(source, currentDefaults);
        }
        public static void ClearBuiltInChallenges(int currentRev)
        {
            var currentDefaults = GetAllDefaults();
            foreach(var challengeSource in currentDefaults)
            {
                challengeSource.Challenges.RemoveAll(challenge => challenge.BuiltInRev < currentRev && challenge.IsBuiltIn);
            }
            SaveAllResults(currentDefaults);
        }
        public static void AddChallengesToSource(List<DataStructures.Challenge> challenges, string source)
        {
            var currentDefaults = GetDefaults(source);
            foreach (var challenge in challenges)
            {
                if (currentDefaults.Challenges.Any(t => t.Id == challenge.Id))
                    return;
                currentDefaults.Challenges.Add(challenge);
            }

            SaveResults(source, currentDefaults);
        }
        public static void RemoveChallengeFromSource(DataStructures.Challenge challenge)
        {
            var currentDefaults = GetAllDefaults();
            var valueToRemove = currentDefaults.SelectMany(s => s.Challenges).FirstOrDefault(t => t.Id == challenge.Id);
            if (valueToRemove == null)
            {
                return;
            }
            foreach (var source in currentDefaults)
            {
                source.Challenges.Remove(valueToRemove);
            }
            UpdateConfig(JsonConvert.SerializeObject(currentDefaults));
        }
        public static void SetChallengeEnabled(bool state, DataStructures.Challenge challenge)
        {
            var currentDefaults = GetDefaults(challenge.Source);
            var challengeToModify = currentDefaults.Challenges.First(t => t.Id == challenge.Id);
            challengeToModify.IsEnabled = state;
            SaveResults(challengeToModify.Source, currentDefaults);
        }
        public static DefaultChallengeData GetDefaults(string source)
        {
            lock (_fileLock)
            {
                var stringInfo = File.ReadAllText(infoPath);
                try
                {
                    var currentDefaults = JsonConvert.DeserializeObject<List<DefaultChallengeData>>(stringInfo);
                    if (!currentDefaults.Any(c => c.ChallengeSource == source))
                    {
                        InitializeDefaults(source);
                    }

                    var initializedInfo = File.ReadAllText(infoPath);
                    currentDefaults = JsonConvert.DeserializeObject<List<DefaultChallengeData>>(initializedInfo);
                    return currentDefaults.First(t => t.ChallengeSource == source);
                }
                catch (Exception)
                {
                    InitializeDefaults(source);
                    var resetDefaults = File.ReadAllText(infoPath);
                    return JsonConvert.DeserializeObject<List<DefaultChallengeData>>(resetDefaults).First(t => t.ChallengeSource == source);
                }
            }

        }
        public static List<DefaultChallengeData> GetAllDefaults()
        {
            lock(_fileLock)
            {
                var stringInfo = File.ReadAllText(infoPath);
                if (string.IsNullOrEmpty(stringInfo))
                {
                    return new List<DefaultChallengeData>();
                }
                try
                {
                    var currentDefaults = JsonConvert.DeserializeObject<List<DefaultChallengeData>>(stringInfo);
                    return currentDefaults;
                }
                catch (JsonSerializationException)
                {
                    File.WriteAllText(infoPath, "");
                    return new List<DefaultChallengeData>();
                }
            }

        }
        private static void SaveResults(string source, DefaultChallengeData data)
        {
            lock(_fileLock)
            {
                var stringInfo = File.ReadAllText(infoPath);
                var currentDefaults = JsonConvert.DeserializeObject<List<DefaultChallengeData>>(stringInfo);

                currentDefaults.Remove(currentDefaults.First(cd => cd.ChallengeSource == source));
                currentDefaults.Add(data);

                UpdateConfig(JsonConvert.SerializeObject(currentDefaults, _settings));
            }
        }
        private static void SaveAllResults(List<DefaultChallengeData> data)
        {
            UpdateConfig(JsonConvert.SerializeObject(data, _settings));
        }
        private static void InitializeDefaults(string source)
        {
            lock (_fileLock)
            {
                var stringInfo = File.ReadAllText(infoPath);
                var currentDefaults = new List<DefaultChallengeData>();
                if (!string.IsNullOrEmpty(stringInfo))
                {
                    currentDefaults = JsonConvert.DeserializeObject<List<DefaultChallengeData>>(stringInfo);
                }

                var defaults = new DefaultChallengeData() { ChallengeSource = source, Position = new Point(0, 0), WidtHHeight = new Point(300, 200) };
                currentDefaults.Add(defaults);
                UpdateConfig(JsonConvert.SerializeObject(currentDefaults));
            }

        }
        private static void UpdateConfig(string textToWrite)
        {
            lock ( _fileLock)
            {
                string tempFileName = infoPath + ".temp";

                try
                {
                    File.WriteAllText(tempFileName, textToWrite);

                    // Replace the original file with the temporary file atomically
                    File.Move(tempFileName, infoPath, true);
                }
                catch (Exception e)
                {
                    // Handle the error, e.g., log it or inform the user
                    Logging.LogError($"Error writing config to file: {e.Message}");
                }
            }

        }
    }
}
