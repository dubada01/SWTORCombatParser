using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;


namespace SWTORCombatParser.Utilities
{
    public static class TimeUtility
    {
        private static double _currentOffset = 0;
        public static Task StartUpdateTask()
        {
            if (Settings.ReadSettingOfType<bool>("offline_mode"))
            {
                return Task.CompletedTask;
            }
            return Task.Run(() =>
            {
                while (true)
                {
                    try
                    {
                        UpdateCurrentTimeOffset();
                    }
                    catch (Exception e)
                    {

                    }
                    Thread.Sleep(1800000);
                }
            });

        }
        private static DateTime GetNetworkTime()
        {
            try
            {
                const string ntpServer = "pool.ntp.org";
                var ntpData = new byte[48];
                ntpData[0] = 0x1B;
                var addresses = Dns.GetHostEntry(ntpServer).AddressList;
                var ipEndPoint = new IPEndPoint(addresses[0], 123);
                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    socket.ReceiveTimeout = 5000;
                    socket.SendTimeout = 5000;
                    socket.Connect(ipEndPoint);
                    socket.Send(ntpData);
                    socket.Receive(ntpData);
                }

                var intPart = (ulong)ntpData[40] << 24 | (ulong)ntpData[41] << 16 | (ulong)ntpData[42] << 8 | ntpData[43];
                var fractPart = (ulong)ntpData[44] << 24 | (ulong)ntpData[45] << 16 | (ulong)ntpData[46] << 8 | ntpData[47];
                var milliseconds = (intPart * 1000) + ((fractPart * 1000) / 0x100000000L);
                var networkDateTime = (new DateTime(1900, 1, 1)).AddMilliseconds((long)milliseconds);

                var localTimeZone = TimeZoneInfo.Local;
                var localNetworkTime = TimeZoneInfo.ConvertTimeFromUtc(networkDateTime, localTimeZone);


                return localNetworkTime;
            }
            catch (Exception e)
            {
                return DateTime.Now;
            }
        }
        private static void UpdateCurrentTimeOffset()
        {
            var nistTime = GetNetworkTime();
            // Get the current system time
            DateTime localTime = DateTime.Now;


            // Calculate the offset between local time and NIST time in milliseconds
            TimeSpan offset = nistTime - localTime;
            double offsetInMilliseconds = offset.TotalMilliseconds;
            if (Math.Abs(offsetInMilliseconds) > TimeSpan.FromMinutes(30).TotalMilliseconds)
                return;
            _currentOffset = offsetInMilliseconds;
        }
        public static DateTime CorrectedTime => DateTime.Now.AddMilliseconds(_currentOffset);
    }

}
