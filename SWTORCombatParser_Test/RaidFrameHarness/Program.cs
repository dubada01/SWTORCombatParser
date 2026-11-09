using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media.Imaging;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.Overlays.RaidHots;
using SWTORCombatParser.Views.Overlay.RaidHOTs;

internal static class Program
{
    internal static string Output = "";
    internal static readonly List<string> Checks = new();
    internal static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("Usage: RaidFrameHarness <new-output-directory>");
            return 2;
        }
        Output = Path.GetFullPath(args[0]);
        if (Directory.Exists(Output) || File.Exists(Output))
        {
            Console.Error.WriteLine("Use a new output directory so previous reports cannot look like a successful run.");
            return 2;
        }
        Directory.CreateDirectory(Output);
        Environment.CurrentDirectory = AppContext.BaseDirectory;
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            var path = Path.Combine(AppContext.BaseDirectory, name.Name + ".dll");
            return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
        };
        try
        {
            IsolatePersistence();
            AppBuilder.Configure<RaidFrameHarnessApp>().UsePlatformDetect().UseReactiveUI()
                .StartWithClassicDesktopLifetime(args);
            return 0;
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(Output, "startup-error.txt"), error.ToString());
            return 1;
        }
    }

    private static void IsolatePersistence()
    {
        var data = Path.Combine(Output, "settings");
        Directory.CreateDirectory(data);
        var realRoot = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DubaTech", "SWTORCombatParser"));
        var redirects = new List<string>();
        foreach (var type in new[] { typeof(Settings), typeof(Logging), typeof(DefaultGlobalOverlays), typeof(DefaultCharacterOverlays), typeof(RaidFrameOverlayManager) })
        foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (field.FieldType != typeof(string) || field.GetValue(null) is not string original || !Path.IsPathRooted(original)) continue;
            var full = Path.GetFullPath(original);
            if (full != realRoot && !full.StartsWith(realRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) continue;
            if (field.IsInitOnly) throw new InvalidOperationException("Could not isolate " + field.Name);
            var relative = Path.GetRelativePath(realRoot, full);
            field.SetValue(null, relative == "." ? data : Path.Combine(data, relative));
            redirects.Add(type.Name + "." + field.Name);
        }
        File.WriteAllText(Path.Combine(data, "general_settings.json"), "{\"offline_mode\":true,\"Auto_Parse\":false,\"overlay_bar_scale\":1.0,\"custom_audio_paths\":[],\"timers_enabled\":false}");
        File.WriteAllText(Path.Combine(Output, "isolation.json"), JsonSerializer.Serialize(redirects));
        DefaultGlobalOverlays.Init();
        DefaultCharacterOverlays.Init();
        RaidFrameOverlayManager.Init();
        CombatLogStateBuilder.ClearState();
    }

    internal static void Check(bool value, string description)
    {
        if (!value) throw new InvalidOperationException(description);
        Checks.Add(description);
    }
    internal static object? Field(object instance, string name) => instance.GetType().GetField(name, PrivateInstance)!.GetValue(instance);
    internal static object? Call(object instance, string name, params object[] args) => instance.GetType().GetMethod(name, PrivateInstance)!.Invoke(instance, args);
    internal static void Snapshot(Control control, string name)
    {
        var size = new PixelSize((int)Math.Ceiling(control.Bounds.Width), (int)Math.Ceiling(control.Bounds.Height));
        using var image = new RenderTargetBitmap(size, new Vector(96, 96));
        image.Render(control);
        image.Save(Path.Combine(Output, name + ".png"));
    }
}

