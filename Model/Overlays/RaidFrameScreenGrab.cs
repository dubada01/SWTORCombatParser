using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

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

            Bitmap grayScale = ToGrayscale(bmp);
            grayScale.Save(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "test.png"));
            return bmp;
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
