using System;
using System.Runtime.InteropServices;

namespace SWTORCombatParser.Utilities.Native;

internal static class MacOverlayInput
{
    private const long CanJoinAllSpaces = 1L << 0;
    private const long MoveToActiveSpace = 1L << 1;
    private const long FullScreenPrimary = 1L << 7;
    private const long FullScreenAuxiliary = 1L << 8;
    private const long FullScreenNone = 1L << 9;
    private const long Primary = 1L << 16;
    private const long Auxiliary = 1L << 17;
    private const long CanJoinAllApplications = 1L << 18;

    internal static string Apply(IntPtr handle, string? descriptor, bool locked)
    {
        // Avalonia Native normally exposes NSWindow; tolerate an NSView handle too.
        if (string.Equals(descriptor, "NSView", StringComparison.OrdinalIgnoreCase))
            handle = SendPointer(handle, Selector("window"));
        else if (!string.Equals(descriptor, "NSWindow", StringComparison.OrdinalIgnoreCase))
            throw new PlatformNotSupportedException($"Unexpected macOS window handle: {descriptor}.");
        if (handle == IntPtr.Zero) throw new InvalidOperationException("The macOS overlay window is not ready.");

        SendBool(handle, Selector("setHasShadow:"), false);
        SendBool(handle, Selector("setHidesOnDeactivate:"), false);
        // All Spaces alone doesn't opt an ordinary window into another app's
        // fullscreen Space. macOS 13 added an explicit cross-application behavior.
        var behavior = SendPointer(handle, Selector("collectionBehavior")).ToInt64();
        behavior = GetOverlayCollectionBehavior(behavior, OperatingSystem.IsMacOSVersionAtLeast(13));
        SendInteger(handle, Selector("setCollectionBehavior:"), new IntPtr(behavior));
        SendBool(handle, Selector("setIgnoresMouseEvents:"), locked);
        if (GetBool(handle, Selector("ignoresMouseEvents")) != locked)
            throw new InvalidOperationException("macOS did not confirm overlay mouse transparency.");
        return locked ? "macOS mouse transparency verified." : "macOS editing enabled.";
    }

    internal static long GetOverlayCollectionBehavior(long behavior, bool supportsJoiningApplications)
    {
        behavior = (behavior & ~(MoveToActiveSpace | FullScreenPrimary | FullScreenNone)) |
                   CanJoinAllSpaces | FullScreenAuxiliary;
        if (supportsJoiningApplications)
        {
            // These three Stage Manager/fullscreen roles are mutually exclusive.
            // Preserve other flags supplied by Avalonia, including cycling policy.
            behavior = (behavior & ~(Primary | Auxiliary)) | CanJoinAllApplications;
        }
        return behavior;
    }

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "sel_registerName")] private static extern IntPtr Selector(string name);
    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")] private static extern IntPtr SendPointer(IntPtr receiver, IntPtr selector);
    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")] private static extern void SendInteger(IntPtr receiver, IntPtr selector, IntPtr value);
    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")] private static extern void SendBool(IntPtr receiver, IntPtr selector, [MarshalAs(UnmanagedType.I1)] bool value);
    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)] private static extern bool GetBool(IntPtr receiver, IntPtr selector);
}
