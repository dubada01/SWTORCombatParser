using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;

namespace SWTORCombatParser.Utilities
{
    public static class IconFactory
    {
        private static Bitmap _unknownIcon;

        public static void Init()
        {
            _unknownIcon = new Bitmap(Environment.CurrentDirectory + "/resources/question-mark.png");
        }
        public static BitmapImage GetColoredBitmapImage(string className, System.Windows.Media.Color color)
        {
            return SetIconColor(GetIcon(className), color);
        }
        public static Bitmap GetIcon(string className)
        {
            if (string.IsNullOrEmpty(className) || !File.Exists(Environment.CurrentDirectory + $"/resources/Class Icons/{className.ToLower()}.png"))
                return _unknownIcon;
            return new Bitmap(Environment.CurrentDirectory + $"/resources/Class Icons/{className.ToLower()}.png");
        }
        private static BitmapImage SetIconColor(Bitmap image, System.Windows.Media.Color color)
        {
            Rectangle rect = new Rectangle(0, 0, image.Width, image.Height);
            BitmapData bmpData = image.LockBits(rect, ImageLockMode.ReadWrite, image.PixelFormat);

            IntPtr ptr = bmpData.Scan0;

            int bytes = bmpData.Stride * image.Height;
            byte[] rgbValues = new byte[bytes];
            Marshal.Copy(ptr, rgbValues, 0, bytes);
            for (int y = 0; y < bmpData.Height; y++)
            {
                for (int x = 0; x < bmpData.Width; x++)
                {
                    if (rgbValues[(y * bmpData.Stride) + (x * 4)] != 0)
                    {
                        rgbValues[(y * bmpData.Stride) + (x * 4)] = color.B;
                        rgbValues[(y * bmpData.Stride) + (x * 4) + 1] = color.G;
                        rgbValues[(y * bmpData.Stride) + (x * 4) + 2] = color.R;
                    }
                }
            }
            Marshal.Copy(rgbValues, 0, bmpData.Scan0, bytes);
            image.UnlockBits(bmpData);

            return BitmapToImageSource(image);
        }
        private static BitmapImage BitmapToImageSource(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, ImageFormat.Png);
                memory.Position = 0;
                BitmapImage bitmapimage = new BitmapImage();
                bitmapimage.BeginInit();
                bitmapimage.StreamSource = memory;
                bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapimage.EndInit();

                return bitmapimage;
            }
        }
    }
}
