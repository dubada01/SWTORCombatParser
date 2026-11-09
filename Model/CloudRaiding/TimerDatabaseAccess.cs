using Newtonsoft.Json;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using Timer = SWTORCombatParser.DataStructures.Timer;

namespace SWTORCombatParser.Model.CloudRaiding
{
    public static class TimerDatabaseAccess
    {
        private static string _apiPath => DatabaseIPGetter.CurrentAPIURL();
        private static readonly HttpClient _client = OfflineHttpHandler.CreateClient();
        public static async Task<List<string>> GetAllTimerIds()
        {
            if (Settings.ReadSettingOfType<bool>("offline_mode"))
                return new List<string>();
            try
            {
                Uri uri = new Uri($"{_apiPath}/timers/getAll");
                using var response = await _client.GetAsync(uri);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<List<string>>() ?? new List<string>();
            }
            catch (Exception e)
            {
                Logging.LogError(e.Message);
                return new List<string>();
            }
        }
        public static async Task AddTimer(Timer newTimer)
        {
            if (Settings.ReadSettingOfType<bool>("offline_mode"))
                return;
            try
            {
                Uri uri = new Uri($"{_apiPath}/timers/add");
                var str = JsonConvert.SerializeObject(newTimer);
                using var content = new StringContent(str, Encoding.UTF8, "application/json");
                using var response = await _client.PostAsync(uri, content);
                response.EnsureSuccessStatusCode();
                return;
            }
            catch (Exception e)
            {
                Logging.LogError(e.Message);
                return;
            }
        }
        public static async Task<Timer> GetTimerFromId(string timerId)
        {
            if (Settings.ReadSettingOfType<bool>("offline_mode"))
                return null;
            try
            {
                Uri uri = new Uri($"{_apiPath}/timers/get?timerId={Uri.EscapeDataString(timerId)}");
                using var response = await _client.GetAsync(uri);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<Timer>();
            }
            catch (Exception e)
            {
                Logging.LogError(e.Message);
                return null;
            }
        }
    }
}
