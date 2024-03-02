//using MoreLinq;
using Newtonsoft.Json;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

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
        public static async Task<List<string>> GetEncountersWithEntries()
        {
            List<string> entriesFound = new List<string>();
            if (Settings.ReadSettingOfType<bool>("offline_mode"))
                return entriesFound;
            try
            {
                using (HttpClient connection = new HttpClient())
                {
                    Uri uri = new Uri($"{_apiPath}/leaderboard/getEncounterWithEntries");
                    var response = await connection.GetAsync(uri);
                    var body = await response.Content.ReadFromJsonAsync<List<string>>();
                    return body;
                }
            }
            catch (Exception e)
            {
                Logging.LogError(e.Message);
                return entriesFound;
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
        public static async Task<List<string>> GetBossesFromEncounterWithEntries(string encounter)
        {
            List<string> bossesFound = new List<string>();
            if (Settings.ReadSettingOfType<bool>("offline_mode"))
                return bossesFound;
            try
            {
                using (HttpClient connection = new HttpClient())
                {
                    Uri uri = new Uri($"{_apiPath}/leaderboard/getBossesFromEncounterWithEntries?encounter={encounter}");
                    var response = await connection.GetAsync(uri);
                    var body = await response.Content.ReadFromJsonAsync<List<string>>();
                    return body;
                }
            }
            catch (Exception e)
            {
                Logging.LogError(e.Message);
                return bossesFound;
            }
        }

        public static async Task<List<LeaderboardEntry>> GetEntriesForBossOfType(string bossName, string encounter, LeaderboardEntryType entryType)
        {
            List<LeaderboardEntry> entriesFound = new List<LeaderboardEntry>();
            if (Settings.ReadSettingOfType<bool>("offline_mode") || string.IsNullOrEmpty(bossName))
                return entriesFound;
            try
            {
                using (HttpClient connection = new HttpClient())
                {

                    Uri uri = new Uri($"{_apiPath}/leaderboard/getEntriesForBossOfType");
                    var str = JsonConvert.SerializeObject(new List<string> { bossName, encounter, entryType.ToString() });
                    var content = new StringContent(str, Encoding.UTF8, "application/json");
                    var response = await connection.PostAsync(uri, content);
                    var body = await response.Content.ReadFromJsonAsync<List<LeaderboardEntry>>();
                    return body;
                }
            }
            catch (Exception e)
            {
                Logging.LogError(e.Message);
                return entriesFound;
            }
        }
    }
}
