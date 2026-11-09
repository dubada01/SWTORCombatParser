using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Threading;
using SharpHook;
using SharpHook.Reactive;
using SWTORCombatParser.DataStructures.Hotkeys;

namespace SWTORCombatParser.Utilities
{
    public class HotkeyHandler : IDisposable
    {
        private readonly IReactiveGlobalHook _hook;
        private readonly Dictionary<int, RegisteredHotkey> _registeredHotkeys = new Dictionary<int, RegisteredHotkey>();
        private bool _isInitialized = false;

        public static bool IgnoreHotkeys = false;
        internal static volatile bool GlobalInputAvailable;
        internal static event Action<Point>? GlobalMousePressed;
        private readonly CompositeDisposable _subscriptions = new();
        private bool _disposed;

        // Track the current state of modifier keys
        private readonly HashSet<SharpHook.Native.KeyCode> _activeModifiers = new HashSet<SharpHook.Native.KeyCode>();

        // Define which keys are considered modifiers
        private readonly HashSet<SharpHook.Native.KeyCode> _modifierKeys = new HashSet<SharpHook.Native.KeyCode>
        {
            SharpHook.Native.KeyCode.VcLeftShift,
            SharpHook.Native.KeyCode.VcRightShift,
            SharpHook.Native.KeyCode.VcLeftControl,
            SharpHook.Native.KeyCode.VcRightControl,
            SharpHook.Native.KeyCode.VcLeftAlt,
            SharpHook.Native.KeyCode.VcRightAlt,
        };

        public HotkeyHandler()
        {
            // Initialize the global hook
            _hook = new SimpleReactiveGlobalHook(OperatingSystem.IsWindows() ? GlobalHookType.Keyboard : GlobalHookType.All);
        }

        public void Init()
        {
            if (_isInitialized)
                return;

            // Subscribe to key pressed and released events
            _subscriptions.Add(_hook.KeyPressed.Subscribe(OnKeyPressed));
            _subscriptions.Add(_hook.KeyReleased.Subscribe(OnKeyReleased));
            _subscriptions.Add(_hook.HookEnabled.Subscribe(_ =>
            {
                GlobalInputAvailable = true;
                OverlayPlatformStatus.Report("Global input", "Available" + (OverlayPlatformStatus.IsWaylandSession ? " for XWayland applications; native Wayland input is restricted." : "."));
            }));
            _subscriptions.Add(_hook.HookDisabled.Subscribe(_ => GlobalInputAvailable = false));
            _subscriptions.Add(_hook.MousePressed.Subscribe(args =>
            {
                if (args.Data.Button == SharpHook.Native.MouseButton.Button1)
                    GlobalMousePressed?.InvokeSafely(new Point(args.Data.X, args.Data.Y));
            }));

            // Start the hook
            _ = ObserveHookAsync();

            _isInitialized = true;
            UpdateKeys();
        }

        private async Task ObserveHookAsync()
        {
            try { await _hook.RunAsync(); }
            catch (Exception error)
            {
                GlobalInputAvailable = false;
                if (!_disposed)
                    OverlayPlatformStatus.Report("Global input", "Unavailable: " + error.Message +
                        (OperatingSystem.IsMacOS() ? " Allow Orbs in Privacy & Security → Accessibility and restart." :
                         " Use the main window controls. On Wayland, global input may require an X11 session."));
            }
        }

        public void UpdateKeys()
        {
            _registeredHotkeys.Clear();
            var hotkeySettings = Settings.ReadSettingOfType<HotkeySettings>("Hotkeys");

            if (hotkeySettings.UILockEnabled)
            {
                RegisterHotKey(1, hotkeySettings.UILockHotkeyStroke, hotkeySettings.UILockHotkeyMod1, hotkeySettings.UILockHotkeyMod2);
            }
            if (hotkeySettings.HOTRefreshEnabled)
            {
                RegisterHotKey(2, hotkeySettings.HOTRefreshHotkeyStroke, hotkeySettings.HOTRefreshHotkeyMod1, hotkeySettings.HOTRefreshHotkeyMod2);
            }
            if (hotkeySettings.OverlayHideEnabled)
            {
                RegisterHotKey(3, hotkeySettings.OverlayHideHotkeyStroke, hotkeySettings.OverlayHideHotkeyMod1, hotkeySettings.OverlayHideHotkeyMod2);
            }
        }

        // Register hotkeys using SharpHook
        public void RegisterHotKey(int hotkeyId, int key, int modifier1 = 0, int modifier2 = 0)
        {
            if (!_isInitialized)
                return;

            if (_registeredHotkeys.ContainsKey(hotkeyId))
            {
                UnregisterHotKey(hotkeyId);
            }

            var hotkey = new RegisteredHotkey
            {
                Id = hotkeyId,
                Key = (SharpHook.Native.KeyCode)key,
                Modifiers = ConvertModifiers(modifier1, modifier2)
            };

            _registeredHotkeys[hotkeyId] = hotkey;
        }

        // Unregister a specific hotkey
        public void UnregisterHotKey(int hotkeyId)
        {
            if (_registeredHotkeys.ContainsKey(hotkeyId))
            {
                _registeredHotkeys.Remove(hotkeyId);
            }
        }

