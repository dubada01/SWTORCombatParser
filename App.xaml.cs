using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels;
using SWTORCombatParser.Views;
using System;
using System.Diagnostics;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Orbs_Avalonia;
using Avalonia;
using SWTORCombatParser.ViewModels.Avalonia_TEMP;

namespace SWTORCombatParser
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
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
                Task.Run(async () =>
                {
                    await ExtractIconsIfNecessaryAsync();
                    IconGetter.Init();
                });

                var mainWindow = new MainWindow();
                System.Windows.Application.Current.MainWindow = mainWindow;
                var mainWindowVM = new MainWindowViewModel(mainWindow.HotkeyHandler);
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

        private async Task ExtractIconsIfNecessaryAsync()
        {
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DubaTech", "SWTORCombatParser");
            var iconsPath = Path.Combine(appDataPath, "resources/icons");

            // Check if the icons directory already exists
            if (!Directory.Exists(iconsPath))
            {
                Directory.CreateDirectory(iconsPath);
                var zipFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources", "packagedIcons.zip");

                // Use System.IO.Compression to extract the files
                ZipFile.ExtractToDirectory(zipFilePath, iconsPath);
            }
        }
        private static void CheckForAppVersion()
        {
            Task.Run(VersionChecker.CheckForMostRecentVersion);
        }


    }
}
