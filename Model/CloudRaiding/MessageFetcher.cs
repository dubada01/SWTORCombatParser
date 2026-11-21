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
        private static readonly HttpClient _client = OfflineHttpHandler.CreateClient();
        public static async Task<List<UpdateMessage>> GetMessages()
        {
            if (Settings.ReadSettingOfType<bool>("offline_mode"))
                return new List<UpdateMessage>();
            try
            {
                Uri uri = new Uri($"{_apiPath}/messages/getActive");
                using var response = await _client.GetAsync(uri);
                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadFromJsonAsync<List<UpdateMessage>>();
                var version = Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
                // Accept public versions and the zero-padded CLR/MSIX form.
                var filteredMessages = (body ?? new List<UpdateMessage>()).Where(m => m.ValidForBuild == version || m.ValidForBuild == version + ".0").ToList();
                return filteredMessages;
            }
            catch (Exception e)
            {
                Logging.LogError(e.Message);
                return new List<UpdateMessage>();
            }
        }
    }
}
