using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using MsBox.Avalonia;

namespace SWTORCombatParser.Model.CloudRaiding
{
    public static class VersionChecker
    {
        private static readonly string _appFamilyName = "37053DubaTech.OldRepublicBattleParser_efsdnxq7216xe";
        public static bool AppIsUpToDate { get; set; }
        public static event Action AppVersionInfoReady = delegate { };
        public static async Task CheckForMostRecentVersion()
        {
            var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
            var maxVersion = await API_Connection.GetMostRecentVersion();
            if (maxVersion > currentVersion)
                AppIsUpToDate = false;
            else
                AppIsUpToDate = true;
            AppVersionInfoReady();
        }
        public static async void OpenMicrosoftStoreToAppPage()
        {
            // Construct the app URI
            string appUri = $"ms-windows-store://pdp/?PFN={_appFamilyName}";

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd",
                    Arguments = $"/c start {appUri}",
                    UseShellExecute = true,
                    CreateNoWindow = true
                });
            }
            catch (Exception ex)
            {
                if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    var warning = MessageBoxManager.GetMessageBoxStandard("An error occurred while trying to open the Microsoft Store. Make sure the Microsoft Store is installed and the Package Family Name is correct.","Are you sure?");
                    await warning.ShowAsync();
                }
            }
        }
    }
}
