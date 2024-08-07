using Newtonsoft.Json;
using System;
using System.IO;

namespace SWTORCombatParser.Model.CloudRaiding
{
    public static class LeaderboardSettings
    {
        private static string appDataPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DubaTech", "SWTORCombatParser");
        private static string _leaderboardSettingsPath => Path.Combine(appDataPath, "leaderboard_settings.json");

        public static void SaveLeaderboardSettings(LeaderboardType setting)
        {
            if (!File.Exists(_leaderboardSettingsPath))
            {
                var file = File.Create(_leaderboardSettingsPath);
                file.Close();
            }
            File.WriteAllText(_leaderboardSettingsPath, JsonConvert.SerializeObject(setting));
        }
        public static LeaderboardType ReadLeaderboardSettings()
        {
            try
            {
                if (!File.Exists(_leaderboardSettingsPath))
                {
                    var file = File.Create(_leaderboardSettingsPath);
                    file.Close();
                    File.WriteAllText(_leaderboardSettingsPath, JsonConvert.SerializeObject(LeaderboardType.Off));
                }

                var currentLeaderboardSetting = JsonConvert.DeserializeObject<LeaderboardType>(File.ReadAllText(_leaderboardSettingsPath));
                return currentLeaderboardSetting;
            }
            catch(Exception e)
            {
                File.WriteAllText(_leaderboardSettingsPath, JsonConvert.SerializeObject(LeaderboardType.Off));
                return LeaderboardType.Off;
            }
        }
    }
}
