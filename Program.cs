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

        return builder;
    }
}