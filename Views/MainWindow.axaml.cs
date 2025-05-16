using SWTORCombatParser.Model.Updates;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.Update;
using System;
using System.Diagnostics;
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
    public partial class MainWindow : Window
    {
        public bool ActuallyClosing { get; set; } = false;
        public HotkeyHandler HotkeyHandler;
        public MainWindow()
        {
            InitializeComponent();
            #if DEBUG
                this.AttachDevTools();
            #endif
            HotkeyHandler  = new HotkeyHandler();
            LoadingWindowFactory.SetMainWindow(this);

            Closed += MainWindow_Closed;
            Loaded += CheckForUpdates;
            Opened += SetWindowParams;
        }

        private void SetWindowParams(object? sender, EventArgs e)
        {
            var windowInfo = OrbsWindowManager.GetWindowSizeAndPosition();
            Position = new PixelPoint(windowInfo.TopLeft.X, windowInfo.TopLeft.Y);
            Width = windowInfo.Width;
            Height = windowInfo.Height;
            ScalingManager.UIScalingFactor = RenderScaling;
            //base.OnOpened(e);
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
            if (!ActuallyClosing && ShouldShowPopup.ReadShouldShowPopup("BackgroundDisabled"))
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
        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            HotkeyHandler.Init();
        }

        private void Window_PointerLeave(object sender, PointerEventArgs e)
        {
            OrbsWindowManager.SaveWindowSizeAndPosition(new OrbsWindowInfo { TopLeft = new PixelPoint(Position.X,Position.Y), Width = ClientSize.Width, Height = ClientSize.Height });
        }
    }
}
