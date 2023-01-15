using ScottPlot;
using ScottPlot.Drawing.Colormaps;
using SWTORCombatParser.ViewModels.Overlays.RaidHots;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Interop;

namespace SWTORCombatParser.Model.Overlays
{
    public static class RaidFrameScreenGrab
    {
        public static double CurrentCompressionFactor;
        public static Bitmap GetRaidFrameBitmap(System.Drawing.Point topLeft, int width, int height)
        {
            Rectangle rect = new Rectangle(topLeft.X, topLeft.Y, width, height);
            Bitmap bmp = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb);
            Graphics g = Graphics.FromImage(bmp);
            g.CopyFromScreen(rect.Left, rect.Top, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
            return bmp;
        }
        public static MemoryStream GetRaidFrameBitmapStream(System.Drawing.Point topLeft, int width, int height,int rowsCount)
        {
            CurrentCompressionFactor = Math.Min((300d / height),1f);
            Rectangle rect = new Rectangle(topLeft.X, topLeft.Y, width, height);
            using (Bitmap bmp = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(rect.Left, rect.Top, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
                    RemoveOverlayNames(bmp,rowsCount);
                    return CompressByReducingPixelsToStream(bmp);
                }
            }
        }
        private static void RemoveOverlayNames(Bitmap bmp, int rowsCount)
        {
            App.Current.Dispatcher.Invoke(new Action(() =>
            {
                System.Windows.Media.Matrix transformToDevice;
                using (var source = new HwndSource(new HwndSourceParameters()))
                    transformToDevice = source.CompositionTarget.TransformToDevice;
                var pixels = transformToDevice.Transform(new Vector(16.5, 16.5));

                var ratio = Math.Ceiling(bmp.Height / (double)rowsCount);
                var breakPositions = Enumerable.Range(0, rowsCount).Select(r => r * ratio);
                for (var y = 0; y < bmp.Height; y++)
                {
                    if (!breakPositions.Any(b => y < b + Math.Ceiling(pixels.Y) && y > b))
                        continue;
                    for (var x = 0; x < bmp.Width; x++)
                    {
                        bmp.SetPixel(x, y, Color.Transparent);
                    }
                }
            }));

        }

        private static MemoryStream CompressByReducingPixelsToStream(Bitmap source)
        {
            // Calculate the new width and height
            int newWidth = (int)(source.Width * CurrentCompressionFactor);
            int newHeight = (int)(source.Height * CurrentCompressionFactor);

            // Create the thumbnail image
            Image thumbnail = source.GetThumbnailImage(newWidth, newHeight, null, IntPtr.Zero);
            var ms = new MemoryStream();
            // Save the thumbnail image
            thumbnail.Save(ms, ImageFormat.Bmp);
            return ms;
        }

