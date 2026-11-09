using System;
using System.Runtime.InteropServices;

namespace SWTORCombatParser.Utilities.Native;

internal static class X11Session
{
    // XSetErrorHandler is process-wide. Serialize our requests, trap only our display,
    // and forward Avalonia/other clients' errors to the handler we temporarily replaced.
    private static readonly object Gate = new();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ErrorHandler(IntPtr display, ref XErrorEvent error);
    [StructLayout(LayoutKind.Sequential)]
    private struct XErrorEvent
    {
        public int Type;
        public IntPtr Display, ResourceId, Serial;
        public byte ErrorCode, RequestCode, MinorCode;
    }

    internal static T Run<T>(Func<IntPtr, T> action)
    {
        lock (Gate)
        {
            var display = XOpenDisplay(IntPtr.Zero);
            if (display == IntPtr.Zero)
                throw new InvalidOperationException("Cannot connect to DISPLAY. Start Orbs in an X11 session or enable XWayland.");
            var previous = IntPtr.Zero;
            string? failure = null;
            ErrorHandler handler = (IntPtr source, ref XErrorEvent error) =>
            {
                if (source != display) return ForwardError(previous, source, ref error);
                failure = $"X11 error {error.ErrorCode} (request {error.RequestCode}.{error.MinorCode}).";
                return 0;
            };
            var old = XSetErrorHandler(Marshal.GetFunctionPointerForDelegate(handler));
            previous = old;
            try
            {
                var result = action(display);
                XSync(display, 0);
                if (failure != null) throw new InvalidOperationException(failure);
                return result;
            }
            finally
            {
                XSync(display, 0);
                XCloseDisplay(display);
                XSetErrorHandler(old);
                GC.KeepAlive(handler);
            }
        }
    }

    private static unsafe int ForwardError(IntPtr previous, IntPtr display, ref XErrorEvent error)
    {
        if (previous == IntPtr.Zero) return 0;
        // Avalonia's previous handler can itself be a managed callback. Converting
        // its thunk to our delegate type can throw InvalidCastException. Call the
        // native ABI directly and leave ownership of that callback with Avalonia.
        fixed (XErrorEvent* pointer = &error)
            return ((delegate* unmanaged[Cdecl]<IntPtr, XErrorEvent*, int>)previous)(display, pointer);
    }

    [DllImport("libX11.so.6")] private static extern IntPtr XOpenDisplay(IntPtr name);
    [DllImport("libX11.so.6")] private static extern int XCloseDisplay(IntPtr display);
    [DllImport("libX11.so.6")] private static extern int XSync(IntPtr display, int discard);
    [DllImport("libX11.so.6")] private static extern IntPtr XSetErrorHandler(IntPtr handler);
    [DllImport("libX11.so.6")] internal static extern int XFree(IntPtr data);
}
