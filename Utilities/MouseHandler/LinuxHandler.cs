#if LINUX
using System;
using System.Runtime.InteropServices;
using System.Threading;
using Avalonia;

namespace SWTORCombatParser.Utilities.MouseHandler;

public class MouseHookHandler
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
    public static Point GetCursorPosition()
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
        private const int ButtonPress = 4; // Button press event
    private const int ButtonRelease = 5; // Button release event

    // X11 functions
    [DllImport("libX11.so.6")]
    private static extern IntPtr XOpenDisplay(string displayName);

    [DllImport("libX11.so.6")]
    private static extern IntPtr XDefaultRootWindow(IntPtr display);

    [DllImport("libX11.so.6")]
    private static extern void XSelectInput(IntPtr display, IntPtr window, long eventMask);

    [DllImport("libX11.so.6")]
    private static extern void XNextEvent(IntPtr display, ref XEvent xevent);

    [StructLayout(LayoutKind.Sequential)]
    public struct XEvent
    {
        public int type;
        public XButtonEvent xbutton;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XButtonEvent
    {
        public int x;
        public int y;
        public int button;
        public int state;
        public int time;
        public IntPtr x_root;
        public IntPtr y_root;
        public IntPtr window;
        public IntPtr same_window;
        public int xevent;
    }

    public event Action<Point> MouseClicked = delegate { };

    private IntPtr _display;
    private IntPtr _rootWindow;

    public void SubscribeToClicks()
    {
        _display = XOpenDisplay(null);
        if (_display == IntPtr.Zero)
            throw new Exception("Unable to open X display.");

        _rootWindow = XDefaultRootWindow(_display);
        XSelectInput(_display, _rootWindow, ButtonPress | ButtonRelease);

        new Thread(EventLoop).Start();
    }

    public void UnsubscribeFromClicks()
    {
        if (_display != IntPtr.Zero)
        {
            XCloseDisplay(_display);
            _display = IntPtr.Zero;
        }
    }

    private void EventLoop()
    {
        XEvent e = new XEvent();
        while (_display != IntPtr.Zero)
        {
            XNextEvent(_display, ref e);
            if (e.type == ButtonPress)
            {
                var clickLocation = new Point(e.xbutton.x, e.xbutton.y);
                MouseClicked?.Invoke(clickLocation);
            }
        }
    }
}
#endif