using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using SWTORCombatParser.DataStructures.Hotkeys;

namespace SWTORCombatParser.Utilities
{
    public class HotkeyHandler
    {
        private IntPtr _handle;
        private bool _isInitialized = false;

        // Windows-specific constants
#if WINDOWS
        private const int MOD_CONTROL = 0x0002;
        private const int MOD_ALT = 0x0001;
        private const int WM_HOTKEY = 0x0312;
        private const int MOD_NONE = 0x0000;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
#endif

        // macOS-specific imports and constants
#if MACOS
        [DllImport("/System/Library/Frameworks/CoreServices.framework/Versions/A/Frameworks/CarbonCore.framework/Versions/A/Carbon")]
        public static extern int RegisterEventHotKey(int virtualKey, int modifiers, IntPtr hotKeyId, IntPtr eventTarget, int options, out IntPtr hotKeyRef);

        [DllImport("/System/Library/Frameworks/CoreServices.framework/Versions/A/Frameworks/CarbonCore.framework/Versions/A/Carbon")]
        public static extern void UnregisterEventHotKey(IntPtr hotKeyRef);

        private IntPtr _hotKeyRef1;
        private IntPtr _hotKeyRef2;
        private IntPtr _hotKeyRef3;
#endif

        public static bool IgnoreHotkeys = false;

        public void Init()
        {
#if WINDOWS
            // Check if we're using the ClassicDesktop style and retrieve the handle
            if(Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var handle = desktop.MainWindow.TryGetPlatformHandle();
                if (handle.Handle != null)
                {
                    _handle = handle.Handle;
                }

                // Ensure we have a valid window handle for Windows
                if (_handle == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Failed to get window handle for hotkey registration.");
                }
            }
#endif

            // Initialize hotkey registration
            _isInitialized = true;
            UpdateKeys();
        }


        public void UpdateKeys()
        {
            var hotkeySettings = Settings.ReadSettingOfType<HotkeySettings>("Hotkeys");

            if (hotkeySettings.UILockEnabled)
            {
                RegHotKey(1, hotkeySettings.UILockHotkeyStroke, hotkeySettings.UILockHotkeyMod1, hotkeySettings.UILockHotkeyMod2);
            }
            if (hotkeySettings.HOTRefreshEnabled)
            {
                RegHotKey(2, hotkeySettings.HOTRefreshHotkeyStroke, hotkeySettings.HOTRefreshHotkeyMod1, hotkeySettings.HOTRefreshHotkeyMod2);
            }
            if (hotkeySettings.OverlayHideEnabled)
            {
                RegHotKey(3, hotkeySettings.OverlayHideHotkeyStroke, hotkeySettings.OverlayHideHotkeyMod1, hotkeySettings.OverlayHideHotkeyMod2);
            }
        }

        // Register hotkeys for Windows and macOS
        public void RegHotKey(int hotkeyId, int key, int modifier1 = 0, int modifier2 = 0)
        {
            if (!_isInitialized)
                return;

            int combinedModifiers = modifier1 | modifier2;

#if WINDOWS
            if (_handle != IntPtr.Zero)
            {
                if (RegisterHotKey(_handle, hotkeyId, combinedModifiers, key))
                {
                    Console.WriteLine("Hotkey registered successfully on Windows.");
                }
                else
                {
                    Console.WriteLine("Error: Unable to register hotkey on Windows.");
                }
            }
#elif MACOS
            int macModifiers = ConvertModifiersToMac(combinedModifiers);
            IntPtr eventTarget = IntPtr.Zero; // Use default for global hotkeys
            int result = RegisterEventHotKey(key, macModifiers, IntPtr.Zero, eventTarget, 0, out IntPtr hotKeyRef);

            if (hotkeyId == 1) _hotKeyRef1 = hotKeyRef;
            else if (hotkeyId == 2) _hotKeyRef2 = hotKeyRef;
            else if (hotkeyId == 3) _hotKeyRef3 = hotKeyRef;

            if (result == 0)
            {
                Console.WriteLine("Hotkey registered successfully on macOS.");
            }
            else
            {
                Console.WriteLine("Error: Unable to register hotkey on macOS.");
            }
#endif
        }

        // macOS modifier conversion
#if MACOS
        private int ConvertModifiersToMac(int combinedModifiers)
        {
            int result = 0;
            if ((combinedModifiers & MOD_CONTROL) != 0) result |= (1 << 18); // Cmd key
            if ((combinedModifiers & MOD_ALT) != 0) result |= (1 << 19); // Option key
            return result;
        }
#endif

        public void UnregAll()
        {
            UnregHotKey(1);
            UnregHotKey(2);
            UnregHotKey(3);
        }

        public void UnregHotKey(int id)
        {
#if WINDOWS
            UnregisterHotKey(_handle, id);
#elif MACOS
            if (id == 1) UnregisterEventHotKey(_hotKeyRef1);
            else if (id == 2) UnregisterEventHotKey(_hotKeyRef2);
            else if (id == 3) UnregisterEventHotKey(_hotKeyRef3);
#endif
        }

        // Event triggers for hotkeys
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
