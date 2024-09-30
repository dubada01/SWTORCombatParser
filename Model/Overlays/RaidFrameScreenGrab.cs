using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Threading;
using SkiaSharp;

namespace SWTORCombatParser.Model.Overlays
{
    public static class RaidFrameScreenGrab
    {
        public static double CurrentCompressionFactor;

        private static IScreenCapture screenCapturer;

        static RaidFrameScreenGrab()
        {
#if WINDOWS
            screenCapturer = new WindowsScreenCapturer();
#elif MACOS
            screenCapturer = new MacOSScreenCapturer();
#else
            throw new PlatformNotSupportedException("Screen capture is not supported on this platform.");
#endif
        }

        public static MemoryStream GetRaidFrameBitmapStream(Point topLeft, int width, int height, int rowsCount)
        {
            CurrentCompressionFactor = Math.Min((300d / height), 1f);

            SKBitmap bmp = screenCapturer.CaptureScreenArea((int)topLeft.X, (int)topLeft.Y, width, height);
            RemoveOverlayNames(bmp, rowsCount);
            return CompressByReducingPixelsToStream(bmp);
        }

        private static void RemoveOverlayNames(SKBitmap bmp, int rowsCount)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                var ratio = Math.Ceiling(bmp.Height / (double)rowsCount);
                var breakPositions = Enumerable.Range(0, rowsCount).Select(r => r * ratio).ToList();
                var pixelsToMask = (int)Math.Ceiling(16.5); // Adjust as needed

                foreach (var y in breakPositions)
                {
                    for (int i = 0; i < pixelsToMask; i++)
                    {
                        int currentY = (int)y + i;
                        if (currentY >= bmp.Height)
                            continue;

                        for (int x = 0; x < bmp.Width; x++)
                        {
                            bmp.SetPixel(x, currentY, SKColors.Transparent);
                        }
                    }
                }
            });
        }

        private static MemoryStream CompressByReducingPixelsToStream(SKBitmap source)
        {
            // Calculate the new width and height
            int newWidth = (int)(source.Width * CurrentCompressionFactor);
            int newHeight = (int)(source.Height * CurrentCompressionFactor);

            // Resize the image
            SKImageInfo resizeInfo = new SKImageInfo(newWidth, newHeight);
            SKBitmap resizedBitmap = new SKBitmap(resizeInfo);
            source.ScalePixels(resizedBitmap, SKFilterQuality.High);

            // Encode the image to a stream
            var ms = new MemoryStream();
            using (var image = SKImage.FromBitmap(resizedBitmap))
            {
                image.Encode(SKEncodedImageFormat.Bmp, 100).SaveTo(ms);
            }
            ms.Seek(0, SeekOrigin.Begin);
            return ms;
        }
    }
}
