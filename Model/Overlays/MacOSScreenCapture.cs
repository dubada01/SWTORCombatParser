using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace SWTORCombatParser.Model.Overlays;

public class MacOSScreenCapturer : IScreenCapture
{
    public SKBitmap CaptureScreenArea(int x, int y, int width, int height)
    {
        if (width <= 0 || height <= 0 || (long)width * height > 16_777_216)
            throw new ArgumentOutOfRangeException(nameof(width), "Select a smaller, nonempty screen area.");
        // Called only by the user's Detect names action. Requesting access also
        // registers Orbs in macOS privacy settings on its first capture attempt.
        if (!CGPreflightScreenCaptureAccess() && !CGRequestScreenCaptureAccess())
            throw new InvalidOperationException("Allow Orbs in System Settings → Privacy & Security → Screen Recording, then restart Orbs and try Detect names again.");

        var filePath = Path.Combine(Path.GetTempPath(), $"orbs-capture-{Guid.NewGuid():N}.png");
        try
        {
            var start = new ProcessStartInfo("/usr/sbin/screencapture") { UseShellExecute = false, CreateNoWindow = true };
            start.ArgumentList.Add("-x");
            start.ArgumentList.Add("-t");
            start.ArgumentList.Add("png");
            start.ArgumentList.Add("-R" + string.Create(CultureInfo.InvariantCulture, $"{x},{y},{width},{height}"));
            start.ArgumentList.Add(filePath);
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start macOS screen capture.");
            if (!process.WaitForExit(5000))
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
                throw new TimeoutException("Screen capture timed out. Check Screen Recording permission and try again.");
            }
            if (process.ExitCode != 0 || !File.Exists(filePath))
                throw new InvalidOperationException("macOS could not capture the raid frames. Check Screen Recording permission and keep SWTOR visible.");
            return SKBitmap.Decode(filePath) ?? throw new InvalidOperationException("macOS returned an unreadable screenshot.");
        }
        finally { if (File.Exists(filePath)) File.Delete(filePath); }
    }

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    [return: MarshalAs(UnmanagedType.I1)] private static extern bool CGPreflightScreenCaptureAccess();
    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    [return: MarshalAs(UnmanagedType.I1)] private static extern bool CGRequestScreenCaptureAccess();
}
