using SWTORCombatParser.DataStructures.Hotkeys;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace SWTORCombatParser.Utilities
{
    public class HotkeyHandler
    {
        private nint _handle;
        // Define modifier constants
        // Constants used for the Windows API
        private const int MOD_CONTROL = 0x0002;
        private const int MOD_ALT = 0x0001;
        private const int WM_HOTKEY = 0x0312;
        private const int MOD_NONE = 0x0000;

        private bool _isInitialized = false;
        // RegisterHotKey and UnregisterHotKey methods from Windows API
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        public static bool IgnoreHotkeys = false;
        public void Init(nint handle)
        {
            _handle = handle;
            var source = HwndSource.FromHwnd(_handle);
            source.AddHook(HwndHook);
            _isInitialized = true;
            UpdateKeys();
        }
        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if(IgnoreHotkeys)
                return IntPtr.Zero;
            // Handle the WM_HOTKEY message
            if (msg == WM_HOTKEY)
            {
                if (wParam.ToInt32() == 1)
                {
                    FireLockOverlay();
                    handled = true; // Mark the message as handled
                }
                if (wParam.ToInt32() == 2)
                {
                    FireRefreshHots();
                    handled = true; // Mark the message as handled
                }
                if (wParam.ToInt32() == 3)
                {
                    FireHideOverlays();
                    handled = true; // Mark the message as handled
                }
            }
            return IntPtr.Zero;
        }
        public void UpdateKeys()
        {
            var hotkeySettings = Settings.ReadSettingOfType<HotkeySettings>("Hotkeys");
            if (hotkeySettings.UILockEnabled)
            {
                //register lock hotkey for UI Unlock
                RegHotKey(1, hotkeySettings.UILockHotkeyStroke, hotkeySettings.UILockHotkeyMod1, hotkeySettings.UILockHotkeyMod2);
            }
            if (hotkeySettings.HOTRefreshEnabled)
            {
                //register lock hotkey for UI Unlock
                RegHotKey(2, hotkeySettings.HOTRefreshHotkeyStroke, hotkeySettings.HOTRefreshHotkeyMod1, hotkeySettings.HOTRefreshHotkeyMod2);
            }
            if (hotkeySettings.OverlayHideEnabled)
            {
                //register lock hotkey for UI Unlock
                RegHotKey(3, hotkeySettings.OverlayHideHotkeyStroke, hotkeySettings.OverlayHideHotkeyMod1, hotkeySettings.OverlayHideHotkeyMod2);
            }
        }
        // Method to register a hotkey
        public void RegHotKey(int hotkeyId, int key, int modifier1 = MOD_NONE, int modifier2 = MOD_NONE)
        {
            if (!_isInitialized)
                return;
            int combinedModifiers = modifier1 | modifier2; // Combine modifiers

            // Register the hotkey
            if (RegisterHotKey(_handle, hotkeyId, combinedModifiers, key))
            {
                // Hotkey registered successfully
                Console.WriteLine("Hotkey registered successfully.");
            }
            else
            {
                // Handle registration failure
                Console.WriteLine("Error: Unable to register hotkey.");
            }
        }
        public void UnregAll()
        {
            UnregHotKet(1);
            UnregHotKet(2);
            UnregHotKet(3);
        }
        public void UnregHotKet(int id)
        {
            // Unregister the hotkey
            UnregisterHotKey(_handle, id);
        }

        public static event Action OnLockOverlayHotkey = delegate { };
        public static void FireLockOverlay()
        {
            Debug.WriteLine("Locking hotkey fired");
            OnLockOverlayHotkey();
        }
        public static event Action OnRefreshHOTsHotkey = delegate { };
        public static void FireRefreshHots()
        {
            Debug.WriteLine("Refresh HOTs hotkey fired");
            OnRefreshHOTsHotkey();
        }
        public static event Action OnHideOverlaysHotkey = delegate { };
        public static void FireHideOverlays()
        {
            Debug.WriteLine("Hide Overlays hotkey fired");
            OnHideOverlaysHotkey();
        }
    }
}
