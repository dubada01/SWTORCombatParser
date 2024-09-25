namespace SWTORCombatParser.Model.CloudRaiding
{
    public static class DatabaseIPGetter
    {
        private static string _debugLocalPort = "32771";
        private static string _prodPort = "36715";

        private static string _debugLocalURL = "localhost";
        private static string _prodURL = "orbs-stats.com";


        private static string _apiURL = _prodURL;

        private static string _currentAPIPort = _prodPort;
        public static string GetCurrentRemoteServerIP()
        {
            return _apiURL;
        }
        public static string CurrentAPIURL()
        {
            return $"http://{GetCurrentRemoteServerIP()}:{_currentAPIPort}";
        }
    }
}
