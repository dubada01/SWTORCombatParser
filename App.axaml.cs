using Avalonia;
using Avalonia.Threading;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels;
using SWTORCombatParser.Views;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using LibVLCSharp.Shared;
using ManagedBass;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;

namespace SWTORCombatParser
{
    public partial class App : Application
    {
        [DllImport("libvlc.dll")]
        public static extern IntPtr libvlc_new(int argc, string[] argv);
        public override void Initialize()
        {
            #if WINDOWS
            Core.Initialize();
            #endif
            #if MACOS
            Bass.Init();
            #endif
            AvaloniaXamlLoader.Load(this);
        }
        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Line below is needed to remove Avalonia data validation.
                // Without this line you will get duplicate validations from both Avalonia and CT
                BindingPlugins.DataValidators.RemoveAt(0);
            }
            App_Startup();
            base.OnFrameworkInitializationCompleted();
        }

        private void App_Startup()
        {
            CheckForAppVersion();
            Process[] processCollection = Process.GetProcesses();
            if (processCollection.Count(pc => pc.ProcessName.ToLower() == "orbs") == 1)
            {
                ConvertToAppData.ConvertFromProgramDataToAppData();
                CombatLogLoader.RefreshSWTORCombatLogsDirectory();
                var task = TimeUtility.StartUpdateTask();
                Task.Run(async () =>
                {
                    await ExtractIconsIfNecessaryAsync();
                    IconGetter.Init();
                });
                var mainWindow = new MainWindow();
                var mainWindowVM = new MainWindowViewModel(mainWindow.HotkeyHandler);
                mainWindow.DataContext = mainWindowVM;
                if(ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.MainWindow = mainWindow;
                }
                // TODO record when players start the software
                //Logging.LogStartup();
                mainWindow.Show();
            }
            else
            {
                if (ShouldShowPopup.ReadShouldShowPopup("InstanceRunning"))
                {
                    var warningWindow = new InstanceAlreadyRunningWarning();
                    warningWindow.Show();
                    warningWindow.Closed += (s, e) => { Dispatcher.UIThread.InvokeAsync(() => { ExitApplication(); }); };
                }
                else
                {
                    ExitApplication();
                }
            }
        }

        private async Task ExtractIconsIfNecessaryAsync()
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DubaTech", "SWTORCombatParser");

            var iconsPath = Path.Combine(appDataPath, "resources", "icons");

            // Extract only if missing or empty
            var needsExtraction =
                !Directory.Exists(iconsPath) ||
                !Directory.EnumerateFileSystemEntries(iconsPath).Any();

            if (!needsExtraction)
                return;

            var zipFilePath = Path.Combine(AppContext.BaseDirectory, "resources", "packagedIcons.zip");
            if (!File.Exists(zipFilePath))
                throw new FileNotFoundException("Could not find the packaged icons zip file", zipFilePath);

            if (Directory.Exists(iconsPath))
                Directory.Delete(iconsPath, true);

            Directory.CreateDirectory(iconsPath);

            await Task.Run(() => ZipFile.ExtractToDirectory(zipFilePath, iconsPath));

#if LINUX
            NormalizeIconFilenamesToLower(iconsPath);
#endif
        }

        private static void NormalizeIconFilenamesToLower(string root)
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                var dir = Path.GetDirectoryName(file)!;

                var name = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                var ext  = Path.GetExtension(file).ToLowerInvariant();
                var lowerPath = Path.Combine(dir, name + ext);

                if (string.Equals(file, lowerPath, StringComparison.Ordinal))
                    continue;

                // If a lowercased version already exists, keep it and delete the duplicate
                if (File.Exists(lowerPath))
                {
                    File.Delete(file);
                    continue;
                }

                File.Move(file, lowerPath);
            }
        }
        private static void CheckForAppVersion()
        {
            Task.Run(VersionChecker.CheckForMostRecentVersion);
        }
        private void ExitApplication()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopApp)
            {
                desktopApp.Shutdown();
            }
        }

        private void ShowClicked(object sender, EventArgs e)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopApp)
            {
                desktopApp.MainWindow.Show();
                LoadingWindowFactory.MainWindowHidden = false;
                desktopApp.MainWindow.WindowState = WindowState.Normal;
            }
        }

        private void ExitClicked(object sender, EventArgs e)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopApp)
            {
                var mainWindow = desktopApp.MainWindow as MainWindow;
                mainWindow.ActuallyClosing = true;
            }
            ExitApplication();
        }
    }
}
