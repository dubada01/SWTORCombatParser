using System;
using System.Runtime.InteropServices;
using SkiaSharp;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.Utilities.Native;

namespace SWTORCombatParser.Model.Overlays;

public class LinuxScreenCapturer : IScreenCapture
{
    public SKBitmap CaptureScreenArea(int x, int y, int width, int height)
    {
        if (width <= 0 || height <= 0 || (long)width * height > 16_777_216)
            throw new ArgumentOutOfRangeException(nameof(width), "Select a smaller, nonempty screen area.");
        if (OverlayPlatformStatus.IsWaylandSession)
            throw new PlatformNotSupportedException("Automatic name detection needs an X11 desktop session. XWayland cannot capture the Wayland desktop. Use Assign by click for an XWayland game, or sign into Plasma (X11) when available.");
        return X11Session.Run(display =>
        {
            var root = XDefaultRootWindow(display);
            if (XGetGeometry(display, root, out _, out _, out _, out var screenWidth, out var screenHeight, out _, out _) == 0 ||
                x < 0 || y < 0 || (long)x + width > screenWidth || (long)y + height > screenHeight)
                throw new InvalidOperationException("Move the entire raid grid inside the X11 desktop before detecting names.");
            var image = XGetImage(display, root, x, y, (uint)width, (uint)height, nuint.MaxValue, 2);
            if (image == IntPtr.Zero) throw new InvalidOperationException("The X server could not capture the raid frames.");
            try
            {
                var info = Marshal.PtrToStructure<XImage>(image);
                if (info.RedMask == 0 || info.GreenMask == 0 || info.BlueMask == 0)
                    throw new PlatformNotSupportedException("Screen capture requires a TrueColor X11 visual.");
                // XGetPixel handles the server's byte order, stride and pixel depth.
                var pixels = new SKColor[checked(width * height)];
                for (var row = 0; row < height; row++)
                    for (var column = 0; column < width; column++)
                    {
                        var pixel = XGetPixel(image, column, row);
                        pixels[row * width + column] = new SKColor(Channel(pixel, info.RedMask), Channel(pixel, info.GreenMask), Channel(pixel, info.BlueMask));
                    }
                var bitmap = new SKBitmap(width, height);
                try { bitmap.Pixels = pixels; return bitmap; }
                catch { bitmap.Dispose(); throw; }
            }
            finally { XDestroyImage(image); }
        });
    }

    internal static byte Channel(nuint pixel, nuint mask)
    {
        if (mask == 0) return 0;
        while ((mask & 1) == 0) { mask >>= 1; pixel >>= 1; }
        return (byte)(((ulong)(pixel & mask) * 255 + (ulong)mask / 2) / (ulong)mask);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XImage
    {
        public int Width, Height, XOffset, Format;
        public IntPtr Data;
        public int ByteOrder, BitmapUnit, BitmapBitOrder, BitmapPad, Depth, BytesPerLine, BitsPerPixel;
        public nuint RedMask, GreenMask, BlueMask;
    }
    [DllImport("libX11.so.6")] private static extern IntPtr XDefaultRootWindow(IntPtr display);
    [DllImport("libX11.so.6")] private static extern int XGetGeometry(IntPtr display, IntPtr window, out IntPtr root, out int x, out int y, out uint width, out uint height, out uint border, out uint depth);
    [DllImport("libX11.so.6")] private static extern IntPtr XGetImage(IntPtr display, IntPtr window, int x, int y, uint width, uint height, nuint planeMask, int format);
    [DllImport("libX11.so.6")] private static extern nuint XGetPixel(IntPtr image, int x, int y);
    [DllImport("libX11.so.6")] private static extern int XDestroyImage(IntPtr image);
}