public sealed class RaidFrameHarnessApp : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        var converters = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "converter-resources.json")))!;
        foreach (var converter in converters)
            Resources[converter["Key"]] = Activator.CreateInstance(typeof(Settings).Assembly.GetType("SWTORCombatParser.Utilities.Converters." + converter["Type"])!, true)!;
        var configurationStyle = new Uri("avares://Orbs/Utilities/UIStyles/Configuration.axaml");
        Styles.Add(new StyleInclude(configurationStyle) { Source = configurationStyle });
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var config = new RaidHotsConfigViewModel();
            var setup = new RaidHOTsSteup { DataContext = config };
            var main = new Window { Width = 590, Height = 570, Content = setup, Title = "Raid frame isolated checks", ShowActivated = false };
            desktop.MainWindow = main;
            main.Opened += async (_, _) => await Run(config, main);
        }
        base.OnFrameworkInitializationCompleted();
    }

    private static async Task Run(RaidHotsConfigViewModel config, Window main)
    {
        var vm = config.Overlay;
        var view = (RaidFrameOverlay)vm.MainContent;
        Exception? failure = null;
        try
        {
            config.RaidHotsEnabled = true;
            vm.Editable = true;
            await Settle();
            Program.Check(vm._overlayWindow.Width >= 200 && vm._overlayWindow.Height >= 200,
                "Fresh installation shows a usable default overlay size");
            Program.Snapshot(vm._overlayWindow, "overlay-default-size");
            vm._overlayWindow.Width = 620;
            vm._overlayWindow.Height = 500;
            vm._overlayWindow.Position = new PixelPoint(80, 100);
            await Settle();
            Program.Check(vm.Rows == 4 && vm.Columns == 2 && vm.RaidHotCells.Count == 8, "Initial default grid creates eight slots");
            Program.Check(view.TryGetRaidBounds(out var position, out var size, out var scale), "Actual rendered grid has valid capture and hit-test bounds");
            var grid = view.FindControl<ItemsControl>("RaidGrid")!;
            var screen = grid.PointToScreen(default);
            Program.Check(position == new Point(screen.X, screen.Y) && Math.Abs(size.X - grid.Bounds.Width * scale) <= 1, "Capture coordinates match rendered grid and monitor scale");
            vm.UpdateNames(Names("Alice", "Brianna"));
            var alice = vm.RaidHotCells.Single(c => c.Name == "ALICE");
            var brianna = vm.RaidHotCells.Single(c => c.Name == "BRIANNA");
            vm.UpdateNames(Names("Brianna", "Alice"));
            Program.Check(ReferenceEquals(alice, vm.RaidHotCells.Single(c => c.Row == 0 && c.Column == 1)) &&
                          ReferenceEquals(brianna, vm.RaidHotCells.Single(c => c.Row == 0 && c.Column == 0)), "Swapping player names retains each player's cell object");
            vm.UpdateNames(new List<PlacedName> { new() { Name = "Ária", Row = 0, Column = 0 } });
            var accented = vm.RaidHotCells.Single(c => c.Name == "ÁRIA");
            vm.UpdateNames(Names("Aria", "Ária"));
            Program.Check(!ReferenceEquals(accented, vm.RaidHotCells.Single(c => c.Row == 0 && c.Column == 0)) &&
                          ReferenceEquals(accented, vm.RaidHotCells.Single(c => c.Row == 0 && c.Column == 1)),
                "A new unaccented name does not steal an accented player's existing cell");
            vm.UpdateNames(Names("Alice", "ALICE"));
            Program.Check(Program.Call(vm, "GetCellThatMatchesName", "Alice") == null,
                "Duplicate placements do not route timers to an arbitrary slot");
            vm.UpdateNames(Names("Alice", "Brianna"));
            var beforeRows = vm.Rows;
            vm.Rows = 0; vm.Columns = -1;
            Program.Check(vm.Rows == beforeRows && vm.Columns == 2, "Invalid dimensions do not corrupt the grid");
            await Settle();
            Program.Snapshot(main, "setup");
            Program.Snapshot(vm._overlayWindow, "overlay");

            vm.BeginManualAlignment();
            Program.Check(vm.IsManuallyAligning && !vm.Editable && !vm.CanDetect, "Manual assignment locks dragging and excludes OCR");
            Program.Check(Program.Field(view, "_mouseHookHandler") != null, "Manual mode installs click hook immediately");
            Program.Call(view, "GlobalMouseDown", new Point(position.X + size.X * .25, position.Y + size.Y * .375));
            Program.Check(vm.CurrentNames.Count == 2, "Pending click preserves existing assignments");
            Program.Call(vm, "CorrectCellWhenTargeted", Selection("Oldplayer", _7_0LogParsing.TargetClearedId));
            Program.Check(vm.CurrentNames.Count == 2 && vm.CurrentNames.All(n => n.Name != "Oldplayer"), "Target-cleared log does not assign old player");
            Program.Call(vm, "CorrectCellWhenTargeted", Selection("Carol", _7_0LogParsing.TargetSetId));
            Program.Check(vm.CurrentNames.Any(n => n.Name == "Carol" && n.Row == 1 && n.Column == 0), "Target-set log assigns clicked slot");
            Program.Call(view, "GlobalMouseDown", new Point(position.X + size.X, position.Y + size.Y * .5));
            Program.Call(view, "GlobalMouseDown", new Point(position.X + size.X * .5, position.Y - 1));
            Program.Check(!((ManualRaidFrameAlignment)Program.Field(vm, "_manualAlignment")!).HasPendingClick,
                "Right boundary and toolbar clicks do not select a raid slot");
            await Settle();
            Program.Snapshot((Window)Program.Field(vm, "_manualWindow")!, "manual-instructions");

            vm.CellClicked(.75, .875);
            var pending = (ManualRaidFrameAlignment)Program.Field(vm, "_manualAlignment")!;
            pending.RecordClick(3, 1, DateTime.Now.AddSeconds(-5));
            Program.Call(vm, "ExpireManualClick");
            Program.Check(!pending.HasPendingClick && vm.CurrentNames.Count == 3 && vm.AlignmentStatus.Contains("No new player selection"), "Timeout explains recovery and preserves assignments");
            vm.Editable = false;
            Program.Check(!vm.IsManuallyAligning && Program.Field(view, "_mouseHookHandler") == null && Program.Field(vm, "_manualWindow") == null, "Global lock exits manual mode and removes hook even when already locked");

            vm.Editable = true;
            vm.BeginManualAlignment();
            vm.CellClicked(.75, .875);
            vm.EndManualAlignment();
            Program.Call(vm, "CorrectCellWhenTargeted", Selection("TooLate", _7_0LogParsing.TargetSetId));
            Program.Check(!vm.IsManuallyAligning && vm.Editable && !pending.HasPendingClick && vm.CurrentNames.All(n => n.Name != "TooLate"), "Finish restores editing and discards late selection");
            vm.BeginManualAlignment();
            ((Window)Program.Field(vm, "_manualWindow")!).Close();
            Program.Check(!vm.IsManuallyAligning && Program.Field(view, "_mouseHookHandler") == null, "Closing instructions exits manual mode cleanly");

            vm.BeginManualAlignment();
            Program.Call(vm, "OnStartCombat", new CombatStatusUpdate { Type = UpdateType.Start });
            await Settle();
            Program.Check(vm.IsManuallyAligning && Program.Field(view, "_mouseHookHandler") == null, "Combat pauses assignment and removes hook");
            Program.Call(vm, "OnStartCombat", new CombatStatusUpdate { Type = UpdateType.Stop });
            await Settle();
            Program.Check(Program.Field(view, "_mouseHookHandler") != null, "Leaving combat resumes manual assignment hook");
            vm.Rows = 3;
            Program.Check(!vm.IsManuallyAligning && vm.CurrentNames.Count == 0 && vm.RaidHotCells.Count == 6, "Grid resize ends assignment and clears obsolete placements");

            vm.UpdateNames(Names("Alice", "Brianna"));
            vm.Editable = true;
            vm.BeginManualAlignment();
            config.RaidHotsEnabled = false;
            Program.Check(!vm.IsManuallyAligning && Program.Field(view, "_mouseHookHandler") == null && vm.CurrentNames.Count == 2, "Disabling overlay stops hook and preserves names");
            config.RaidHotsEnabled = true;
            vm.HideOverlayWindow();
            await vm.DetectPlayersAsync();
            Program.Check(vm.CurrentNames.Count == 2 && vm.AlignmentStatus.Contains("Show the raid overlay"), "Hidden detection exits safely without capture or network");
        }
        catch (Exception error) { failure = error; }
        finally
        {
            try { vm.EndManualAlignment(false); config.RaidHotsEnabled = false; } catch { }
            File.WriteAllText(Path.Combine(Program.Output, "report.json"), JsonSerializer.Serialize(new { Status = failure == null ? "Passed" : "Failed", Checks = Program.Checks, Error = failure?.ToString(), NetworkCalls = 0, ScreenUploads = 0 }, new JsonSerializerOptions { WriteIndented = true }));
            Environment.Exit(failure == null ? 0 : 1);
        }
    }

    private static async Task Settle()
    {
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        await Task.Delay(50);
    }
    private static List<PlacedName> Names(string first, string second) => new()
    {
        new() { Name = first, Row = 0, Column = 0 }, new() { Name = second, Row = 0, Column = 1 }
    };
    private static ParsedLogEntry Selection(string target, ulong effect) => new()
    {
        TimeStamp = DateTime.Now.AddMilliseconds(1),
        SourceInfo = new EntityInfo { Entity = new Entity { Name = "Local", IsLocalPlayer = true, IsCharacter = true } },
        TargetInfo = new EntityInfo { Entity = new Entity { Name = target, IsCharacter = true } },
        Effect = new Effect { EffectType = EffectType.TargetChanged, EffectId = effect }
    };
}
