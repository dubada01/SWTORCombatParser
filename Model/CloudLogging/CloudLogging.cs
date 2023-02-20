using Npgsql;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Utilities;
using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Reflection;

namespace SWTORCombatParser.Model.CloudLogging
{
    public static class CloudLogging
    {
        public static async void UploadLogAsync(string logMessage, string logCategory)
        {
            try
            {
                using (NpgsqlConnection connection = ConnectToDB())
                {
                    var pcName = Dns.GetHostName();
                    var ipAddress = GetLocalIPAddress();

                    using (var cmd = new NpgsqlCommand("INSERT INTO public.cloud_logs" +
                    " (timestamp,computer_name,ip_address,category,log_text,software_version)" +
                    $" VALUES (" +
                    $"@time," +
                    $"@pc," +
                    $"@ip," +
                    $"@cat," +
                    $"@log," +
                    $"@ver)", connection)
                    {
                        Parameters =
                        {
                            new("time",GetUTCTimeStamp(DateTime.Now)),
                            new("pc",pcName),
                            new("ip",ipAddress),
                            new("cat",logCategory),
                            new("log",logMessage),
                            new("ver",Assembly.GetExecutingAssembly().GetName().Version.ToString()),
                        }
                    })
                    {
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch(Exception e)
            {
                Logging.LogError("Failed to connect to database: " + e.Message, false);
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
        private static DateTime GetUTCTimeStamp(DateTime timeZone)
        {
            return timeZone.ToUniversalTime();
        }
        private static NpgsqlConnection ConnectToDB()
        {
            try
            {
                var conn = new NpgsqlConnection(DatabaseIPGetter.GetCurrentConnectionString());
                conn.Open();
                return conn;
            }
            catch (Exception e)
            {
                throw new Exception("Failed to connect to logging database: " + e.Message);
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
