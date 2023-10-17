using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels;
using SWTORCombatParser.Views;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace SWTORCombatParser
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        
        protected override void OnStartup(StartupEventArgs e)
        {
            App_Startup(e);
        }
        private void App_Startup(StartupEventArgs e)
		{
			CheckForAppVersion();

			Process[] processCollection = Process.GetProcesses();
			if (processCollection.Count(pc => pc.ProcessName.ToLower() == "orbs") == 1)
			{
				ConvertToAppData.ConvertFromProgramDataToAppData();
				var task = TimeUtility.StartUpdateTask();
				var mainWindowVM = new MainWindowViewModel();
				var mainWindow = new MainWindow();
				Application.Current.MainWindow = mainWindow;
				mainWindow.DataContext = mainWindowVM;
				mainWindow.Show();
			}
			else
			{
				if (ShouldShowPopup.ReadShouldShowPopup("InstanceRunning"))
				{
					var warningWindow = new InstanceAlreadyRunningWarning();
					warningWindow.Show();
					warningWindow.Closed += (s, e) => { Shutdown(0); };
				}
				else
				{
					Shutdown(0);
				}
			}
		}

		private static void CheckForAppVersion()
		{
			Task.Run(VersionChecker.CheckForMostRecentVersion);
		}


	}
}
