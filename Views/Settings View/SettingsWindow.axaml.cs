using SWTORCombatParser.DataStructures.Hotkeys;
using SWTORCombatParser.Model.Updates;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.Update;
using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using SharpHook.Native;

namespace SWTORCombatParser.Views.SettingsView
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private bool _isDragging;
        private Point _startPoint;

        public SettingsWindow()
        {
            InitializeComponent();
            InitHotkeys();
            InitBools();
            InitPath();
            RefreshEnabled.IsCheckedChanged += ToggleHotkeyEnabled;
            LockEnabled.IsCheckedChanged += ToggleHotkeyEnabled;
            HideEnabled.IsCheckedChanged += ToggleHotkeyEnabled;
            RunInBackground.IsCheckedChanged += ToggleBackground;
            ForceLogUpdates.IsCheckedChanged += ToggleLogForce;
            OfflineMode.IsCheckedChanged += ToggleOffline;
            BackgroundWarning.IsCheckedChanged += ToggleWarning;
            LogPath.TextChanged += UpdatePath;
            ResetMessagesButton.Click += ResetMessages;

            EmergencyUIReset.Click += ShowEmergencyDialog;

            SetCombatLogsPath.Click += UpdateCombatLogsPath;



            ShowTargeted.IsCheckedChanged += UpdatedDisplayTargeted;
        }



        private async void UpdateCombatLogsPath(object? sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog(){Title = "Select New Combat Logs Path"};
            var result = await dialog.ShowAsync(this);
            if (string.IsNullOrEmpty(result))
                return;
            LogPath.Text = result;
            //UpdatePath(null,null);
        }


        private void InitBools()
        {
            OfflineMode.IsChecked = Settings.ReadSettingOfType<bool>("offline_mode");
            RunInBackground.IsChecked = ShouldShowPopup.ReadShouldShowPopup("BackgroundDisabled");
            ForceLogUpdates.IsChecked = Settings.ReadSettingOfType<bool>("force_log_updates");
            BackgroundWarning.IsChecked = ShouldShowPopup.ReadShouldShowPopup("BackgroundMonitoring");
            ShowTargeted.IsChecked = Settings.ReadSettingOfType<bool>("overlay_show_targeted");
        }
        private async void ResetMessages(object sender, RoutedEventArgs e)
        {
            var newMessages = await UpdateMessageService.GetAllUpdateMessages();
            if (newMessages.Count > 0)
            {
                var updateWindow = new FeatureUpdateInfoWindow();
                var updateWindowViewModel = new FeatureUpdatesViewModel(newMessages);
                updateWindowViewModel.OnEmpty += updateWindow.Close;
                updateWindow.DataContext = updateWindowViewModel;
                updateWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                await updateWindow.ShowDialog(this);
            }
        }
        private async void ShowEmergencyDialog(object? sender, RoutedEventArgs e)
        {
            var warning = MessageBoxManager.GetMessageBoxStandard("This will completely reset all your overlay positions for all roles.\r\nIf so, click yes and restart Orbs","Are you sure?",ButtonEnum.YesNo);
            var result = await warning.ShowWindowDialogAsync(this);
            if(result != ButtonResult.No)
            {
                return;
            }
            var currentPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DubaTech", "SWTORCombatParser");
            var newPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DubaTech", "SWTORCombatParser_Archived");
            Directory.Move(currentPath, newPath);
        }
        private void InitPath()
        {
            var path = Settings.ReadSettingOfType<string>("combat_logs_path");
            LogPath.Text = path;
        }
        private void UpdatePath(object sender, TextChangedEventArgs e)
        {
            Settings.WriteSetting<string>("combat_logs_path", LogPath.Text);
        }

        private void ToggleOffline(object sender, RoutedEventArgs e)
        {
            Settings.WriteSetting("offline_mode", OfflineMode.IsChecked.Value);
        }

        private void ToggleLogForce(object sender, RoutedEventArgs e)
        {
            Settings.WriteSetting("force_log_updates", ForceLogUpdates.IsChecked.Value);
        }

        private void ToggleBackground(object sender, RoutedEventArgs e)
        {
            ShouldShowPopup.SaveShouldShowPopup("BackgroundDisabled", !RunInBackground.IsChecked.Value);
        }
        private void ToggleWarning(object sender, RoutedEventArgs e)
        {
            ShouldShowPopup.SaveShouldShowPopup("BackgroundMonitoring", !RunInBackground.IsChecked.Value);
        }
        private void UpdatedDisplayTargeted(object? sender, RoutedEventArgs e)
        {
            Settings.WriteSetting("overlay_show_targeted", ShowTargeted.IsChecked.Value);
        }
        private void ToggleHotkeyEnabled(object sender, RoutedEventArgs e)
        {
            var current = Settings.ReadSettingOfType<HotkeySettings>("Hotkeys");
            if(((CheckBox)sender).Name == "RefreshEnabled")
                current.HOTRefreshEnabled = !current.HOTRefreshEnabled;
            if (((CheckBox)sender).Name == "LockEnabled")
                current.UILockEnabled = !current.UILockEnabled;
            if (((CheckBox)sender).Name == "HideEnabled")
                current.OverlayHideEnabled = !current.OverlayHideEnabled;

            Settings.WriteSetting("Hotkeys", current);
        }

        private void InitHotkeys()
        {
            var current = Settings.ReadSettingOfType<HotkeySettings>("Hotkeys");
            UpdateTextBoxDisplay(RefreshHotkey, current.HOTRefreshHotkeyMod1, current.HOTRefreshHotkeyMod2, current.HOTRefreshHotkeyStroke);
            RefreshEnabled.IsChecked = current.HOTRefreshEnabled;
            UpdateTextBoxDisplay(LockHotkey, current.UILockHotkeyMod1, current.UILockHotkeyMod2, current.UILockHotkeyStroke);
            LockEnabled.IsChecked = current.UILockEnabled;
            UpdateTextBoxDisplay(HideHotkey, current.OverlayHideHotkeyMod1, current.OverlayHideHotkeyMod2, current.OverlayHideHotkeyStroke);
            HideEnabled.IsChecked = current.OverlayHideEnabled;
        }

        private void Cancel(object sender, RoutedEventArgs e)
        {
            Close();
        }

        public void StartDrag(object sender, PointerPressedEventArgs args)
        {
            _isDragging = true;
            _startPoint = args.GetPosition(this);
        }
        public void DragWindow(object sender, PointerEventArgs args)
        {
            if (_isDragging)
            {
                // Get the current scaling factor to adjust the movement correctly
                var scalingFactor = this.VisualRoot.RenderScaling;

                var currentPosition = args.GetPosition(this);
                var delta = (currentPosition - _startPoint) / scalingFactor;  // Adjust for DPI scaling

                // Move the window (or element) by the delta
                var currentPositionInScreen = this.Position;
                this.Position = new PixelPoint(
                    currentPositionInScreen.X + (int)delta.X,
                    currentPositionInScreen.Y + (int)delta.Y
                );
            }
        }
        public void StopDrag(object sender, PointerReleasedEventArgs args)
        {
            _isDragging = false;
        }
        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Prevents the TextBox from handling the key input so the TextBox remains empty
            e.Handled = true;

            var textBox = sender as TextBox;
            if (textBox == null) return;

            // Reset previous input
            int mod1 = 0, mod2 = 0, keyStroke = 0;

            // Check for Ctrl modifier
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                if (mod1 == 0)
                    mod1 = 2; // Representing Ctrl
                else if (mod2 == 0)
                    mod2 = 2; // Representing Ctrl
            }

            // Check for Shift modifier
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                if (mod1 == 0)
                    mod1 = 4; // Representing Shift
                else if (mod2 == 0)
                    mod2 = 4; // Representing Shift
            }

            // Check for Alt modifier
            if (e.KeyModifiers.HasFlag(KeyModifiers.Alt))
            {
                if (mod1 == 0)
                    mod1 = 1; // Representing Alt
                else if (mod2 == 0)
                    mod2 = 1; // Representing Alt
            }

            // Capture the key stroke if it's not a modifier key
            if (e.Key != Key.LeftCtrl && e.Key != Key.RightCtrl &&
                e.Key != Key.LeftShift && e.Key != Key.RightShift &&
                e.Key != Key.LeftAlt && e.Key != Key.RightAlt)
            {
                keyStroke = ConvertAvaloniaKeyToSharpHookKeyCode(e.Key);
            }

            // Save settings or update display as needed
            SaveSetting(textBox, mod1, mod2, keyStroke);
            UpdateTextBoxDisplay(textBox, mod1, mod2, keyStroke);
        }

        private int ConvertAvaloniaKeyToSharpHookKeyCode(Key avaloniaKey)
        {
            // Map Avalonia keys to SharpHook KeyCode enum values
            return avaloniaKey switch
            {
                Key.A => (int)KeyCode.VcA,
                Key.B => (int)KeyCode.VcB,
                Key.C => (int)KeyCode.VcC,
                Key.D => (int)KeyCode.VcD,
                Key.E => (int)KeyCode.VcE,
                Key.F => (int)KeyCode.VcF,
                Key.G => (int)KeyCode.VcG,
                Key.H => (int)KeyCode.VcH,
                Key.I => (int)KeyCode.VcI,
                Key.J => (int)KeyCode.VcJ,
                Key.K => (int)KeyCode.VcK,
                Key.L => (int)KeyCode.VcL,
                Key.M => (int)KeyCode.VcM,
                Key.N => (int)KeyCode.VcN,
                Key.O => (int)KeyCode.VcO,
                Key.P => (int)KeyCode.VcP,
                Key.Q => (int)KeyCode.VcQ,
                Key.R => (int)KeyCode.VcR,
                Key.S => (int)KeyCode.VcS,
                Key.T => (int)KeyCode.VcT,
                Key.U => (int)KeyCode.VcU,
                Key.V => (int)KeyCode.VcV,
                Key.W => (int)KeyCode.VcW,
                Key.X => (int)KeyCode.VcX,
                Key.Y => (int)KeyCode.VcY,
                Key.Z => (int)KeyCode.VcZ,
                Key.D0 => (int)KeyCode.Vc0,
                Key.D1 => (int)KeyCode.Vc1,
                Key.D2 => (int)KeyCode.Vc2,
                Key.D3 => (int)KeyCode.Vc3,
                Key.D4 => (int)KeyCode.Vc4,
                Key.D5 => (int)KeyCode.Vc5,
                Key.D6 => (int)KeyCode.Vc6,
                Key.D7 => (int)KeyCode.Vc7,
                Key.D8 => (int)KeyCode.Vc8,
                Key.D9 => (int)KeyCode.Vc9,
                Key.Enter => (int)KeyCode.VcEnter,
                Key.Space => (int)KeyCode.VcSpace,
                Key.Back => (int)KeyCode.VcBackspace,
                Key.Tab => (int)KeyCode.VcTab,
                Key.Escape => (int)KeyCode.VcEscape,
                // Add more mappings as needed
                _ => 0 // Default value if key is not mapped
            };
        }

        private void SaveSetting(TextBox textBox, int mod1, int mod2, int keyStroke)
        {
            var current = Settings.ReadSettingOfType<HotkeySettings>("Hotkeys");
            switch (textBox.Tag)
            {
                case "Refresh":
                    current.HOTRefreshHotkeyMod1 = mod1;
                    current.HOTRefreshHotkeyMod2 = mod2;
                    current.HOTRefreshHotkeyStroke = keyStroke;
                    break;
                case "Lock":
                    current.UILockHotkeyMod1 = mod1;
                    current.UILockHotkeyMod2 = mod2;
                    current.UILockHotkeyStroke = keyStroke;
                    break;
                case "Hide":
                    current.OverlayHideHotkeyMod1 = mod1;
                    current.OverlayHideHotkeyMod2 = mod2;
                    current.OverlayHideHotkeyStroke = keyStroke;
                    break;
            }
            Settings.WriteSetting("Hotkeys", current);
        }
        private void UpdateTextBoxDisplay(TextBox textBox, int mod1, int mod2, int keyStroke)
        {
            // Convert the key codes for modifiers to strings
            string mod1Str = KeyCodeToModifierString(mod1);
            string mod2Str = KeyCodeToModifierString(mod2);

            // Convert the key stroke to a string, handling special cases as needed
            string keyStrokeStr = KeyToString(keyStroke);

            // Combine the modifiers and keystroke into a single string
            string hotkeyStr = $"{mod1Str}{(mod1Str != string.Empty && mod2Str != string.Empty ? " + " : string.Empty)}{mod2Str}{(mod1Str != string.Empty || mod2Str != string.Empty ? " + " : string.Empty)}{keyStrokeStr}";

            // Update the TextBox's Text property to display the hotkey
            textBox.Text = hotkeyStr;
        }

        private string KeyCodeToModifierString(int keyCode)
        {
            switch (keyCode)
            {
                case 2:
                    return "Ctrl";
                case 4:
                    return "Shift";
                case 1:
                    return "Alt";
                default:
                    return string.Empty; // No modifier or unsupported modifier
            }
        }

        private string KeyToString(int keyCode)
        {
            // Convert the keyCode to an Avalonia Key enum
            var key = (KeyCode)keyCode;

            // Special handling for specific keys can go here
            // For example, converting Key.OemPlus to "+" or other special keys
            switch (key)
            {
                case KeyCode.VcSpace:
                    return "Space";
                // Add more cases as necessary for specific key conversions
                default:
                    return ConvertKeyCodeToString(key); // Fallback to the default string representation of the key
            }
        }
        private string ConvertKeyCodeToString(KeyCode keyCode)
        {
            // Convert KeyCode to string without the "Vc" prefix
            string keyCodeName = keyCode.ToString();
            return keyCodeName.StartsWith("Vc") ? keyCodeName.Substring(2) : keyCodeName;
        }

    }
}
