using System;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace SWTORCombatParser.Utilities;

public static class ImageHelper
{
    public static Bitmap LoadFromResource(string resourceUri)
    {
        return new Bitmap(AssetLoader.Open(new Uri(resourceUri)));
    }
    
}