        private static MemoryStream CompressBitmapToJPGStream(Bitmap source)
        {
            using (var encoder = new EncoderParameter(Encoder.Quality, 75L))
            {
                using (var param = new EncoderParameters(1))
                {
                    param.Param[0] = encoder;
                    var jpg = ImageCodecInfo.GetImageEncoders().FirstOrDefault(e => e.MimeType == "image/jpeg");
                    var ms = new MemoryStream();
                    source.Save(ms, jpg, param);
                    return ms;

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

        public static Bitmap UpdateCellNamePixels(RaidFrameOverlayViewModel raidViewModel, Bitmap raidFrame)
        {
            var rows = raidViewModel.Rows;
            var columns = raidViewModel.Columns;
            var rowsBreak = Math.Ceiling(raidFrame.Height / (double)rows);
            var rowsPositions = Enumerable.Range(0, rows).Select(r => r * rowsBreak).ToList();
            var columnsBreak = Math.Ceiling(raidFrame.Width / (double)columns);
            var columnsPositions = Enumerable.Range(0, columns).Select(r => r * columnsBreak).ToList();
            Dictionary<(int, int), List<int>> namePixelIndicies = new Dictionary<(int, int), List<int>>();
            Dictionary<(int, int), int> fullRedHPPixelCount = new Dictionary<(int, int), int>();

            Rectangle rect = new Rectangle(0, 0, raidFrame.Width, raidFrame.Height);
            BitmapData bmpData = raidFrame.LockBits(rect, ImageLockMode.ReadOnly, raidFrame.PixelFormat);

            IntPtr ptr = bmpData.Scan0;

            int bytes = bmpData.Stride * raidFrame.Height;
            byte[] rgbValues = new byte[bytes];
            Marshal.Copy(ptr, rgbValues, 0, bytes);
            int count = -1;
            for (int y = 0; y < bmpData.Height; y++)
            {
                var currentRowBreak = rowsPositions.First(b => y < b + rowsBreak && y >= b);
                var celly = rowsPositions.IndexOf(currentRowBreak);
                for (int x = 0; x < bmpData.Width; x++)
                {
                    count++;
                    var currentColumnBreak = columnsPositions.First(b => x < b + columnsBreak && x >= b);
                    var cellx = columnsPositions.IndexOf(currentColumnBreak);

                    if (!namePixelIndicies.ContainsKey((cellx, celly)))
                    {
                        fullRedHPPixelCount[(cellx, celly)] = 0;
                        namePixelIndicies[(cellx, celly)] = new List<int>();
                    }

                    if (x > (currentColumnBreak + (columnsBreak / 2)))
                        continue;
                    if (y < currentRowBreak + (rowsBreak / 5) || y > currentRowBreak + (rowsBreak / 2))
                        continue;
                    var b = (byte)(rgbValues[(y * bmpData.Stride) + (x * 4)]);
                    var g = (byte)(rgbValues[(y * bmpData.Stride) + (x * 4) + 1]);
                    var r = (byte)(rgbValues[(y * bmpData.Stride) + (x * 4) + 2]);
                    ColorToHSV(Color.FromArgb(r, g, b), out var h, out var s, out var v);
                    if ((h < 215 && h > 188 && s > 0.3 && s < 0.6 && v > .9))
                    {
                        namePixelIndicies[(cellx, celly)].Add(count);
                    }
                    if (((h > 350 || h < 5) && s > 0.95 && v > 0.66))
                    {
                        fullRedHPPixelCount[(cellx, celly)]++;     
                    }
                }
            }

            raidFrame.UnlockBits(bmpData);
            foreach (var cell in raidViewModel.RaidHotCells)
            {
                if(fullRedHPPixelCount[(cell.Column, cell.Row)] > 100)
                    cell.NamePixelIndicies = namePixelIndicies[(cell.Column, cell.Row)];
            }

            return CreateTestImage(raidViewModel.RaidHotCells.SelectMany(kvp => kvp.StaticPixelChanges).ToList(),
                new Bitmap(raidFrame));
        }

        private static Bitmap CreateTestImage(List<int> nameIndicies,Bitmap testImage)
        {
            Rectangle rect = new Rectangle(0, 0, testImage.Width, testImage.Height);
            BitmapData bmpData = testImage.LockBits(rect, ImageLockMode.ReadWrite, testImage.PixelFormat);

            IntPtr ptr = bmpData.Scan0;

            int bytes = bmpData.Stride * testImage.Height;
            byte[] rgbValues = new byte[bytes];
            Marshal.Copy(ptr, rgbValues, 0, bytes);
            int count = 0;
            for (int y = 0; y < bmpData.Height; y++)
            {
                for (int x = 0; x < bmpData.Width; x++)
                {
                    if (nameIndicies.Contains(count))
                    {
                        rgbValues[(y * bmpData.Stride) + (x * 4)] = 255;
                        rgbValues[(y * bmpData.Stride) + (x * 4) + 1] = 0;
                        rgbValues[(y * bmpData.Stride) + (x * 4) + 2] = 255;
                    }
                    count++;
                }
            }
            Marshal.Copy(rgbValues, 0, bmpData.Scan0,bytes);
            testImage.UnlockBits(bmpData);
            return testImage;
        }
        public static double GetRatioOfRedPixels(Bitmap raidFrame)
        {
            return GetNumberOfPixelsBetweenHue(raidFrame,5,345)/(double)(raidFrame.Width * raidFrame.Height);
        }
        private static int GetNumberOfPixelsBetweenHue(Bitmap image, int maxHue, int minHue)
        {
            Rectangle rect = new Rectangle(0, 0, image.Width, image.Height);
            BitmapData bmpData = image.LockBits(rect, ImageLockMode.ReadWrite, image.PixelFormat);

            IntPtr ptr = bmpData.Scan0;

            int bytes = bmpData.Stride * image.Height;
            byte[] rgbValues = new byte[bytes];
            Marshal.Copy(ptr, rgbValues, 0, bytes);
            var numberOfRed = 0;
            for (int y = 0; y < bmpData.Height; y++)
            {
                for (int x = 0; x < bmpData.Width; x++)
                {
                    var b = (byte)(rgbValues[(y * bmpData.Stride) + (x * 4)]);
                    var g = (byte)(rgbValues[(y * bmpData.Stride) + (x * 4) + 1]);
                    var r = (byte)(rgbValues[(y * bmpData.Stride) + (x * 4) + 2]);
                    ColorToHSV(Color.FromArgb(r, g, b), out var h, out var s, out var v);
                    if (((h > 345 || h < 5)))
                    {
                        rgbValues[(y * bmpData.Stride) + (x * 4)] = 255;
                        rgbValues[(y * bmpData.Stride) + (x * 4) + 1] = 0;
                        rgbValues[(y * bmpData.Stride) + (x * 4) + 2] = 255;
                        numberOfRed++;
                    }
                }
            }
            Marshal.Copy(rgbValues, 0, bmpData.Scan0, bytes);
            image.UnlockBits(bmpData);

            return numberOfRed;
        }
    }
}
