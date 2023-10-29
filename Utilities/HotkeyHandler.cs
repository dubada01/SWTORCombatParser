using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
