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
        public static async Task<List<string>> GetAllTimerIds()
        {
            if (Settings.ReadSettingOfType<bool>("offline_mode"))
                return new List<string>();
            try
            {
                using (HttpClient connection = new HttpClient())
                {
                    Uri uri = new Uri($"{_apiPath}/timers/getAll");
                    var response = await connection.GetAsync(uri);
                    return await response.Content.ReadFromJsonAsync<List<string>>();
                }
            }
            catch (Exception e)
            {
                Logging.LogError(e.Message);
                return new List<string>();
            }
        }
        public static async Task AddTimer(Timer newTimer)
        {
            try
            {
                using (HttpClient connection = new HttpClient())
                {
                    Uri uri = new Uri($"{_apiPath}/timers/add");
                    var str = JsonConvert.SerializeObject(newTimer);
                    var content = new StringContent(str, Encoding.UTF8, "application/json");
                    var response = await connection.PostAsync(uri, content);
                    response.EnsureSuccessStatusCode();
                    return;
                }
            }
            catch (Exception e)
            {
                Logging.LogError(e.Message);
                return;
            }
        }
        public static async Task<Timer> GetTimerFromId(string timerId)
        {
            try
            {
                using (HttpClient connection = new HttpClient())
                {
                    Uri uri = new Uri($"{_apiPath}/timers/get?timerId={timerId}");
                    var response = await connection.GetAsync(uri);
                    return await response.Content.ReadFromJsonAsync<Timer>();
                }
            }
            catch (Exception e)
            {
                Logging.LogError(e.Message);
                return null;
            }
        }
    }
}
