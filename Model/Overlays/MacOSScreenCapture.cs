#if MACOS
using System;
using System.Diagnostics;
using System.IO;
using SkiaSharp;
using System.Runtime.InteropServices;

namespace SWTORCombatParser.Model.Overlays
{
    public class MacOSScreenCapturer : IScreenCapture
    {
        public SKBitmap CaptureScreenArea(int x, int y, int width, int height)
        {
            string filePath = "/tmp/screencapture.png";
            string region = $"{x},{y},{width},{height}";

            // Execute the screencapture command
            Process.Start("screencapture", $"-R{region} {filePath}")?.WaitForExit();

            // Load the image into an SKBitmap from SkiaSharp
            using (var fileStream = File.OpenRead(filePath))
            {
                var skData = SKData.Create(fileStream);
                return SKBitmap.Decode(skData);
            }
        }

        public MemoryStream CaptureAsStream(int x, int y, int width, int height)
        {
            var bitmap = CaptureScreenArea(x, y, width, height);
        
            // Save to a MemoryStream for further use (e.g., uploading to cloud)
            var memoryStream = new MemoryStream();
            bitmap.Encode(memoryStream, SKEncodedImageFormat.Png, 100);
            memoryStream.Seek(0, SeekOrigin.Begin);

            return memoryStream;
        }
    }

}
#endif
