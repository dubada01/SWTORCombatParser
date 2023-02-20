using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SWTORCombatParser.Utilities;
using System;
using System.IO;
using System.Net.Http;

namespace SWTORCombatParser.Model.CloudRaiding
{
    public static class DatabaseIPGetter
    {
        private static DateTime _mostRecentIpTime;
        private static string _currentIP;
        private static string _dbConnectionString => ReadEncryptedString(JsonConvert.DeserializeObject<JObject>(File.ReadAllText(@"connectionConfig.json"))["NewConnectionString"].ToString());
        public static string GetCurrentConnectionString()
        {
            var currentConnectionString = _dbConnectionString;
            var hostname = currentConnectionString.Split('=')[1].Split(';')[0];
            var newConnectionString = currentConnectionString.Replace(hostname, GetCurrentRemoteServerIP());
            return newConnectionString;
        }
        public static string GetCurrentRemoteServerIP()
        {
            if ((DateTime.Now - _mostRecentIpTime).TotalMinutes < 1 && !string.IsNullOrEmpty(_currentIP))
                return _currentIP;
            string url = "http://ec2-35-85-227-238.us-west-2.compute.amazonaws.com:5000/getIP";
            using (HttpClient client = new HttpClient())
            {
                var currentIp = client.GetStringAsync(url).Result;
                _mostRecentIpTime = DateTime.Now;
                _currentIP = currentIp;
                return currentIp;
            }
        }
        private static string ReadEncryptedString(string encryptedString)
        {
            var secret = "obscureButNotSecure";
            var decryptedString = Crypto.DecryptStringAES(encryptedString, secret);

            return decryptedString;
        }
    }
}
