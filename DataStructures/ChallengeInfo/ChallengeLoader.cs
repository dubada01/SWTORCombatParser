using System;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SWTORCombatParser.Model.Challenge;

namespace SWTORCombatParser.DataStructures.ChallengeInfo
{
    internal class ChallengeLoader
    {
        public static void TryLoadChallenges()
        {
            List<DefaultChallengeData> challengeData = new List<DefaultChallengeData>();

            var builtInChallenges = JsonConvert.DeserializeObject<JArray>(File.ReadAllText(Path.Combine( Environment.CurrentDirectory, @"DataStructures/ChallengeInfo/BuiltInChallenges.json")));
            var bossTimerDeserialized = builtInChallenges.ToObject<List<DefaultChallengeData>>();
            challengeData.AddRange(bossTimerDeserialized);

            var builtInRev = challengeData.Any() ? challengeData.First().Challenges.First().BuiltInRev : 0;

            DefaultChallengeManager.ClearBuiltInChallenges(builtInRev);
            var currentBossTimers = DefaultChallengeManager.GetAllDefaults();

            var sourcesToAdd = new List<DefaultChallengeData>();
            foreach (var source in challengeData)
            {
                if (source.Challenges.Count == 0)
                    continue;
                foreach (var timer in source.Challenges)
                {
                    timer.IsBuiltIn = true;
                }
                sourcesToAdd.Add(source);
            }
            DefaultChallengeManager.AddSources(sourcesToAdd);
        }
    }
}
