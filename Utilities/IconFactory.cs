using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
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
        private static ConcurrentDictionary<string, Bitmap> _classColoredBitmaps = new ConcurrentDictionary<string, Bitmap>();
        public static void Init()
        {
            Task.Run(() =>
            {
                _unknownIcon = new Bitmap(AssetLoader.Open(new Uri("avares://Orbs/resources/question-mark.png")));
                foreach(var swtorClass in ClassLoader.LoadAllClasses())
                {
                    var colorForClass = GetIconColorFromClass(swtorClass);
                    _classColoredBitmaps[swtorClass.Discipline] = GetColoredBitmapImage(swtorClass, colorForClass);
                }  
            });
        }
        
        public static Bitmap GetClassIcon(string className)
        {
            if(string.IsNullOrEmpty(className))
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
            var iconForClass = new Bitmap(AssetLoader.Open(new Uri("avares://Orbs/resources/Class Icons/" + className.ToLower() + ".png")));
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
                Logging.LogError("Failed to set icon color: "+ex.Message +"\r\n" + ex.StackTrace);
                return GetIcon(swtorClass.Name);
            }
        }




        private static WriteableBitmap SetIconColor(Bitmap image, Color color)
        {
            // Convert Avalonia Bitmap to SkiaSharp SKBitmap
            SKBitmap skBitmap;
            using (var imageStream = new MemoryStream())
            {
                image.Save(imageStream);
                imageStream.Seek(0, SeekOrigin.Begin);
                skBitmap = SKBitmap.Decode(imageStream);
            }

            // Apply color transformation
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

            // Create Avalonia WriteableBitmap
            var pixelSize = new PixelSize(skBitmap.Width, skBitmap.Height);
            var dpi = new Vector(96, 96);
            var writeable = new WriteableBitmap(pixelSize, dpi, Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Premul);

            // Copy SKBitmap pixel data into WriteableBitmap
            int totalBytes = skBitmap.Height * skBitmap.RowBytes;
            byte[] pixelBytes = new byte[totalBytes];
            System.Runtime.InteropServices.Marshal.Copy(skBitmap.GetPixels(), pixelBytes, 0, totalBytes);

            using (var fb = writeable.Lock())
            {
                System.Runtime.InteropServices.Marshal.Copy(pixelBytes, 0, fb.Address, totalBytes);
            }

            return writeable;
        }




    }
}
