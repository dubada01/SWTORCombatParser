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
        private static readonly HttpClient _client = new HttpClient();
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
                Uri uri = new Uri($"{_apiPath}/logging/add?message={Uri.EscapeDataString(logMessage ?? string.Empty)}&category={Uri.EscapeDataString(logCategory ?? string.Empty)}&hostName={Uri.EscapeDataString(pcName)}&ipAddress={Uri.EscapeDataString(Ip)}&version={Uri.EscapeDataString(version)}");
                using var response = await _client.GetAsync(uri);
                response.EnsureSuccessStatusCode();
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
                Uri uri = new Uri($"{_apiPath}/logging/startup?id={_appStartupId}&version={Uri.EscapeDataString(version)}");
                using var response = await _client.GetAsync(uri);
                response.EnsureSuccessStatusCode();
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
                Uri uri = new Uri($"{_apiPath}/logging/shutdown?id={_appStartupId}&version={Uri.EscapeDataString(version)}");
                using var response = await _client.GetAsync(uri);
                response.EnsureSuccessStatusCode();
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
