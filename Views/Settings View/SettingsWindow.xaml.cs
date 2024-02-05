using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.Hotkeys;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SWTORCombatParser.Views.SettingsView
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            InitHotkeys();
            InitBools();
            InitPath();
            RefreshEnabled.Checked += ToggleHotkeyEnabled;
            LockEnabled.Checked += ToggleHotkeyEnabled;
            HideEnabled.Checked += ToggleHotkeyEnabled;
            RunInBackground.Checked += ToggleBackground;
            ForceLogUpdates.Checked += ToggleLogForce;
            OfflineMode.Checked += ToggleOffline;
            BackgroundWarning.Checked += ToggleWarning;
            LogPath.TextChanged += UpdatePath;
        }



        private void InitBools()
        {
            OfflineMode.IsChecked = Settings.ReadSettingOfType<bool>("offline_mode");
            RunInBackground.IsChecked = ShouldShowPopup.ReadShouldShowPopup("BackgroundDisabled");
            ForceLogUpdates.IsChecked = Settings.ReadSettingOfType<bool>("force_log_updates");
            BackgroundWarning.IsChecked = ShouldShowPopup.ReadShouldShowPopup("BackgroundMonitoring");
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
            Settings.WriteSetting<bool>("force_log_updates", OfflineMode.IsChecked.Value);
        }

        private void ToggleLogForce(object sender, RoutedEventArgs e)
        {
            Settings.WriteSetting<bool>("force_log_updates", ForceLogUpdates.IsChecked.Value);
        }

        private void ToggleBackground(object sender, RoutedEventArgs e)
        {
            ShouldShowPopup.SaveShouldShowPopup("BackgroundDisabled", !RunInBackground.IsChecked.Value);
        }
        private void ToggleWarning(object sender, RoutedEventArgs e)
        {
            ShouldShowPopup.SaveShouldShowPopup("BackgroundMonitoring", !RunInBackground.IsChecked.Value);
        }

        private void ToggleHotkeyEnabled(object sender, RoutedEventArgs e)
        {
            var current = Settings.ReadSettingOfType<HotkeySettings>("Hotkeys");
            if(((CheckBox)sender).Name == "RefreshEnabled")
                current.HOTRefreshEnabled = true;
            if (((CheckBox)sender).Name == "LockEnabled")
                current.UILockEnabled = true;
            if (((CheckBox)sender).Name == "HideEnabled")
                current.OverlayHideEnabled = true;

            Settings.WriteSetting<HotkeySettings>("Hotkeys", current);
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

        public void DragWindow(object sender, MouseButtonEventArgs args)
        {
            DragMove();
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
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                if (mod1 == 0)
                    mod1 = 2; // Representing Ctrl
                else if (mod2 == 0)
                    mod2 = 2; // Representing Ctrl
            }

            // Check for Shift modifier
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            {
                if (mod1 == 0)
                    mod1 = 4; // Representing Shift
                else if (mod2 == 0)
                    mod2 = 4; // Representing Shift
            }

            // Check for Alt modifier
            if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
            {
                if (mod1 == 0)
                    mod1 = 1; // Representing Alt
                else if (mod2 == 0)
                    mod2 = 1; // Representing Alt
            }

            // Capture the key stroke if it's not a modifier
            if (e.Key != Key.LeftCtrl && e.Key != Key.RightCtrl &&
                e.Key != Key.LeftShift && e.Key != Key.RightShift &&
                e.Key != Key.LeftAlt && e.Key != Key.RightAlt)
            {
                keyStroke = KeyInterop.VirtualKeyFromKey(e.Key);
            }

            // TODO: Store these values in your settings
            // Example: Update your configuration here based on which TextBox sent the event
            // You might identify the TextBox by Name or Tag and then update the corresponding hotkey settings

            // For demonstration: Just displaying the captured keys
            //MessageBox.Show($"Modifiers: {mod1} {mod2}, KeyStroke: {keyStroke}");
            SaveSetting(textBox, mod1, mod2, keyStroke);
            UpdateTextBoxDisplay(textBox, mod1, mod2, keyStroke);
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
            Settings.WriteSetting<HotkeySettings>("Hotkeys", current);
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
            // This converts the keyCode to a Key enum and then to a string
            // You might want to handle specific cases or provide more user-friendly names
            var key = KeyInterop.KeyFromVirtualKey(keyCode);

            // Special handling for certain keys can go here, if necessary
            // For example, converting Key.OemPlus to "+"

            return key.ToString();
        }

    }
}
