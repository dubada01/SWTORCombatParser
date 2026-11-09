using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using SkiaSharp;

namespace SWTORCombatParser.Model.Overlays
{
    public static class RaidFrameScreenGrab
    {
        private static IScreenCapture CreateScreenCapturer()
        {
#if WINDOWS
            return new WindowsScreenCapturer();
#elif MACOS
            return new MacOSScreenCapturer();
#elif LINUX
            return new LinuxScreenCapturer();
#else
            throw new PlatformNotSupportedException("Screen capture is not supported on this platform.");
#endif
        }

        public static MemoryStream GetRaidFrameBitmapStream(Point topLeft, int width, int height, int rowsCount,
            double? overlayScale = null, bool maskOverlayNames = true)
        {
            if (width <= 0 || height <= 0 || rowsCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "Capture dimensions and row count must be positive.");

            var scale = overlayScale ?? Dispatcher.UIThread.Invoke(() =>
                Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                    ? desktop.MainWindow?.RenderScaling ?? 1d : 1d);
            using var bitmap = CreateScreenCapturer().CaptureScreenArea((int)topLeft.X, (int)topLeft.Y, width, height);
            if (bitmap == null || bitmap.Width == 0 || bitmap.Height == 0)
                throw new InvalidOperationException("Failed to capture the raid frame.");
            if (maskOverlayNames)
            {
                // Capture backends may return more pixels than requested (for example Retina).
                RemoveOverlayNames(bitmap, rowsCount, scale * bitmap.Height / height);
            }
            return EncodeRaidFramePng(bitmap);
        }

        public static void RemoveOverlayNames(SKBitmap bitmap, int rowsCount, double overlayScale = 1d)
        {
            if (rowsCount <= 0 || !double.IsFinite(overlayScale) || overlayScale <= 0)
                throw new ArgumentOutOfRangeException(nameof(rowsCount));

            var pixelsToMask = (int)Math.Ceiling(16.5d * overlayScale);
            var pixels = bitmap.Pixels;
            for (var row = 0; row < rowsCount; row++)
            {
                // Round each boundary independently; rounding the row height first accumulates drift.
                var start = (int)Math.Floor(row * bitmap.Height / (double)rowsCount);
                var end = Math.Min(bitmap.Height, start + pixelsToMask);
                for (var y = start; y < end; y++)
                    for (var x = 0; x < bitmap.Width; x++)
                        pixels[y * bitmap.Width + x] = SKColors.Transparent;
            }
            bitmap.Pixels = pixels;
        }

        public static MemoryStream EncodeRaidFramePng(SKBitmap source)
        {
            if (source.Width <= 0 || source.Height <= 0)
                throw new ArgumentOutOfRangeException(nameof(source));

            // PNG already compresses the crop without losing text detail. Shrinking every raid
            // to 300 pixels high made small names unreadable in taller layouts.
            using var image = SKImage.FromBitmap(source);
            using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
            if (encoded == null)
                throw new InvalidOperationException("Failed to encode the raid frame image.");
            var stream = new MemoryStream();
            encoded.SaveTo(stream);
            stream.Position = 0;
            return stream;
        }
    }
}
