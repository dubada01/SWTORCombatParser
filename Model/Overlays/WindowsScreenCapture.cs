// WindowsScreenCapturer.cs
#if WINDOWS
using System.Drawing;
using System.Drawing.Imaging;
using SkiaSharp;

namespace SWTORCombatParser.Model.Overlays
{
    public class WindowsScreenCapturer : IScreenCapture
    {
        public SKBitmap CaptureScreenArea(int x, int y, int width, int height)
        {
            using (Bitmap bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(x, y, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
                }

                // Convert Bitmap to SKBitmap
                return BitmapToSKBitmap(bmp);
            }
        }

        private SKBitmap BitmapToSKBitmap(Bitmap bitmap)
        {
            SKBitmap skBitmap = new SKBitmap(bitmap.Width, bitmap.Height, SKColorType.Bgra8888, SKAlphaType.Premul);

            BitmapData data = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadOnly,
                bitmap.PixelFormat);

            skBitmap.InstallPixels(
                new SKImageInfo(bitmap.Width, bitmap.Height, SKColorType.Bgra8888, SKAlphaType.Premul),
                data.Scan0,
                data.Stride,
                (addr, ctx) => bitmap.UnlockBits(data));

            return skBitmap;
        }
    }
}
#endif