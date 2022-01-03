using SWTORCombatParser.ViewModels;
using SWTORCombatParser.Views;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;

namespace SWTORCombatParser
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        void App_Startup(object sender, StartupEventArgs e)
        {
            Process[] processCollection = Process.GetProcesses();
            if (processCollection.Count(pc => pc.ProcessName.ToLower() == "orbs") == 1)
            {
                var mainWindowVM = new MainWindowViewModel();
                var mainWindow = new MainWindow();
                mainWindow.DataContext = mainWindowVM;
                mainWindow.Show();
            }
            else
            {
                var warningWindow = new InstanceAlreadyRunningWarning();
                warningWindow.Show();
                warningWindow.Closed += (s, e) => { Shutdown(0); };
            }
        }
    }
}
