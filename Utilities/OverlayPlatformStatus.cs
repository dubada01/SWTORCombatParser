using System;
using System.Collections.Generic;
using System.Linq;

namespace SWTORCombatParser.Utilities;

// Shared by settings and native overlays; reports failures even if the overlay had to hide.
public static class OverlayPlatformStatus
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, string> Messages = new();
    public static event Action? Changed;

    public static bool IsWaylandSession => IsWayland(
        Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"), Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));

    internal static bool IsWayland(string? sessionType, string? waylandDisplay) =>
        string.Equals(sessionType, "wayland", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(waylandDisplay);

    public static string Guidance => OperatingSystem.IsLinux()
        ? $"Desktop: {Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? "unknown"}; " +
          $"session: {(IsWaylandSession ? "Wayland / XWayland" : "X11")}. " +
          "KDE Plasma is supported through X11 or XWayland. Use SWTOR in borderless/windowed mode. " +
          "In KWin Window Rules, match the overlay title ‘Orbs ·’, then set Keep above and All desktops if needed. " +
          "A compositor must be enabled for transparent backgrounds. " +
          (IsWaylandSession ? "Wayland restricts global hotkeys and screen capture; XWayland cannot inspect native Wayland windows. " : "") +
          "If graphics are broken, restart with ORBS_SOFTWARE_RENDERING=1."
        : OperatingSystem.IsMacOS()
            ? "Use SWTOR in windowed/borderless mode. Allow Orbs in Privacy & Security → Accessibility for global hotkeys and click assignment, " +
              "and Screen Recording for Detect names. Restart Orbs after changing permissions. Overlay mouse transparency needs neither permission."
            : "Lock overlays to pass mouse input to the game. Use borderless/windowed mode if fullscreen hides overlays.";

    public static string Summary
    {
        get { lock (Gate) return string.Join(Environment.NewLine, Messages.OrderBy(p => p.Key).Select(p => $"{p.Key}: {p.Value}")); }
    }

    public static void Report(string key, string message)
    {
        lock (Gate)
        {
            if (Messages.TryGetValue(key, out var previous) && previous == message) return;
            Messages[key] = message;
        }
        // Diagnostics must never prevent a failed overlay from being hidden.
        try { Logging.LogInfo($"Overlay platform [{key}]: {message}", false, false); }
        catch (Exception error) { System.Diagnostics.Trace.WriteLine(error); }
        Changed?.Invoke();
    }

    public static void Remove(string key)
    {
        lock (Gate) { if (!Messages.Remove(key)) return; }
        Changed?.Invoke();
    }
}
