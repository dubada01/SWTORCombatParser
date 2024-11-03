#if LINUX
using System;
using SkiaSharp;
using System.Runtime.InteropServices;

namespace SWTORCombatParser.Model.Overlays
{
    public class LinuxScreenCapturer : IScreenCapture
    {
        [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
        private static extern IntPtr CGWindowListCreateImage(CGRect screenRect, CGWindowListOption option, uint windowID, CGWindowImageOption imageOption);

        [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
        private static extern IntPtr CGDataProviderCopyData(IntPtr provider);

        [DllImport("/System/Library/Frameworks.CoreGraphics.framework/CoreGraphics")]
        private static extern IntPtr CGImageGetDataProvider(IntPtr image);

        [DllImport("/System/Library/Frameworks.CoreGraphics.framework/CoreGraphics")]
        private static extern IntPtr CFDataGetBytePtr(IntPtr cfData);

        [DllImport("/System/Library/Frameworks.CoreGraphics.framework/CoreGraphics")]
        private static extern long CFDataGetLength(IntPtr cfData);

        [DllImport("/System/Library/Frameworks.CoreGraphics.framework/CoreGraphics")]
        private static extern void CFRelease(IntPtr cfData);

        public SKBitmap CaptureScreenArea(int x, int y, int width, int height)
        {
            var screenRect = new CGRect(x, y, width, height);
            IntPtr screenImage = CGWindowListCreateImage(screenRect, CGWindowListOption.OnScreenOnly, 0, CGWindowImageOption.Default);

            IntPtr dataProvider = CGImageGetDataProvider(screenImage);
            IntPtr cfData = CGDataProviderCopyData(dataProvider);

            long length = CFDataGetLength(cfData);
            byte[] buffer = new byte[length];
            Marshal.Copy(CFDataGetBytePtr(cfData), buffer, 0, (int)length);

            CFRelease(cfData); // Release CFData to prevent memory leak

            using (var skData = SKData.CreateCopy(buffer))
            {
                return SKBitmap.Decode(skData);
            }
        }
    }

    // CGRect struct
    [StructLayout(LayoutKind.Sequential)]
    public struct CGRect
    {
        public double X;
        public double Y;
        public double Width;
        public double Height;

        public CGRect(double x, double y, double width, double height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }

    // Enum for CGWindowListOption
    public enum CGWindowListOption : uint
    {
        All = 0,
        OnScreenOnly = 1,
        OnScreenAboveWindow = 2,
        OnScreenBelowWindow = 4,
        IncludingWindow = 8,
        ExcludeDesktopElements = 16
    }

    // Enum for CGWindowImageOption
    public enum CGWindowImageOption : uint
    {
        Default = 0,
        BoundsIgnoreFraming = 1,
        ShouldBeOpaque = 2,
        OnlyShadows = 4
    }
}
#endif
