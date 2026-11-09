using System;
using SWTORCombatParser.Utilities;

namespace SWTORCombatParser.Model.Overlays;

public readonly record struct OverlayAppearance(double ContentOpacity, double? BackgroundOpacity)
{
    public double ResolveBackgroundOpacity(bool locked, double lockedDefault, double unlockedDefault) =>
        locked ? BackgroundOpacity ?? lockedDefault : unlockedDefault;
}

/// <summary>Shared appearance preferences for every live overlay window.</summary>
public static class OverlayAppearanceSettings
{
    public const string ContentOpacityKey = "overlay_content_opacity";
    public const string BackgroundOpacityKey = "overlay_background_opacity";
    public static event Action<OverlayAppearance> AppearanceChanged = delegate { };

    static OverlayAppearanceSettings()
    {
        Settings.SettingsUpdated += name =>
        {
            // Read once per change, rather than twice for every visible and hidden
            // overlay during each slider movement.
            if (IsAppearanceSetting(name)) AppearanceChanged(ReadAppearance());
        };
    }

    public static double ContentOpacity => NormalizeContentOpacity(Settings.ReadSettingOfType<double?>(ContentOpacityKey));

    // Null preserves the deliberately different backgrounds of metric, raid, and utility overlays.
    public static double? BackgroundOpacity => NormalizeBackgroundOpacity(Settings.ReadSettingOfType<double?>(BackgroundOpacityKey));

    public static bool IsAppearanceSetting(string name) => name == ContentOpacityKey || name == BackgroundOpacityKey;

    public static OverlayAppearance ReadAppearance() => new(ContentOpacity, BackgroundOpacity);

    public static void SetContentOpacity(double opacity) =>
        Settings.WriteSetting(ContentOpacityKey, NormalizeContentOpacity(opacity));

    public static void SetBackgroundOpacity(double? opacity) =>
        Settings.WriteSetting(BackgroundOpacityKey, NormalizeBackgroundOpacity(opacity));

    public static void Reset()
    {
        SetContentOpacity(1);
        SetBackgroundOpacity(null);
    }

    public static double ResolveBackgroundOpacity(bool locked, double lockedDefault, double unlockedDefault) =>
        ReadAppearance().ResolveBackgroundOpacity(locked, lockedDefault, unlockedDefault);

    private static double NormalizeContentOpacity(double? value) =>
        value.HasValue && double.IsFinite(value.Value) ? Math.Clamp(value.Value, 0.2, 1) : 1;

    private static double? NormalizeBackgroundOpacity(double? value) =>
        value.HasValue && double.IsFinite(value.Value) ? Math.Clamp(value.Value, 0, 1) : null;
}
