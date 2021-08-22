using SWTORCombatParser.ViewModels;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
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
            var mainWindowVM = new MainWindowViewModel();
            var mainWindow = new MainWindow();
            mainWindow.DataContext = mainWindowVM;
            mainWindow.Show();
        }

    }
}
