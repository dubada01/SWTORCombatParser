using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using SWTORCombatParser.Utilities;

static class CrossPlatformClipboard
{
    public static void SetText(string text)
    {
        // Avalonia owns the native clipboard and its event-loop lifetime, including
        // X11 selection ownership and macOS pasteboard marshalling.
        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                var clipboard = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow?.Clipboard;
                if (clipboard == null) throw new InvalidOperationException("No desktop clipboard is available.");
                await clipboard.SetTextAsync(text);
            }
            catch (Exception error) { Logging.LogError("Could not copy text: " + error.Message, false); }
        });
    }
}
