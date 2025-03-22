using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
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
            return Dispatcher.UIThread.Invoke(() =>
            {
                SKBitmap bmp =  screenCapturer.CaptureScreenArea((int)topLeft.X, (int)topLeft.Y, width, height);
                if (bmp == null || bmp.Width == 0 || bmp.Height == 0)
                {
                    throw new Exception("Failed to capture screen area or invalid bitmap dimensions.");
                }
                RemoveOverlayNames(bmp, rowsCount);
                return CompressByReducingPixelsToStream(bmp);
                
            });
        }

        public static void RemoveOverlayNames(SKBitmap bmp, int rowsCount)
        {
            var scalingFactor = 1d;
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {

                scalingFactor = desktop.MainWindow.RenderScaling;

            }
            // Calculate the height of each row segment based on the total height and number of rows
            var ratio = Math.Ceiling(bmp.Height / (double)rowsCount);
            var breakPositions = Enumerable.Range(0, rowsCount).Select(r => (int)(r * ratio)).ToList();

            var pixelsToMask = (int)Math.Ceiling(16.5d * scalingFactor); // Number of rows to make transparent

            // Get the bitmap's pixels array
            SKColor[] pixels = bmp.Pixels;

            foreach (var y in breakPositions)
            {
                for (int i = 0; i < pixelsToMask; i++)
                {
                    int currentY = y + i;
                    if (currentY >= bmp.Height || currentY < 0)
                        continue;

                    // Loop through the width of the bitmap for each target row
                    for (int x = 0; x < bmp.Width; x++)
                    {
                        // Calculate the pixel's 1D index in the pixels array
                        int index = currentY * bmp.Width + x;
                    
                        // Set the pixel to transparent
                        pixels[index] = SKColors.Transparent;
                    }
                }
            }

            // Apply the modified pixel array back to the bitmap
            bmp.Pixels = pixels;
        }



        private static MemoryStream CompressByReducingPixelsToStream(SKBitmap source)
        {
            // Calculate the new width and height based on compression factor
            int newWidth = (int)(source.Width * CurrentCompressionFactor);
            int newHeight = (int)(source.Height * CurrentCompressionFactor);

            // Check if dimensions are valid
            if (newWidth <= 0 || newHeight <= 0)
            {
                throw new ArgumentException("Invalid dimensions after resizing. Check compression factor.");
            }

            // Resize the image
            SKImageInfo resizeInfo = new SKImageInfo(newWidth, newHeight);
            using (SKBitmap resizedBitmap = new SKBitmap(resizeInfo))
            {
                bool scaled = source.ScalePixels(resizedBitmap, SKFilterQuality.High);
                if (!scaled)
                {
                    throw new Exception("Failed to scale pixels in SKBitmap.");
                }
                // Encode the resized bitmap to a stream
                using (var image = SKImage.FromBitmap(resizedBitmap))
                {
                    if (image == null)
                    {
                        throw new Exception("Failed to create SKImage from resized bitmap.");
                    }
                    var encodedData = image.Encode(SKEncodedImageFormat.Png, 100);
                    if (encodedData == null)
                    {
                        throw new Exception("Failed to encode image.");
                    }

                    var ms = new MemoryStream();
                    encodedData.SaveTo(ms);
                    ms.Seek(0, SeekOrigin.Begin);
                    return ms;
                }
            }
        }

    }
}
