
namespace SWTORCombatParser.DataStructures.Hotkeys
{
    internal class HotkeySettings
    {
        public int UILockHotkeyMod1 { get; set; }
        public int UILockHotkeyMod2 { get; set; }
        public int UILockHotkeyStroke { get; set; }
        public bool UILockEnabled { get; set; }

        public int HOTRefreshHotkeyMod1 { get; set; }
        public int HOTRefreshHotkeyMod2 { get; set; }
        public int HOTRefreshHotkeyStroke { get; set; }
        public bool HOTRefreshEnabled { get; set; }

        public int OverlayHideHotkeyMod1 { get; set; }
        public int OverlayHideHotkeyMod2 { get; set; }
        public int OverlayHideHotkeyStroke { get; set; }
        public bool OverlayHideEnabled { get; set; }
    }
}
