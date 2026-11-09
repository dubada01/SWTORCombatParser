using System.Runtime.InteropServices;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Utilities.Native;
using SWTORCombatParser.Utilities;

// Run under a disposable X server: xvfb-run -a dotnet run --project .../PlatformHarness.csproj
// The test clicks real windows via XTEST, not mocks of XShape/XFixes.
var display = Native.XOpenDisplay(IntPtr.Zero);
if (display == IntPtr.Zero) throw new Exception("An X11 DISPLAY is required.");
var root = Native.XDefaultRootWindow(display);
var target = Native.XCreateSimpleWindow(display, root, 0, 0, 1200, 900, 0, 0, 0xff0000);
var overlay = Native.XCreateSimpleWindow(display, root, 100, 100, 400, 300, 0, 0, 0x0000ff);
var child = Native.XCreateSimpleWindow(display, overlay, 0, 0, 400, 300, 0, 0, 0x0000ff);
var buffer = Marshal.AllocHGlobal(24 * IntPtr.Size);
try
{
    // Match production's Topmost=true so clicking the game doesn't raise it over
    // the overlay; that would test stacking policy instead of restored input.
    var state = Native.XInternAtom(display, "_NET_WM_STATE", 0);
    var above = Native.XInternAtom(display, "_NET_WM_STATE_ABOVE", 0);
    Native.XChangeProperty(display, overlay, state, new IntPtr(4), 32, 0, new[] { above }, 1);
    foreach (var window in new[] { target, overlay, child })
    {
        Native.XSelectInput(display, window, (nint)4); // ButtonPressMask
        Native.XMapWindow(display, window);
    }
    Native.XSync(display, 0);
    Thread.Sleep(350); // Allow a WM to manage/reparent the top levels.
    // Use the managed overlay's current root coordinates; WMs can reposition it.
    Native.XTranslateCoordinates(display, overlay, root, 0, 0, out var x, out var y, out _);
    Native.XRaiseWindow(display, overlay);
    Native.XSync(display, 0);
    Thread.Sleep(150);

    foreach (var fixes in new[] { true, false })
    {
        Console.WriteLine(LinuxOverlayInput.Apply(overlay, "XID", true, fixes));
        ClickExpect(target, "locked input reaches underlying game");
        Native.XResizeWindow(display, overlay, 450, 320);
        Native.XResizeWindow(display, child, 450, 320);
        Native.XSync(display, 0);
        LinuxOverlayInput.Apply(overlay, "XID", true, fixes);
        ClickExpect(target, "resize preserves click-through");
        LinuxOverlayInput.Apply(overlay, "XID", false, fixes);
        ClickExpect(child, "unlock restores renderer-child input");
        Native.XUnmapWindow(display, overlay);
        Native.XSync(display, 0);
        Thread.Sleep(100);
        Native.XChangeProperty(display, overlay, state, new IntPtr(4), 32, 0, new[] { above }, 1);
        Native.XMapWindow(display, overlay);
        Native.XSync(display, 0);
        Thread.Sleep(350); // Production schedules repair after the WM has remapped the frame.
        LinuxOverlayInput.Apply(overlay, "XID", true, fixes);
        ClickExpect(target, "remap restores click-through");
    }
    Native.XUnmapWindow(display, overlay);
    Native.XSync(display, 0);
    Native.XTranslateCoordinates(display, target, root, 0, 0, out var tx, out var ty, out _);
    if (OverlayPlatformStatus.IsWaylandSession)
    {
        try { new LinuxScreenCapturer().CaptureScreenArea(tx + 20, ty + 20, 20, 20); throw new Exception("Wayland capture silently accepted."); }
        catch (PlatformNotSupportedException) { Console.WriteLine("PASS: Wayland capture reports its limitation"); }
    }
    else
    {
        using var capture = new LinuxScreenCapturer().CaptureScreenArea(tx + 20, ty + 20, 20, 20);
        if (capture.GetPixel(10, 10).Red < 240 || capture.GetPixel(10, 10).Blue > 10)
            throw new Exception("X11 capture pixel conversion failed.");
        Console.WriteLine("PASS: X11 screen capture returns expected RGB pixels");
    }
    Native.XDestroyWindow(display, overlay);
    Native.XSync(display, 0);
    try { LinuxOverlayInput.Apply(overlay, "XID", true); throw new Exception("Dead window accepted."); }
    catch (InvalidOperationException) { Console.WriteLine("PASS: destroyed window fails without terminating process"); }
    try { LinuxOverlayInput.Apply(target, "Wayland", true); throw new Exception("Wrong backend accepted."); }
    catch (PlatformNotSupportedException) { Console.WriteLine("PASS: native Wayland handle rejected explicitly"); }
    var forwarded = 0;
    Native.ForeignErrorHandler foreignHandler = (_, _) => { forwarded++; return 0; };
    var previousHandler = Native.XSetErrorHandler(Marshal.GetFunctionPointerForDelegate(foreignHandler));
    try
    {
        X11Session.Run(_ =>
        {
            // Deliberately fail on another connection while our error trap is active.
            Native.XDestroyWindow(display, new IntPtr(0x7fffffff));
            Native.XSync(display, 0);
            return true;
        });
        if (forwarded != 1) throw new Exception("The application's original X11 error handler was not preserved.");
        Console.WriteLine("PASS: unrelated errors forwarded to existing managed X11 handler");
    }
    finally { Native.XSetErrorHandler(previousHandler); GC.KeepAlive(foreignHandler); }
    Console.WriteLine("ALL PLATFORM CHECKS PASSED");

    void ClickExpect(IntPtr expected, string description)
    {
        Thread.Sleep(100); // Allow ShapeNotify to update any WM-owned frame.
        while (Native.XPending(display) > 0) Native.XNextEvent(display, buffer);
        Native.XTranslateCoordinates(display, overlay, root, 60, 60, out var px, out var py, out _);
        Native.XTestFakeMotionEvent(display, -1, px, py, 0);
        Native.XTestFakeButtonEvent(display, 1, 1, 0);
        Native.XTestFakeButtonEvent(display, 1, 0, 0);
        Native.XSync(display, 0);
        Thread.Sleep(50);
        if (Native.XCheckWindowEvent(display, expected, (nint)4, buffer) == 0)
            throw new Exception("FAIL: " + description);
        foreach (var other in new[] { target, overlay, child }.Where(w => w != expected))
            if (Native.XCheckWindowEvent(display, other, (nint)4, buffer) != 0)
                throw new Exception("Unexpected second click recipient: " + description);
        Console.WriteLine("PASS: " + description);
    }
}
finally
{
    Marshal.FreeHGlobal(buffer);
    Native.XDestroyWindow(display, target);
    Native.XCloseDisplay(display);
}

