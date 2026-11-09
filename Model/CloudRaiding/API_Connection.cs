//using MoreLinq;
using Newtonsoft.Json;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace SWTORCombatParser.Model.CloudRaiding
{
    public static partial class API_Connection
    {
        private static string _apiPath => DatabaseIPGetter.CurrentAPIURL();
        private static readonly HttpClient _client = OfflineHttpHandler.CreateClient(new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
        });
        public static async Task<int> GetCurrentLeaderboardVersion()
        {
            if (Settings.ReadSettingOfType<bool>("offline_mode"))
                return 0;
            try
            {
                Uri uri = new Uri($"{_apiPath}/leaderboard/version");
                using var response = await _client.GetAsync(uri);
                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadFromJsonAsync<int>();
                return body;
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
                Uri uri = new Uri($"{_apiPath}/leaderboard/addMany");
                using var response = await _client.PostAsJsonAsync(uri, newEntry);
                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadFromJsonAsync<bool>();
                return body;
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
                Uri uri = new Uri($"{_apiPath}/stats/encounter/add");
                using var response = await _client.PostAsJsonAsync(uri, gameEncounter);
                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadFromJsonAsync<bool>();
                return body;
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
                Uri uri = new Uri($"{_apiPath}/SoftwareVersion");
                using var response = await _client.GetAsync(uri);
                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadFromJsonAsync<Version>();
                return body ?? new Version();
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

                Uri uri = new Uri($"{_apiPath}/leaderboard/getTopEntryForLeaderboard");
                var str = JsonConvert.SerializeObject(new List<string> { bossName, encounter, entryType.ToString(), className, filterClass.ToString() });
                using var content = new StringContent(str, Encoding.UTF8, "application/json");
                using var response = await _client.PostAsync(uri, content);
                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadFromJsonAsync<LeaderboardTop>();
                return body ?? new LeaderboardTop();
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
                return Array.Empty<int>();
            try
            {

                Uri uri = new Uri($"{_apiPath}/leaderboard/getAllPercentileForBoss");
                var str = JsonConvert.SerializeObject(new List<string> { bossName, encounter, entryType.ToString()});
                using var content = new StringContent(str, Encoding.UTF8, "application/json");
                using var response = await _client.PostAsync(uri, content);
                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadFromJsonAsync<int[]>();
                return body ?? Array.Empty<int>();
            }
            catch (Exception e)
            {
                Logging.LogError(e.Message);
                return Array.Empty<int>();
            }
        }
        public static async Task<int[]> GetLeaderboardPercentilesForRole(string bossName, string encounter, LeaderboardEntryType entryType, string role)
        {
            if (Settings.ReadSettingOfType<bool>("offline_mode") || string.IsNullOrEmpty(bossName))
                return Array.Empty<int>();
            try
            {

                Uri uri = new Uri($"{_apiPath}/leaderboard/getAllPercentileForBossForRole");
                var str = JsonConvert.SerializeObject(new List<string> { bossName, encounter, entryType.ToString(), role });
                using var content = new StringContent(str, Encoding.UTF8, "application/json");
                using var response = await _client.PostAsync(uri, content);
                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadFromJsonAsync<int[]>();
                return body ?? Array.Empty<int>();
            }
            catch (Exception e)
            {
                Logging.LogError(e.Message);
                return Array.Empty<int>();
            }
        }
        public static async Task<int[]> GetLeaderboardPercentilesForDiscipline(string bossName, string encounter, LeaderboardEntryType entryType, string discipline)
        {
            if (Settings.ReadSettingOfType<bool>("offline_mode") || string.IsNullOrEmpty(bossName))
                return Array.Empty<int>();
            try
            {

                Uri uri = new Uri($"{_apiPath}/leaderboard/getAllPercentileForBossForDiscipline");
                var str = JsonConvert.SerializeObject(new List<string> { bossName, encounter, entryType.ToString(), discipline });
                using var content = new StringContent(str, Encoding.UTF8, "application/json");
                using var response = await _client.PostAsync(uri, content);
                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadFromJsonAsync<int[]>();
                return body ?? Array.Empty<int>();
            }
            catch (Exception e)
            {
                Logging.LogError(e.Message);
                return Array.Empty<int>();
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
                Uri uri = new Uri($"{_apiPath}/trial_leaderboard/getEntriesForBossFull" +
                                  $"?bossfightName={HttpUtility.UrlEncode(bossName)}" +
                                  $"&encounter={HttpUtility.UrlEncode(encounterName)}" +
                                  $"&difficulty={HttpUtility.UrlEncode(difficulty)}" +
                                  $"&playerCount={HttpUtility.UrlEncode(playerCount)}");

                using var response = await _client.GetAsync(uri);
                response.EnsureSuccessStatusCode();

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
                Uri uri = new Uri($"{_apiPath}/trial_leaderboard/add");
                var str = JsonConvert.SerializeObject(entry);
                using var content = new StringContent(str, Encoding.UTF8, "application/json");
                using var response = await _client.PostAsync(uri, content);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception e)
            {
                Logging.LogError(e.Message);
            }
        }
    }
}
