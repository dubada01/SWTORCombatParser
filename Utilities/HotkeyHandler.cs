using System;
using System.Diagnostics;

namespace SWTORCombatParser.Utilities
{
    public static class HotkeyHandler
    {
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
    }
}