internal static class Native
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int ForeignErrorHandler(IntPtr display, IntPtr error);
    [DllImport("libX11.so.6")] internal static extern IntPtr XSetErrorHandler(IntPtr handler);
    [DllImport("libX11.so.6")] internal static extern IntPtr XInternAtom(IntPtr d, string name, int onlyIfExists);
    [DllImport("libX11.so.6")] internal static extern int XChangeProperty(IntPtr d, IntPtr w, IntPtr property, IntPtr type, int format, int mode, IntPtr[] data, int count);
    [DllImport("libX11.so.6")] internal static extern IntPtr XOpenDisplay(IntPtr name);
    [DllImport("libX11.so.6")] internal static extern int XCloseDisplay(IntPtr display);
    [DllImport("libX11.so.6")] internal static extern IntPtr XDefaultRootWindow(IntPtr display);
    [DllImport("libX11.so.6")] internal static extern IntPtr XCreateSimpleWindow(IntPtr d, IntPtr parent, int x, int y, uint w, uint h, uint border, nuint borderPixel, nuint background);
    [DllImport("libX11.so.6")] internal static extern int XMapWindow(IntPtr d, IntPtr w);
    [DllImport("libX11.so.6")] internal static extern int XUnmapWindow(IntPtr d, IntPtr w);
    [DllImport("libX11.so.6")] internal static extern int XRaiseWindow(IntPtr d, IntPtr w);
    [DllImport("libX11.so.6")] internal static extern int XMoveWindow(IntPtr d, IntPtr w, int x, int y);
    [DllImport("libX11.so.6")] internal static extern int XResizeWindow(IntPtr d, IntPtr w, uint width, uint height);
    [DllImport("libX11.so.6")] internal static extern int XDestroyWindow(IntPtr d, IntPtr w);
    [DllImport("libX11.so.6")] internal static extern int XSelectInput(IntPtr d, IntPtr w, nint mask);
    [DllImport("libX11.so.6")] internal static extern int XSync(IntPtr d, int discard);
    [DllImport("libX11.so.6")] internal static extern int XPending(IntPtr d);
    [DllImport("libX11.so.6")] internal static extern int XNextEvent(IntPtr d, IntPtr buffer);
    [DllImport("libX11.so.6")] internal static extern int XCheckWindowEvent(IntPtr d, IntPtr w, nint mask, IntPtr buffer);
    [DllImport("libX11.so.6")] internal static extern int XTranslateCoordinates(IntPtr d, IntPtr source, IntPtr dest, int x, int y, out int dx, out int dy, out IntPtr child);
    [DllImport("libXtst.so.6")] internal static extern int XTestFakeMotionEvent(IntPtr d, int screen, int x, int y, nuint delay);
    [DllImport("libXtst.so.6")] internal static extern int XTestFakeButtonEvent(IntPtr d, uint button, int pressed, nuint delay);
}
