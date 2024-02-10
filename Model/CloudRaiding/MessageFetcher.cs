using SWTORCombatParser.DataStructures.Updates;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser.Model.CloudRaiding
{
    public static class MessageFetcher
    {
        private static string _apiPath => DatabaseIPGetter.CurrentAPIURL();
        public static List<UpdateMessage> GetMessages()
        {
            if (Settings.ReadSettingOfType<bool>("offline_mode"))
                return new List<UpdateMessage>();
            try
            {
                using (HttpClient connection = new HttpClient())
                {
                    Uri uri = new Uri($"{_apiPath}/messages/getActive");
                    var response = connection.GetAsync(uri).Result;
                    var body = response.Content.ReadFromJsonAsync<List<UpdateMessage>>().Result;
                    var filteredMessages = body.Where(m => m.ValidForBuild == Assembly.GetExecutingAssembly().GetName().Version.ToString());
                    return body;
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
