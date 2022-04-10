using Google.Cloud.Vision.V1;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser.Model.CloudRaiding
{
    public static class GoogleCloudPlatform
    {
        private static ImageAnnotatorClient _currentClient;
        public static ImageAnnotatorClient GetClient()
        {
            var creds = JsonConvert.DeserializeObject<JObject>(File.ReadAllText("connectionConfig.json"))["GoogleCloudCreds"].ToString();
            if(_currentClient == null)
            {
                ImageAnnotatorClientBuilder clientBuilder = new ImageAnnotatorClientBuilder() { JsonCredentials = creds };
                _currentClient = clientBuilder.Build();
            }
            return _currentClient;
        }
    }
}
