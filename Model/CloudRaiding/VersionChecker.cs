using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

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
		public static void OpenMicrosoftStoreToAppPage()
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
				MessageBox.Show("An error occurred while trying to open the Microsoft Store. Make sure the Microsoft Store is installed and the Package Family Name is correct.", "Error");
			}
		}
	}
}
