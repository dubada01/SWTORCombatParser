using System;
using SWTORCombatParser.Utilities;

namespace SWTORCombatParser.Model.Overlays;

/// <summary>Presentation is independent of window geometry, role layouts and input transparency.</summary>
public static class OverlayPresentationSettings
{
    public const string Prefix = "overlay_presentation_";
    public static string[] Styles { get; } = { "Compact", "Detailed" };
    public static int Read(string overlayKey) =>
        Settings.ReadSettingOfType<int?>(Prefix + overlayKey) == 1 ? 1 : 0;
    public static void Write(string overlayKey, int style)
    {
        if (style is 0 or 1 && Read(overlayKey) != style)
            Settings.WriteSetting(Prefix + overlayKey, style);
    }
}
