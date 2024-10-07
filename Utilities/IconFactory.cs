using System;
using System.IO;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;


namespace SWTORCombatParser.Utilities
{
    public static class IconFactory
    {
        public static Bitmap _unknownIcon;

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
            if (string.IsNullOrEmpty(className))
                return _unknownIcon;
            var iconForClass = new Bitmap(AssetLoader.Open(new Uri("avares://Orbs/resources/Class Icons/" + className.ToLower() + ".png")));
            return iconForClass;
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

            // Apply the color change using SkiaSharp
            for (int y = 0; y < skBitmap.Height; y++)
            {
                for (int x = 0; x < skBitmap.Width; x++)
                {
                    SKColor skColor = skBitmap.GetPixel(x, y);
                    if (skColor.Alpha != 0) // Check alpha channel
                    {
                        var newColor = new SKColor(color.R, color.G, color.B, skColor.Alpha);
                        skBitmap.SetPixel(x, y, newColor);
                    }
                }
            }

            // Convert SkiaSharp SKBitmap back to Avalonia WriteableBitmap
            using (var skiaStream = new MemoryStream())
            {
                skBitmap.Encode(skiaStream, SKEncodedImageFormat.Png, 100);
                skiaStream.Seek(0, SeekOrigin.Begin);
        
                return WriteableBitmap.Decode(skiaStream);
            }
        }


    }
}
