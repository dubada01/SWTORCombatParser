using SWTORCombatParser.DataStructures.Updates;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Threading.Tasks;

namespace SWTORCombatParser.Model.CloudRaiding
{
    public static class MessageFetcher
    {
        private static string _apiPath => DatabaseIPGetter.CurrentAPIURL();
        public static async Task<List<UpdateMessage>> GetMessages()
        {
            if (Settings.ReadSettingOfType<bool>("offline_mode"))
                return new List<UpdateMessage>();
            try
            {
                using (HttpClient connection = new HttpClient())
                {
                    Uri uri = new Uri($"{_apiPath}/messages/getActive");
                    var response = await connection.GetAsync(uri);
                    var body = await response.Content.ReadFromJsonAsync<List<UpdateMessage>>();
                    var filteredMessages = body.Where(m => m.ValidForBuild == Assembly.GetExecutingAssembly().GetName().Version.ToString()).ToList();
                    return filteredMessages;
                }
            }
            catch (Exception e)
            {
                Logging.LogError(e.Message);
                return new List<UpdateMessage>();
            }
        }
    }
}
