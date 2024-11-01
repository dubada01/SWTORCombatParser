#if WINDOWS
using System.Drawing;
using System.Drawing.Imaging;
using SkiaSharp;
using SkiaSharp.Views.Desktop;

namespace SWTORCombatParser.Model.Overlays
{
    public class WindowsScreenCapturer : IScreenCapture
    {
        public SKBitmap CaptureScreenArea(int x, int y, int width, int height)
        {
            // Create a new bitmap where the capture will be stored
            Bitmap bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        
            // Create graphics object from the bitmap and copy the screen area into it
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(x, y, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
            }

            return bmp.ToSKBitmap();
        }
    }
}
#endif