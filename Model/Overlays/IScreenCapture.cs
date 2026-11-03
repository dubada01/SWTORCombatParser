using SkiaSharp;

namespace SWTORCombatParser.Model.Overlays;

public interface IScreenCapture
{
    SKBitmap CaptureScreenArea(int x, int y, int width, int height);   
}