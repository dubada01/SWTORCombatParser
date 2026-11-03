using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;
using SWTORCombatParser.DataStructures.ClassInfos;


namespace SWTORCombatParser.Utilities
{
    public static class IconFactory
    {
        public static Bitmap _unknownIcon;

        private static ConcurrentDictionary<string, Bitmap> _classColoredBitmaps =
            new ConcurrentDictionary<string, Bitmap>();

        public static void Init()
        {
            Task.Run(() =>
            {
                _unknownIcon = new Bitmap(AssetLoader.Open(new Uri("avares://Orbs/resources/question-mark.png")));
                foreach (var swtorClass in ClassLoader.LoadAllClasses())
                {
                    var colorForClass = GetIconColorFromClass(swtorClass);
                    _classColoredBitmaps[swtorClass.Discipline] = GetColoredBitmapImage(swtorClass, colorForClass);
                }
            });
        }

        public static Bitmap GetClassIcon(string className)
        {
            if (string.IsNullOrEmpty(className))
                return _unknownIcon;
            if (_classColoredBitmaps.ContainsKey(className))
                return _classColoredBitmaps[className];
            return _unknownIcon;
        }

        private static Color GetIconColorFromClass(SWTORClass classInfo)
        {
            return classInfo.Role switch
            {
                Role.Healer => Colors.ForestGreen,
                Role.Tank => Colors.CornflowerBlue,
                Role.DPS => Colors.IndianRed,
                _ => (Color)ResourceFinder.GetColorFromResourceName("Gray4")
            };
        }

        private static Bitmap GetIcon(string className)
        {
            if (string.IsNullOrEmpty(className))
                return _unknownIcon;
            var iconForClass =
                new Bitmap(AssetLoader.Open(new Uri("avares://Orbs/resources/Class Icons/" + className.ToLower() +
                                                    ".png")));
            return iconForClass;
        }

        private static Bitmap GetColoredBitmapImage(SWTORClass swtorClass, Color color)
        {
            try
            {
                return SetIconColor(GetIcon(swtorClass.Name), color);
            }
            catch (Exception ex)
            {
                Logging.LogError("Failed to set icon color: " + ex.Message + "\r\n" + ex.StackTrace);
                return GetIcon(swtorClass.Name);
            }
        }



        private static WriteableBitmap SetIconColor(Bitmap image, Avalonia.Media.Color color)
        {
            // 1. Avalonia Bitmap -> Skia SKBitmap
            using var imageStream = new MemoryStream();
            image.Save(imageStream);
            imageStream.Position = 0;

            using var decoded = SKBitmap.Decode(imageStream);
            if (decoded == null)
                throw new InvalidOperationException("Failed to decode image into SKBitmap.");

            // 2. Force a known pixel format: BGRA8888 (matches Avalonia's Bgra8888)
            using var skBitmap = decoded.Copy(SKColorType.Bgra8888)
                                 ?? throw new InvalidOperationException("Failed to copy bitmap to BGRA8888.");

            // 3. Apply color tint while preserving alpha
            for (int y = 0; y < skBitmap.Height; y++)
            {
                for (int x = 0; x < skBitmap.Width; x++)
                {
                    var skColor = skBitmap.GetPixel(x, y);
                    if (skColor.Alpha != 0)
                    {
                        var newColor = new SKColor(color.R, color.G, color.B, skColor.Alpha);
                        skBitmap.SetPixel(x, y, newColor);
                    }
                }
            }

            // 4. Create Avalonia WriteableBitmap with matching format
            var pixelSize = new PixelSize(skBitmap.Width, skBitmap.Height);
            var dpi = new Vector(96, 96);
            var writeable = new WriteableBitmap(
                pixelSize,
                dpi,
                Avalonia.Platform.PixelFormat.Bgra8888,
                Avalonia.Platform.AlphaFormat.Premul);

            // 5. Copy pixels row-by-row (handles stride differences)
            using (var fb = writeable.Lock())
            {
                int srcStride = skBitmap.RowBytes; // bytes per row in Skia
                int dstStride = fb.RowBytes; // bytes per row in Avalonia
                int rowBytesToCopy = Math.Min(srcStride, dstStride);

                var rowBuffer = new byte[rowBytesToCopy];

                IntPtr srcBase = skBitmap.GetPixels();
                IntPtr dstBase = fb.Address;

                for (int y = 0; y < skBitmap.Height; y++)
                {
                    IntPtr srcRowPtr = srcBase + y * srcStride;
                    IntPtr dstRowPtr = dstBase + y * dstStride;

                    Marshal.Copy(srcRowPtr, rowBuffer, 0, rowBytesToCopy);
                    Marshal.Copy(rowBuffer, 0, dstRowPtr, rowBytesToCopy);
                }
            }

            return writeable;
        }
    }

}
