using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace SWTORCombatParser.Utilities.Native;

internal static class LinuxOverlayInput
{
    private const int ShapeInput = 2;

    internal static string Apply(IntPtr window, string? descriptor, bool locked, bool preferXFixes = true)
    {
        if (window == IntPtr.Zero || !string.Equals(descriptor, "XID", StringComparison.OrdinalIgnoreCase))
            throw new PlatformNotSupportedException("Overlay has no X11 window. Use the X11/XWayland backend; native Wayland overlays are not supported by this build.");
        return X11Session.Run(display =>
        {
            if (XShapeQueryVersion(display, out var major, out var minor) == 0 || !SupportsInputShape(major, minor))
                throw new PlatformNotSupportedException("The X server needs SHAPE 1.1 for click-through overlays.");
            var fixes = preferXFixes && HasXFixes(display);
            var windows = Descendants(display, window);
            var region = IntPtr.Zero;
            try
            {
                if (fixes && locked)
                {
                    region = XFixesCreateRegion(display, IntPtr.Zero, 0);
                    if (region == IntPtr.Zero) throw new InvalidOperationException("XFixes could not create an empty input region.");
                }
                // Avalonia's GPU renderer may create child windows. Apply to those too,
                // but never to the WM's frame/ancestor (which belongs to another client).
                foreach (var target in windows)
                {
                    if (fixes)
                        XFixesSetWindowShapeRegion(display, target, ShapeInput, 0, 0, region);
                    else if (locked)
                        XShapeCombineRectangles(display, target, ShapeInput, 0, 0, IntPtr.Zero, 0, 0, 0);
                    else
                        XShapeCombineMask(display, target, ShapeInput, 0, 0, IntPtr.Zero, 0);

                    var rectangles = XShapeGetRectangles(display, target, ShapeInput, out var count, out _);
                    try
                    {
                        if (locked ? count != 0 : count == 0)
                            throw new InvalidOperationException("The X server did not confirm the requested overlay input region.");
                    }
                    finally { if (rectangles != IntPtr.Zero) X11Session.XFree(rectangles); }
                }
                return $"{(fixes ? "XFixes" : "SHAPE fallback")}: {(locked ? "click-through verified" : "editing enabled")} ({windows.Count} native windows).";
            }
            finally { if (region != IntPtr.Zero) XFixesDestroyRegion(display, region); }
        });
    }

    internal static bool SupportsInputShape(int major, int minor) => major > 1 || major == 1 && minor >= 1;

    private static bool HasXFixes(IntPtr display)
    {
        try
        {
            var major = 2;
            var minor = 0;
            return XFixesQueryVersion(display, ref major, ref minor) != 0 && major >= 2;
        }
        catch (DllNotFoundException) { return false; }
        catch (EntryPointNotFoundException) { return false; }
    }

    private static List<IntPtr> Descendants(IntPtr display, IntPtr window)
    {
        var result = new List<IntPtr> { window };
        for (var index = 0; index < result.Count; index++)
        {
            if (result.Count > 256) throw new InvalidOperationException("Unexpected overlay window hierarchy.");
            if (XQueryTree(display, result[index], out _, out _, out var children, out var count) == 0)
                throw new InvalidOperationException("The overlay window is no longer available.");
            try
            {
                for (var i = 0; i < count; i++) result.Add(Marshal.ReadIntPtr(children, checked(i * IntPtr.Size)));
            }
            finally { if (children != IntPtr.Zero) X11Session.XFree(children); }
        }
        return result;
    }

    [DllImport("libX11.so.6")] private static extern int XQueryTree(IntPtr d, IntPtr w, out IntPtr root, out IntPtr parent, out IntPtr children, out int count);
    [DllImport("libXext.so.6")] private static extern int XShapeQueryVersion(IntPtr d, out int major, out int minor);
    [DllImport("libXext.so.6")] private static extern void XShapeCombineRectangles(IntPtr d, IntPtr w, int kind, int x, int y, IntPtr rectangles, int count, int operation, int ordering);
    [DllImport("libXext.so.6")] private static extern void XShapeCombineMask(IntPtr d, IntPtr w, int kind, int x, int y, IntPtr mask, int operation);
    [DllImport("libXext.so.6")] private static extern IntPtr XShapeGetRectangles(IntPtr d, IntPtr w, int kind, out int count, out int ordering);
    [DllImport("libXfixes.so.3")] private static extern int XFixesQueryVersion(IntPtr d, ref int major, ref int minor);
    [DllImport("libXfixes.so.3")] private static extern IntPtr XFixesCreateRegion(IntPtr d, IntPtr rectangles, int count);
    [DllImport("libXfixes.so.3")] private static extern void XFixesSetWindowShapeRegion(IntPtr d, IntPtr w, int kind, int x, int y, IntPtr region);
    [DllImport("libXfixes.so.3")] private static extern void XFixesDestroyRegion(IntPtr d, IntPtr region);
}
