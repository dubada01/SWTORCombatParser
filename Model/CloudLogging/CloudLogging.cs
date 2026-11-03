using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Utilities;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;

namespace SWTORCombatParser.Model.CloudLogging
{
    public static class CloudLogging
    {
        private static string _apiPath => DatabaseIPGetter.CurrentAPIURL();
        private static Guid _appStartupId;
        public static async Task UploadLogAsync(string logMessage, string logCategory)
        {
            if (Settings.ReadSettingOfType<bool>("offline_mode"))
                return;
            try
            {
                var pcName = Dns.GetHostName();
                var version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                var Ip = GetLocalIPAddress();
                using (HttpClient connection = new HttpClient())
                {
                    Uri uri = new Uri($"{_apiPath}/logging/add?message={logMessage}&category={logCategory}&hostName={pcName}&ipAddress={Ip}&version={version}");
                    var response = await connection.GetAsync(uri);
                    return;
                }
            }
            catch (Exception e)
            {
                Logging.LogError(e.Message, false);
                return;
            }

        }
        public static async Task LogStartup()
        {
            if (Settings.ReadSettingOfType<bool>("offline_mode"))
                return;
            try
            {
                
                var version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                _appStartupId = Guid.NewGuid();
                using (HttpClient connection = new HttpClient())
                {
                    Uri uri = new Uri($"{_apiPath}/logging/startup?id={_appStartupId}&version={version}");
                    var response = await connection.GetAsync(uri);
                    return;
                }
            }
            catch (Exception e)
            {
                Logging.LogError(e.Message, false);
                return;
            }
        }

        public static async Task LogShutdown()
        {
            if (Settings.ReadSettingOfType<bool>("offline_mode"))
                return;
            try
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                using (HttpClient connection = new HttpClient())
                {
                    Uri uri = new Uri($"{_apiPath}/logging/shutdown?id={_appStartupId}&version={version}");
                    var response = await connection.GetAsync(uri);
                    return;
                }
            }
            catch (Exception e)
            {
                Logging.LogError(e.Message, false);
                return;
            }
        }
        public static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }
    }
}
