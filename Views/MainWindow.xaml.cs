using ScottPlot.Plottable;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Plotting;
using SWTORCombatParser.resources;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels;
using SWTORCombatParser.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SWTORCombatParser
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>

    public static class MainWindowClosing
    {
        public static event Action Closing = delegate { };
        public static event Action Hiding = delegate { };
        public static void FireClosing()
        {
            Closing();
        }
        public static void FireHidden()
        {
            Hiding();
        }
    }
    public static class HeaderSelectionState
    {
        public static event Action NewHeaderSelected = delegate { };
        public static string CurrentlySelectedTabHeader = "";
        public static void UpdateSelectedHeader(string header)
        {
            CurrentlySelectedTabHeader = header;
            NewHeaderSelected();
        }
    }
    public partial class MainWindow : Window
    {
        private bool _actuallyClosing = false;
        public MainWindow()
        {
            InitializeComponent();
            LoadingWindowFactory.SetMainWindow(this);
            var windowInfo = OrbsWindowManager.GetWindowSizeAndPosition();
            Top = windowInfo.TopLeft.Y;
            Left = windowInfo.TopLeft.X;
            Width = windowInfo.Width;
            Height = windowInfo.Height;
            AddNotificationIcon();
            //SWTORDetector.StartMonitoring();
        }
        private void AddNotificationIcon()
        {
            var components = new Container();
            var contextMenu1 = new ContextMenuStrip();

            var menuItem1 = new ToolStripMenuItem();
            menuItem1.Text = "E&xit";
            menuItem1.Click += new EventHandler(ExitClick);

            var menuItem2 = new ToolStripMenuItem();
            menuItem2.Text = "Show";
            menuItem2.Click += new EventHandler(ShowClick);

            contextMenu1.Items.AddRange(
                        new ToolStripMenuItem[] { menuItem2,menuItem1 });

            var notifyIcon1 = new NotifyIcon(components);
            notifyIcon1.Icon = new Icon("resources/SWTORParsingIcon.ico");
            notifyIcon1.ContextMenuStrip = contextMenu1;
            notifyIcon1.Text = System.Windows.Forms.Application.ProductName;
            notifyIcon1.Visible = true;
            notifyIcon1.DoubleClick += new EventHandler(notifyIcon1_DoubleClick);
        }

        private void ShowClick(object sender, EventArgs e)
        {
            Show();
            LoadingWindowFactory.MainWindowHidden = false;
            WindowState = WindowState.Normal;
        }

        private void notifyIcon1_DoubleClick(object sender, EventArgs e)
        {
            Show();
            LoadingWindowFactory.MainWindowHidden = false;
            WindowState = WindowState.Normal;
        }
        
        private void ExitClick(object sender, EventArgs e)
        {
            _actuallyClosing = true;
            Close();
        }
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (!_actuallyClosing && ShouldShowPopup.ReadShouldShowPopup("BackgroundDisabled"))
            {
                e.Cancel = true;
                if (ShouldShowPopup.ReadShouldShowPopup("BackgroundMonitoring"))
                {
                    LoadingWindowFactory.ShowBackgroundNotice();
                }
                LoadingWindowFactory.MainWindowHidden = true;
                MainWindowClosing.FireHidden();
                Hide();
            }
            else
            { 
                MainWindowClosing.FireClosing();
                Environment.Exit(0);
            }
        }
        private void Window_MouseLeave_1(object sender, System.Windows.Input.MouseEventArgs e)
        {
            OrbsWindowManager.SaveWindowSizeAndPosition(new OrbsWindowInfo { TopLeft = new System.Windows.Point { X = Left, Y = Top }, Width = ActualWidth, Height = ActualHeight });
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0)
                return;
            var tabInstance = e.AddedItems[0] as TabInstance;
            if (tabInstance == null)
                return;
            HeaderSelectionState.UpdateSelectedHeader(tabInstance.HeaderText);
        }
    }
}
