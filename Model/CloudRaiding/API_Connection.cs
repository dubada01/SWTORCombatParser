//using MoreLinq;
using Newtonsoft.Json;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace SWTORCombatParser.Model.CloudRaiding
{
    public static class API_Connection
    {
        private static string _apiPath => DatabaseIPGetter.CurrentAPIURL();
        public static async Task<int> GetCurrentLeaderboardVersion()
        {
            if (Settings.ReadSettingOfType<bool>("offline_mode"))
                return 0;
            try
            {
                using (HttpClient connection = new HttpClient())
                {
                    Uri uri = new Uri($"{_apiPath}/leaderboard/version");
                    var response = await connection.GetAsync(uri);
                    var body = await response.Content.ReadFromJsonAsync<int>();
                    return body;
                }
            }
            catch (Exception e)
            {
                Logging.LogError(e.Message);
                return 0;
            }
        }
        public static async Task<bool> TryAddLeaderboardEntries(List<LeaderboardEntry> newEntry)
        {
            if (newEntry.Count == 0 || Settings.ReadSettingOfType<bool>("offline_mode"))
                return false;
            try
            {
                using (HttpClient connection = new HttpClient())
                {
                    Uri uri = new Uri($"{_apiPath}/leaderboard/addMany");
                    var response = await connection.PostAsJsonAsync(uri, newEntry);
                    var body = await response.Content.ReadFromJsonAsync<bool>();
                    return body;
                }
            }
            catch (Exception e)
            {
                Logging.LogError(e.Message);
                return false;
            }
        }
        public static async Task<bool> TryAddBossEncounter(GameEncounter gameEncounter)
        {
            if (Settings.ReadSettingOfType<bool>("offline_mode"))
                return false;
            try
            {
                using (HttpClient connection = new HttpClient())
                {
                    Uri uri = new Uri($"{_apiPath}/stats/encounter/add");
                    var response = await connection.PostAsJsonAsync(uri, gameEncounter);
                    var body = await response.Content.ReadFromJsonAsync<bool>();
                    return body;
                }
            }
            catch (Exception e)
            {
                Logging.LogError(e.Message);
                return false;
            }
        }
        public static async Task<Version> GetMostRecentVersion()
        {
            if (Settings.ReadSettingOfType<bool>("offline_mode"))
                return new Version();
            try
            {
                using (HttpClient connection = new HttpClient())
                {
                    Uri uri = new Uri($"{_apiPath}/SoftwareVersion");
                    var response = await connection.GetAsync(uri);
                    var body = await response.Content.ReadFromJsonAsync<Version>();
                    return body;
                }
            }
            catch (Exception e)
            {
                Logging.LogError(e.Message);
                return new Version();
            }
        }
        public static async Task<LeaderboardTop> GetTopBossEntry(string bossName, string encounter, LeaderboardEntryType entryType, string className, bool filterClass)
        {
            if (Settings.ReadSettingOfType<bool>("offline_mode") || string.IsNullOrEmpty(bossName))
                return new LeaderboardTop();
            try
            {
                using (HttpClient connection = new HttpClient())
                {

                    Uri uri = new Uri($"{_apiPath}/leaderboard/getTopEntryForLeaderboard");
                    var str = JsonConvert.SerializeObject(new List<string> { bossName, encounter, entryType.ToString(), className, filterClass.ToString() });
                    var content = new StringContent(str, Encoding.UTF8, "application/json");
                    var response = await connection.PostAsync(uri, content);
                    var body = await response.Content.ReadFromJsonAsync<LeaderboardTop>();
                    return body;
                }
            }
            catch (Exception e)
            {
                Logging.LogError(e.Message);
                return new LeaderboardTop();
            }
        }
        public static async Task<int[]> GetLeaderboardPercentiles(string bossName, string encounter, LeaderboardEntryType entryType)
        {
            if (Settings.ReadSettingOfType<bool>("offline_mode") || string.IsNullOrEmpty(bossName))
                return new int[100];
            try
            {
                using (HttpClient connection = new HttpClient())
                {

                    Uri uri = new Uri($"{_apiPath}/leaderboard/getAllPercentileForBoss");
                    var str = JsonConvert.SerializeObject(new List<string> { bossName, encounter, entryType.ToString()});
                    var content = new StringContent(str, Encoding.UTF8, "application/json");
                    var response = await connection.PostAsync(uri, content);
                    var body = await response.Content.ReadFromJsonAsync<int[]>();
                    return body;
                }
            }
            catch (Exception e)
            {
                Logging.LogError(e.Message);
                return new int[100];
            }
        }
        public static async Task<int[]> GetLeaderboardPercentilesForRole(string bossName, string encounter, LeaderboardEntryType entryType, string role)
        {
            if (Settings.ReadSettingOfType<bool>("offline_mode") || string.IsNullOrEmpty(bossName))
                return new int[100];
            try
            {
                using (HttpClient connection = new HttpClient())
                {

                    Uri uri = new Uri($"{_apiPath}/leaderboard/getAllPercentileForBossForRole");
                    var str = JsonConvert.SerializeObject(new List<string> { bossName, encounter, entryType.ToString(), role });
                    var content = new StringContent(str, Encoding.UTF8, "application/json");
                    var response = await connection.PostAsync(uri, content);
                    var body = await response.Content.ReadFromJsonAsync<int[]>();
                    return body;
                }
            }
            catch (Exception e)
            {
                Logging.LogError(e.Message);
                return new int[100];
            }
        }
        public static async Task<int[]> GetLeaderboardPercentilesForDiscipline(string bossName, string encounter, LeaderboardEntryType entryType, string discipline)
        {
            if (Settings.ReadSettingOfType<bool>("offline_mode") || string.IsNullOrEmpty(bossName))
                return new int[100];
            try
            {
                using (HttpClient connection = new HttpClient())
                {

                    Uri uri = new Uri($"{_apiPath}/leaderboard/getAllPercentileForBossForDiscipline");
                    var str = JsonConvert.SerializeObject(new List<string> { bossName, encounter, entryType.ToString(), discipline });
                    var content = new StringContent(str, Encoding.UTF8, "application/json");
                    var response = await connection.PostAsync(uri, content);
                    var body = await response.Content.ReadFromJsonAsync<int[]>();
                    return body;
                }
            }
            catch (Exception e)
            {
                Logging.LogError(e.Message);
                return new int[100];
            }
        }
        public static async Task<List<TimeTrialLeaderboardEntry>> GetTimeTrialEntriesForBoss(
            string bossName,
            string encounterName,
            string difficulty,
            string playerCount)
        {
            List<TimeTrialLeaderboardEntry> entriesFound = new List<TimeTrialLeaderboardEntry>();
            if (Settings.ReadSettingOfType<bool>("offline_mode") || string.IsNullOrEmpty(bossName))
                return entriesFound;

            try
            {
                using (HttpClient connection = new HttpClient())
                {
                    Uri uri = new Uri($"{_apiPath}/trial_leaderboard/getEntriesForBoss" +
                                      $"?bossfightName={HttpUtility.UrlEncode(bossName)}" +
                                      $"&encounter={HttpUtility.UrlEncode(encounterName)}" +
                                      $"&difficulty={HttpUtility.UrlEncode(difficulty)}" +
                                      $"&playerCount={HttpUtility.UrlEncode(playerCount)}");

                    var response = await connection.GetAsync(uri);

                    // Check CloudFront cache status
                    if (response.Headers.TryGetValues("X-Cache", out var values))
                    {
                        string xCache = string.Join(",", values);
                        if (xCache.Contains("Hit", StringComparison.OrdinalIgnoreCase))
                            Logging.LogInfo($"CloudFront cache HIT: {xCache}");
                        else if (xCache.Contains("Miss", StringComparison.OrdinalIgnoreCase))
                            Logging.LogInfo($"CloudFront cache MISS: {xCache}");
                        else
                            Logging.LogInfo($"CloudFront cache status: {xCache}");
                    }
                    else
                    {
                        Logging.LogInfo("CloudFront cache header not present.");
                    }

                    var body = await response.Content.ReadFromJsonAsync<List<TimeTrialLeaderboardEntry>>();
                    return body ?? entriesFound;
                }
            }
            catch (Exception e)
            {
                Logging.LogError(e.Message);
                return entriesFound;
            }
        }
        public static async Task AddNewTimeTrialEntry(TimeTrialLeaderboardEntry entry)
        {
            if (Settings.ReadSettingOfType<bool>("offline_mode"))
                return;
            try
            {
                using (HttpClient connection = new HttpClient())
                {
                    Uri uri = new Uri($"{_apiPath}/trial_leaderboard/add");
                    var str = JsonConvert.SerializeObject(entry);
                    var content = new StringContent(str, Encoding.UTF8, "application/json");
                    await connection.PostAsync(uri, content);
                }
            }
            catch (Exception e)
            {
                Logging.LogError(e.Message);
            }
        }
    }
}
