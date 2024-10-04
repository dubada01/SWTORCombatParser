using SWTORCombatParser.Model.Updates;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels;
using SWTORCombatParser.ViewModels.Update;
using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;


namespace SWTORCombatParser.Views
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
        public HotkeyHandler HotkeyHandler;
        public MainWindow()
        {
            InitializeComponent();
            HotkeyHandler  = new HotkeyHandler();
            LoadingWindowFactory.SetMainWindow(this);
            var windowInfo = OrbsWindowManager.GetWindowSizeAndPosition();
            this.Position = new PixelPoint((int)windowInfo.TopLeft.Y, (int)windowInfo.TopLeft.X);
            Width = windowInfo.Width;
            Height = windowInfo.Height;
            Closed += MainWindow_Closed;
            Loaded += CheckForUpdates;
        }

        private async void CheckForUpdates(object sender, RoutedEventArgs e)
        {
            var newMessages = await UpdateMessageService.GetUpdateMessages();
            if(newMessages.Count > 0)
            {
                var updateWindow = new FeatureUpdateInfoWindow();
                var updateWindowViewModel = new FeatureUpdatesViewModel(newMessages);
                updateWindowViewModel.OnEmpty += updateWindow.Close;
                updateWindow.DataContext = updateWindowViewModel;
                updateWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                updateWindow.ShowDialog(this);
            }
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            // Unregister the hotkey when the window is closed to clean up
            HotkeyHandler.UnregisterHotKey(1);
            HotkeyHandler.UnregisterHotKey(2);
        }

        private void Window_Closing(object sender, WindowClosingEventArgs e)
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
                SwtorDetector.StopMonitoring();
                MainWindowClosing.FireClosing();
                Environment.Exit(0);
            }
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
        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            HotkeyHandler.Init();
        }

        private void Window_PointerLeave(object sender, PointerEventArgs e)
        {
            OrbsWindowManager.SaveWindowSizeAndPosition(new OrbsWindowInfo { TopLeft = new Point(Position.X,Position.Y), Width = ClientSize.Width, Height = ClientSize.Height });
        }
    }
}
