using System;
using Avalonia.Platform;
using SkiaSharp;

namespace SWTORCombatParser.Utilities;

public static class SKBitmapFromFile
{
    public static SKBitmap Load(string assetPath)
    {
        // Open the resource stream using the Avalonia asset loader
        using (var assetStream = AssetLoader.Open(new Uri(assetPath)))
        {
            // Decode the stream into an SKBitmap
            return SKBitmap.Decode(assetStream);
        }
    }
}