// MacOSScreenCapturer.cs
#if MACOS
using System;
using SkiaSharp;
using CoreGraphics;
using Foundation;

namespace SWTORCombatParser.Model.Overlays
{
    public class MacOSScreenCapturer : IScreenCapturer
    {
        public SKBitmap CaptureScreenArea(int x, int y, int width, int height)
        {
            var screenRect = new CGRect(x, y, width, height);
            using (var screenImage = CGWindowList.CreateImage(screenRect, CGWindowListOption.OnScreenOnly, CGWindowID.Zero, CGWindowImageOption.Default))
            {
                var dataProvider = screenImage.DataProvider;
                var cfData = dataProvider.CopyData();
                byte[] buffer = new byte[cfData.Length];
                System.Runtime.InteropServices.Marshal.Copy(cfData.Bytes, buffer, 0, (int)cfData.Length);
                cfData.Dispose();

                using (var skData = SKData.CreateCopy(buffer))
                {
                    return SKBitmap.Decode(skData);
                }
            }
        }
    }
}
#endif