        // Unregister all hotkeys
        public void UnregAll()
        {
            _registeredHotkeys.Clear();
        }

        // Handle key pressed events
        private void OnKeyPressed(KeyboardHookEventArgs args)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => OnKeyPressed(args));
                return;
            }
            if (_disposed) return;
            if (IgnoreHotkeys)
                return;

            var key = args.Data.KeyCode;
            // If the pressed key is a modifier, add it to the active modifiers
            if (_modifierKeys.Contains(key))
            {
                _activeModifiers.Add(key);
                return;
            }

            // If the key is not a modifier, check against registered hotkeys
            foreach (var hotkey in _registeredHotkeys.Values)
            {
                if (args.Data.KeyCode == hotkey.Key && AreModifiersActive(hotkey.Modifiers))
                {
                    FireHotkeyEvent(hotkey.Id);
                    break; // Assuming one hotkey per key combination
                }
            }
        }

        // Handle key released events
        private void OnKeyReleased(KeyboardHookEventArgs args)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => OnKeyReleased(args));
                return;
            }
            if (_disposed) return;
            var key = args.Data.KeyCode;

            // If the released key is a modifier, remove it from the active modifiers
            if (_modifierKeys.Contains(key))
            {
                _activeModifiers.Remove(key);
            }
        }

        // Check if the required modifiers for a hotkey are currently active
        private bool AreModifiersActive(SharpHook.Native.ModifierMask requiredModifiers)
        {
            // Convert the requiredModifiers to a set of active modifier keys
            var requiredKeys = ConvertModifierMaskToKeys(requiredModifiers);

            foreach (var key in requiredKeys)
            {
                if (!_activeModifiers.Contains(key))
                    return false;
            }

            return true;
        }

        // Convert ModifierMask to individual modifier keys
        private IEnumerable<SharpHook.Native.KeyCode> ConvertModifierMaskToKeys(SharpHook.Native.ModifierMask mask)
        {
            var keys = new List<SharpHook.Native.KeyCode>();

            if (mask.HasFlag(SharpHook.Native.ModifierMask.Shift))
            {
                keys.Add(SharpHook.Native.KeyCode.VcLeftShift);
                //keys.Add(SharpHook.Native.KeyCode.VcRightShift);
            }
            if (mask.HasFlag(SharpHook.Native.ModifierMask.Ctrl))
            {
                keys.Add(SharpHook.Native.KeyCode.VcLeftControl);
               // keys.Add(SharpHook.Native.KeyCode.VcRightControl);
            }
            if (mask.HasFlag(SharpHook.Native.ModifierMask.Alt))
            {
                keys.Add(SharpHook.Native.KeyCode.VcLeftAlt);
                //keys.Add(SharpHook.Native.KeyCode.VcRightAlt);
            }


            return keys;
        }

        // Fire the corresponding event based on hotkey ID
        private void FireHotkeyEvent(int hotkeyId)
        {
            // Ensure UI updates are marshaled to the main thread if necessary
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                switch (hotkeyId)
                {
                    case 1:
                        FireLockOverlay();
                        break;
                    case 2:
                        FireRefreshHots();
                        break;
                    case 3:
                        FireHideOverlays();
                        break;
                }
            });
        }

        // Convert Windows-style modifiers to SharpHook modifiers
        private SharpHook.Native.ModifierMask ConvertModifiers(int modifier1, int modifier2)
        {
            SharpHook.Native.ModifierMask result = 0;

            if (((modifier1 | modifier2) & 0x0002) != 0) // MOD_CONTROL
                result |= SharpHook.Native.ModifierMask.Ctrl;

            if (((modifier1 | modifier2) & 0x0001) != 0) // MOD_ALT
                result |= SharpHook.Native.ModifierMask.Alt;

            if (((modifier1 | modifier2) & 0x0004) != 0) // MOD_SHIFT
                result |= SharpHook.Native.ModifierMask.Shift;

            

            return result;
        }

        // Event triggers for hotkeys
        public static event Action OnLockOverlayHotkey = delegate { };
        public static void FireLockOverlay()
        {
            OnLockOverlayHotkey();
        }

        public static event Action OnRefreshHOTsHotkey = delegate { };
        public static void FireRefreshHots()
        {
            OnRefreshHOTsHotkey();
        }

        public static event Action OnHideOverlaysHotkey = delegate { };
        public static void FireHideOverlays()
        {
            OnHideOverlaysHotkey();
        }

        // Dispose pattern to clean up the hook
        public void Dispose()
        {
            _disposed = true;
            GlobalInputAvailable = false;
            _subscriptions.Dispose();
            if (_isInitialized)
            {
                _hook.Dispose();
                _isInitialized = false;
            }
        }

        // Inner class to represent a registered hotkey
        private class RegisteredHotkey
        {
            public int Id { get; set; }
            public SharpHook.Native.KeyCode Key { get; set; }
            public SharpHook.Native.ModifierMask Modifiers { get; set; }

            public override string ToString()
            {
                return $"ID: {Id}, Key: {Key}, Modifiers: {Modifiers}";
            }
        }
    }
}
