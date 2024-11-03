using System;
using System.Runtime.InteropServices;
using Avalonia;

namespace SWTORCombatParser.Utilities.MouseHandler;

public static class LinuxHandler
{
// P/Invoke for Ubuntu X11 library
    [DllImport("libX11.so")]
    private static extern IntPtr XOpenDisplay(IntPtr display);

    [DllImport("libX11.so")]
    private static extern int XCloseDisplay(IntPtr display);

    [DllImport("libX11.so")]
    private static extern int DefaultScreen(IntPtr display);

    [DllImport("libX11.so")]
    private static extern IntPtr RootWindow(IntPtr display, int screenNumber);

    [DllImport("libX11.so")]
    private static extern int XQueryPointer(IntPtr display, IntPtr w, out IntPtr rootReturn, out IntPtr childReturn,
        out int rootXReturn, out int rootYReturn, out int winXReturn, out int winYReturn,
        out uint maskReturn);
    // Ubuntu-specific function
    public static Point GetCursorPositionUbuntu()
    {
        IntPtr display = XOpenDisplay(IntPtr.Zero);
        if (display == IntPtr.Zero)
        {
            throw new Exception("Unable to open X11 display.");
        }

        int screen = DefaultScreen(display);
        IntPtr rootWindow = RootWindow(display, screen);

        int rootX, rootY, winX, winY;
        IntPtr childWindow, rootWindowReturn;
        uint maskReturn;

        if (XQueryPointer(display, rootWindow, out rootWindowReturn, out childWindow,
                out rootX, out rootY, out winX, out winY, out maskReturn) == 0)
        {
            throw new Exception("Unable to query pointer position.");
        }

        XCloseDisplay(display);

        return new Point(rootX, rootY);
    }
}