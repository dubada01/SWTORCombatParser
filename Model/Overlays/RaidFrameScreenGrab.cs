using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace SWTORCombatParser.Model.Overlays
{
    public static class RaidFrameScreenGrab
    {
        public static Bitmap GetRaidFrameBitmap(Point topLeft, int width, int height)
        {
            Rectangle rect = new Rectangle(topLeft.X, topLeft.Y, width, height);
            Bitmap bmp = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb);
            Graphics g = Graphics.FromImage(bmp);
            g.CopyFromScreen(rect.Left, rect.Top, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
            //ExtractJustNames(bmp);
            return bmp;
        }

        private static void ExtractJustNames(Bitmap raw)
        {
            for (var y = 0; y < raw.Height; y++)
            {
                for (var x = 0; x < raw.Width; x++)
                {
                    var c = raw.GetPixel(x, y);
                    ColorToHSV(c,out var h, out _, out _);
                    if(h < 75)
                        raw.SetPixel(x,y,Color.Black);
                }
            }
        }
        private static void ColorToHSV(Color color, out double hue, out double saturation, out double value)
        {
            int max = Math.Max(color.R, Math.Max(color.G, color.B));
            int min = Math.Min(color.R, Math.Min(color.G, color.B));

            hue = color.GetHue();
            saturation = (max == 0) ? 0 : 1d - (1d * min / max);
            value = max / 255d;
        }

        public static double GetRatioOfRedPixels(Bitmap raidFrame)
        {
            return GetRedPixels(raidFrame)/(double)(raidFrame.Width * raidFrame.Height);
        }
        private static int GetRedPixels(Bitmap image)
        {
            var numberOfRed = 0;
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    var c = image.GetPixel(x, y);
                    if(c.R > 90 && c.G < 10 && c.B < 10)
                        numberOfRed++;
                }
            }
            return numberOfRed;
        }
        public static Bitmap ToGrayscale(Bitmap bmp)
        {
            var result = new Bitmap(bmp.Width, bmp.Height, PixelFormat.Format8bppIndexed);

            var resultPalette = result.Palette;

            for (int i = 0; i < 256; i++)
            {
                resultPalette.Entries[i] = Color.FromArgb(255, i, i, i);
            }

            result.Palette = resultPalette;

            BitmapData data = result.LockBits(new Rectangle(0, 0, result.Width, result.Height), ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);

            // Copy the bytes from the image into a byte array
            byte[] bytes = new byte[data.Height * data.Stride];
            Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);

            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    var c = bmp.GetPixel(x, y);
                    var rgb = (byte)((c.R + c.G + c.B) / 3);

                    bytes[y * data.Stride + x] = rgb;
                }
            }

            // Copy the bytes from the byte array into the image
            Marshal.Copy(bytes, 0, data.Scan0, bytes.Length);

            result.UnlockBits(data);

            return result;
        }
        public static int GetDifferenceOfAverage(Bitmap image1, Bitmap image2)
        {
            var average1 = GetAveragePixelIntensity(image1);
            var average2 = GetAveragePixelIntensity(image2);
            return Math.Abs(average2 - average1);
        }
        public static int GetAveragePixelIntensity(Bitmap input)
        {
            Bitmap bmp = new Bitmap(1, 1);
            Bitmap orig = input;
            using (Graphics g = Graphics.FromImage(bmp))
            {
                // updated: the Interpolation mode needs to be set to 
                // HighQualityBilinear or HighQualityBicubic or this method
                // doesn't work at all.  With either setting, the results are
                // slightly different from the averaging method.
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(orig, new Rectangle(0, 0, 1, 1));
            }
            Color pixel = bmp.GetPixel(0, 0);
            // pixel will contain average values for entire orig Bitmap
            byte avgR = pixel.R;
            return avgR;
        }
    }
}
