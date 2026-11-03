using Newtonsoft.Json;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser.Model.CloudRaiding
{
    public class ChallengeDatabaseAccess
    {        
        private static JsonSerializerSettings _settings = new JsonSerializerSettings
        {
            Culture = CultureInfo.InvariantCulture
        };
        private static string _apiPath => DatabaseIPGetter.CurrentAPIURL();
        public static async Task<List<string>> GetAllChallengeIds()
        {
            if (Settings.ReadSettingOfType<bool>("offline_mode"))
                return new List<string>();
            try
            {
                using (HttpClient connection = new HttpClient())
                {
                    Uri uri = new Uri($"{_apiPath}/challenges/getAll");
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
        public static async Task AddChallenge(DataStructures.Challenge newTimer)
        {
            if (Settings.ReadSettingOfType<bool>("offline_mode"))
            {
                return;
            }
                try
            {
                using (HttpClient connection = new HttpClient())
                {
                    Uri uri = new Uri($"{_apiPath}/challenges/add");
                    var str = JsonConvert.SerializeObject(newTimer, _settings);
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
        public static async Task<DataStructures.Challenge> GetChallengeFromId(string timerId)
        {
            if (Settings.ReadSettingOfType<bool>("offline_mode"))
            {
                return new DataStructures.Challenge();
            }
            try
            {
                using (HttpClient connection = new HttpClient())
                {
                    Uri uri = new Uri($"{_apiPath}/challenges/get?challengeId={timerId}");
                    var response = await connection.GetAsync(uri);
                    return await response.Content.ReadFromJsonAsync<DataStructures.Challenge> ();
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
