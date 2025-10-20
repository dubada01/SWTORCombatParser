namespace SWTORCombatParser.Model.CloudRaiding
{
    public static class DatabaseIPGetter
    {
        private static string _debugLocalPort = "5020";
        private static string _prodPort = "443";

        private static string _debugLocalURL = "localhost:5020";
        private static string _prodURL = "api.orbs-stats.com/api";


        private static string _apiURL = _prodURL;

        private static string _currentAPIPort = _prodPort;
        public static string GetCurrentRemoteServerIP()
        {
            return _apiURL;
        }
        public static string CurrentAPIURL()
        {
            return $"https://{GetCurrentRemoteServerIP()}";
        }
    }
}
