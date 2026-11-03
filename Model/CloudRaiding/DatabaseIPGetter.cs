namespace SWTORCombatParser.Model.CloudRaiding
{
    public static class DatabaseIPGetter
    {
        private static bool _isDebug = false;
        private static string _prodURL = "api.orbs-stats.com/api";

        public static string GetCurrentRemoteServerIP()
        {
            return _prodURL;
        }
        public static string CurrentAPIURL()
        {
            return _isDebug ? "http://localhost:5020" : $"https://{GetCurrentRemoteServerIP()}";
        }
        
    }
}
