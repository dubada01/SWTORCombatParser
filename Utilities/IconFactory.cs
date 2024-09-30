using System;
using System.IO;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System.Runtime.InteropServices;

namespace SWTORCombatParser.Utilities
{
    public static class IconFactory
    {
        private static Bitmap _unknownIcon;

        public static void Init()
        {
            _unknownIcon = new Bitmap(AssetLoader.Open(new Uri("avares://Orbs/resources/question-mark.png")));
        }

        public static Bitmap GetColoredBitmapImage(string className, Color color)
        {
            return SetIconColor(GetIcon(className), color);
        }

        public static Bitmap GetIcon(string className)
        {
            if (string.IsNullOrEmpty(className) || !File.Exists(Environment.CurrentDirectory + $"/resources/Class Icons/{className.ToLower()}.png"))
                return _unknownIcon;

            return new Bitmap(Environment.CurrentDirectory + $"/resources/Class Icons/{className.ToLower()}.png");
        }

        private static WriteableBitmap SetIconColor(Bitmap image, Color color)
        {
            var writeableBitmap = new WriteableBitmap(image.PixelSize, image.Dpi);
            using (var lockedBuffer = writeableBitmap.Lock())
            {
                var stride = lockedBuffer.RowBytes;
                var buffer = new byte[lockedBuffer.Size.Height * stride];

                Marshal.Copy(lockedBuffer.Address, buffer, 0, buffer.Length);

                for (int y = 0; y < lockedBuffer.Size.Height; y++)
                {
                    for (int x = 0; x < lockedBuffer.Size.Width; x++)
                    {
                        int index = (y * stride) + (x * 4);
                        if (buffer[index + 3] != 0) // Check alpha channel
                        {
                            buffer[index] = color.B;
                            buffer[index + 1] = color.G;
                            buffer[index + 2] = color.R;
                        }
                    }
                }

                Marshal.Copy(buffer, 0, lockedBuffer.Address, buffer.Length);
            }

            return writeableBitmap;
        }

        private static Bitmap BitmapToImageSource(Bitmap bitmap)
        {
            // No need for conversion in Avalonia, return the bitmap directly.
            return bitmap;
        }
    }
}
