using System;
using Avalonia;
using Avalonia.ReactiveUI;
#if WINDOWS
using Avalonia.Win32;
#endif

namespace SWTORCombatParser;

internal sealed class Program
{
    // Entry point for standalone or designer mode
    [STAThread]
    public static void Main(string[] args)
    {
        // MSIX activates this executable directly (without a PSF launcher). Set the
        // working directory before loading relative resources or native libraries.
        string executablePath = AppContext.BaseDirectory;
        Environment.CurrentDirectory = executablePath;
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        
    }
    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .UseReactiveUI()
            .LogToTrace();

#if WINDOWS
        builder = builder.With(new Win32PlatformOptions
        {
            DpiAwareness = Win32DpiAwareness.PerMonitorDpiAware
        });
#endif

#if LINUX
        if (Environment.GetEnvironmentVariable("ORBS_SOFTWARE_RENDERING") == "1")
            builder = builder.With(new X11PlatformOptions { RenderingMode = new[] { X11RenderingMode.Software } });
#endif
        return builder;
    }
}
