using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Media;
using System.Collections.Concurrent;
using System.Windows.Input;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.Challenge;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Model.Phases;
using SWTORCombatParser.Model.RunRace;
using SWTORCombatParser.Model.SessionReview;
using SWTORCombatParser.Model.Timers;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.Utilities.Encounter_Selection;
using SWTORCombatParser.ViewModels;
using SWTORCombatParser.ViewModels.Challenges;
using SWTORCombatParser.ViewModels.Combat_Monitoring;
using SWTORCombatParser.ViewModels.Overlays;
using SWTORCombatParser.ViewModels.Phases;
using SWTORCombatParser.ViewModels.Timers;
using SWTORCombatParser.Views;
using SWTORCombatParser.Views.Challenges;
using SWTORCombatParser.Views.Overlay;
using SWTORCombatParser.Views.Phases;
using SWTORCombatParser.Views.SettingsView;
using SWTORCombatParser.Views.Timers;

internal static class Program
{
    internal static string Output = "";
    internal static bool Baseline;
    internal static bool Interactive;
    internal static bool OverlaysOnly;
    internal static bool RaidDataOnly;
    internal static bool RaidDataInteractive;
    internal static bool LogReviewOnly;
    internal static bool LogReviewInteractive;
    internal static List<string> Checks = new();
    internal static List<object> Snapshots = new();
    internal static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length < 1 || args.Length > 2 || (args.Length == 2 && args[1] != "--baseline" && args[1] != "--interactive" && args[1] != "--overlays" && args[1] != "--raid-data" && args[1] != "--raid-data-interactive" && args[1] != "--log-review" && args[1] != "--log-review-interactive")) return 2;
        Output = Path.GetFullPath(args[0]);
        Baseline = args.Contains("--baseline");
        Interactive = args.Contains("--interactive");
        OverlaysOnly = args.Contains("--overlays");
        RaidDataOnly = args.Contains("--raid-data");
        RaidDataInteractive = args.Contains("--raid-data-interactive");
        LogReviewOnly = args.Contains("--log-review");
        LogReviewInteractive = args.Contains("--log-review-interactive");
        if (Directory.Exists(Output) || File.Exists(Output)) return 2;
        Directory.CreateDirectory(Output);
        Environment.CurrentDirectory = AppContext.BaseDirectory;
        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            var path = Path.Combine(AppContext.BaseDirectory, name.Name + ".dll");
            return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
        };
        try
        {
            IsolatePersistence();
            AppBuilder.Configure<ConfigurationHarnessApp>().UsePlatformDetect().UseReactiveUI()
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
        Environment.SetEnvironmentVariable("ORBS_PROFILE_DATA_DIRECTORY", data);
        Environment.SetEnvironmentVariable("ORBS_LOG_LOAD_PROFILE", Path.Combine(Output, "profile.json"));
        var root = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DubaTech", "SWTORCombatParser"));
        var assembly = typeof(Settings).Assembly;
        var types = new[] {
            "Utilities.Settings", "Utilities.Logging", "Utilities.OrbsWindowManager", "Utilities.MetricColorLoader", "Utilities.ShouldShowPopup",
            "Model.Phases.DefaultPhaseManager", "Model.Timers.DefaultOrbsTimersManager", "Model.DataGrid.DataGridDefaults",
            "Model.Notes.RaidNotesReader", "Model.Challenge.DefaultChallengeManager", "Model.CloudRaiding.LeaderboardSettings",
            "Model.Overlays.DefaultPersonalOverlaysManager", "Model.Overlays.DefaultBossFrameManager", "Model.Overlays.DefaultGlobalOverlays",
            "Model.Overlays.DefaultCharacterOverlays", "Model.Overlays.RaidFrameOverlayManager", "Model.Overlays.DefaultRoomOverlayManager", "DataStructures.RoomOverlay.RoomOverlayLoader"
        };
        var redirected = new List<string>();
        foreach (var suffix in types)
        {
            var type = assembly.GetType("SWTORCombatParser." + suffix)!;
            foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
            {
                if (field.FieldType != typeof(string) || field.GetValue(null) is not string original || !Path.IsPathRooted(original)) continue;
                var path = Path.GetFullPath(original);
                if (!path.Equals(root, StringComparison.OrdinalIgnoreCase) && !path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) continue;
                if (field.IsInitOnly) throw new InvalidOperationException("Cannot isolate " + type.Name + "." + field.Name);
                var relative = Path.GetRelativePath(root, path);
                field.SetValue(null, relative == "." ? data : Path.Combine(data, relative));
                redirected.Add(type.Name + "." + field.Name);
            }
        }
        File.WriteAllText(Path.Combine(Output, "isolation.json"), JsonSerializer.Serialize(redirected));
        File.WriteAllText(Path.Combine(data, "general_settings.json"), JsonSerializer.Serialize(new {
            offline_mode = true, Auto_Parse = false, current_tab = "data_grid", combat_logs_path = data,
            overlay_bar_scale = 1.0, custom_audio_paths = Array.Empty<string>(), timers_enabled = false
        }));
        DefaultGlobalOverlays.Init(); DefaultCharacterOverlays.Init(); DefaultPersonalOverlaysManager.Init();
        DefaultBossFrameManager.Init(); RaidFrameOverlayManager.Init(); DefaultOrbsTimersManager.Init(); DefaultChallengeManager.Init();
        EncounterLoader.LoadAllEncounters();
        CombatLogStateBuilder.ClearState();
        DefaultPhaseManager.AddOrUpdatePhase(new Phase {
            Id = Guid.NewGuid(), Name = "Burn phase", PhaseSource = "The Dread Fortress|Gate Commander Draxus",
            StartTrigger = PhaseTrigger.EntityHP, EndTrigger = PhaseTrigger.EntityDeath,
            StartArgs = new PhaseArgs { HPPercentage = 20, EntityIds = new List<long> { 123 } },
            EndArgs = new PhaseArgs { EntityIds = new List<long> { 123 } }
        });
    }

    internal static object? Field(object instance, string name) => instance.GetType().GetField(name, Private)?.GetValue(instance);
    internal static void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); Checks.Add(message); }
    internal static void Snapshot(Window window, string name)
    {
        var size = new PixelSize((int)Math.Ceiling(window.Bounds.Width), (int)Math.Ceiling(window.Bounds.Height));
        using var image = new RenderTargetBitmap(size, new Vector(96, 96));
        image.Render(window);
        image.Save(Path.Combine(Output, name + ".png"));
        Snapshots.Add(new { Name = name, Width = size.Width, Height = size.Height });
    }
}

public sealed partial class ConfigurationHarnessApp : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        var converters = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "converter-resources.json")))!;
        foreach (var converter in converters)
            Resources[converter["Key"]] = Activator.CreateInstance(typeof(Settings).Assembly.GetType("SWTORCombatParser.Utilities.Converters." + converter["Type"])!, true)!;
        var configurationStyle = new Uri("avares://Orbs/Utilities/UIStyles/Configuration.axaml");
        // Compiled AXAML is not necessarily exposed through AssetLoader.Exists.
        // Use the API added with this style as the baseline assembly version marker.
        if (typeof(OverlayViewModel).GetProperty("ContentOpacityPercent") != null)
            Styles.Add(new StyleInclude(configurationStyle) { Source = configurationStyle });
        var overlayStyle = new Uri("avares://Orbs/Utilities/UIStyles/OverlaySurfaces.axaml");
        if (typeof(BaseOverlayViewModel).GetProperty("SupportsPresentation") != null)
            Styles.Add(new StyleInclude(overlayStyle) { Source = overlayStyle });
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (BindingPlugins.DataValidators.Count > 0) BindingPlugins.DataValidators.RemoveAt(0);
            IconGetter.Init();
            MetricColorLoader.Init(); MetricColorLoader.SetCurrentBrushDict();
            var main = new Window { Width = 400, Height = 140, Title = "Isolated configuration checks", ShowActivated = false };
            desktop.MainWindow = main;
            var started = false;
            main.Opened += async (_, _) =>
            {
                if (started) return;
                started = true;
                if (Program.Interactive) await PrepareInteractive(main);
                else if (Program.OverlaysOnly) await RunOverlayChecks(main);
                else if (Program.RaidDataOnly) await RunRaidDataChecks(main);
                else if (Program.RaidDataInteractive) await PrepareRaidDataInteractive(main);
                else if (Program.LogReviewOnly || Program.LogReviewInteractive) await RunLogReviewChecks(main);
                else await Run(main);
            };
        }
        base.OnFrameworkInitializationCompleted();
    }

    private static async Task Capture(Window window, Window owner, string name, bool minimum = true)
    {
        window.ShowActivated = false;
        window.Show(owner);
        await Task.Delay(180);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        Program.Snapshot(window, name);
        if (minimum && !Program.Baseline)
        {
            window.Width = window.MinWidth > 0 ? window.MinWidth : 520;
            window.Height = window.MinHeight > 0 ? window.MinHeight : 380;
            await Task.Delay(80);
            Program.Snapshot(window, name + "-minimum");
            if (name == "timer-list")
            {
                var lines = new List<string>();
                void Describe(Visual visual, int depth)
                {
                    if (visual is Control control)
                        lines.Add(new string(' ', depth * 2) + control.GetType().Name + " #" + control.Name + " bounds=" + control.Bounds + " margin=" + control.Margin +
                            " padding=" + control.GetType().GetProperty("Padding")?.GetValue(control) + " visible=" + control.IsVisible + " col=" + Grid.GetColumn(control) +
                            " text=" + (control is TextBlock text ? text.Text : control is ContentControl content ? content.Content : ""));
                    foreach (var child in visual.GetVisualChildren()) Describe(child, depth + 1);
                }
                foreach (var header in window.GetVisualDescendants().OfType<Control>().Where(control => control.GetType().Name == "DataGridColumnHeader")) Describe(header, 0);
                File.WriteAllLines(Path.Combine(Program.Output, "timer-header-layout.txt"), lines);
                var table = window.GetVisualDescendants().OfType<DataGrid>().Single();
                table.Columns.Single(column => column.Header?.ToString() == "Seconds").Sort(System.ComponentModel.ListSortDirection.Ascending);
                await Task.Delay(80);
                Program.Snapshot(window, "timer-list-sorted-minimum");
            }
        }
        window.Close();
    }

    private static async Task Run(Window main)
    {
        Exception? failure = null;
        try
        {
            var phase = new PhaseModificationViewModel("The Dread Fortress|Gate Commander Draxus") {
                Name = "Burn phase", SelectedPhaseStart = PhaseTrigger.EffectGain,
                SelectedPhaseEnd = PhaseTrigger.EntityHP, EndValue = "20", SelectedTarget = "123456789"
            };
            phase.MultiValueOptions.Add(new RefreshOptionViewModel { Name = "Corrosive Grenade" });
            phase.EndMultiTargetOptions.Add(new RefreshOptionViewModel { Name = "123456789" });
            await Capture(new PhaseModificationView(phase), main, "phase-editor");
            var challenge = new ChallengeModificationViewModel("The Dread Fortress|Gate Commander Draxus") {
                Name = "Priority target damage", SelectedChallengeType = ChallengeType.DamageOut,
                SelectedTarget = "Gate Commander Draxus", Value = "Corrosive Dart"
            };
            await Capture(new ChallengeModificationView(challenge), main, "challenge-editor");
            var challengePhase = new ChallengeModificationViewModel("The Dread Fortress|Gate Commander Draxus") {
                Name = "Burn phase damage", SelectedChallengeType = ChallengeType.MetricDuringPhase
            };
            await Capture(new ChallengeModificationView(challengePhase), main, "challenge-phase-editor");
            var timer = new ModifyTimerViewModel("Shared") { Name = "Corrosive Dart", SelectedTriggerType = TimerKeyType.EffectGained, Effect = "Corrosive Dart", DurationSec = 18, CanBeRefreshed = true };
            await Capture(new TimerModificationWindow(timer), main, "timer-editor");
            if (!Program.Baseline)
            {
                await Capture(new TimerModificationWindow(new ModifyTimerViewModel("Shared") { Name = "Burn threshold", SelectedTriggerType = TimerKeyType.EntityHP, HPPercentage = 20 }), main, "timer-health-editor");
                var variableWindow = new TimerModificationWindow(new ModifyTimerViewModel("Shared") { Name = "Wave counter", SelectedTriggerType = TimerKeyType.VariableCheck, SelectedVariable = "Wave", VariableComparisonVal = 3, IsModifyingVariables = true, SelectedModifyVariable = "Wave" });
                variableWindow.FindControl<TabStrip>("EffectsTabStrip")!.SelectedIndex = 1;
                await Capture(variableWindow, main, "timer-variable-editor");
            }
            await Capture(new MetricColorPickerWindow(new MetricColorPickerViewModel(OverlayType.DPS)), main, "metric-color");
            await Capture(new SettingsWindow(), main, "settings");
            var phaseList = new PhaseListViewModel();
            SelectFixtureEncounter(phaseList.EncounterSelectionView);
            await Capture(new PhaseListView(phaseList), main, "phase-list");
            DefaultChallengeManager.AddChallengesToSource(new List<Challenge> {
                new() { Id = Guid.NewGuid(), Name = "Priority target damage", Source = "The Dread Fortress|Gate Commander Draxus", ChallengeSource = "The Dread Fortress|Gate Commander Draxus", ChallengeTarget = "Gate Commander Draxus", ChallengeType = ChallengeType.DamageOut, IsEnabled = true, BackgroundColor = "100,149,237" },
                new() { Id = Guid.NewGuid(), Name = "Interrupt assignments", Source = "The Dread Fortress|Gate Commander Draxus", ChallengeSource = "The Dread Fortress|Gate Commander Draxus", ChallengeType = ChallengeType.InterruptCount, BackgroundColor = "52,165,71" }
            }, "The Dread Fortress|Gate Commander Draxus");
            DefaultOrbsTimersManager.AddTimersForSource(new List<SWTORCombatParser.DataStructures.Timer> {
                new() { Id = Guid.NewGuid().ToString(), Name = "Opening wave", TimerSource = "The Dread Fortress|Gate Commander Draxus", TriggerType = TimerKeyType.CombatStart, DurationSec = 18, SpecificEncounter = "The Dread Fortress", SpecificBoss = "Gate Commander Draxus", IsUserAddedTimer = true, IsEnabled = true },
                new() { Id = Guid.NewGuid().ToString(), Name = "Burn threshold", TimerSource = "The Dread Fortress|Gate Commander Draxus", TriggerType = TimerKeyType.EntityHP, HPPercentage = 20, DurationSec = 10, SpecificEncounter = "The Dread Fortress", SpecificBoss = "Gate Commander Draxus", IsUserAddedTimer = true }
            }, "The Dread Fortress|Gate Commander Draxus");
            var monitor = new CombatMonitorViewModel();
            using var sessionReview = new SessionReviewService(monitor);
            using var race = new RunRaceService(new RunGhostStore(Path.Combine(Program.Output, "ghosts.json")), isOffline: () => true);
            var overlays = new OverlayViewModel(sessionReview, race);
            if (!Program.Baseline)
            {
                var timerModel = overlays._timersViewModel;
                Program.Check(timerModel.DisciplineTimerSelected && timerModel.SelectedTimerSource == "Shared" &&
                    timerModel.DisciplineTimersList.Contains("Shared"),
                    "Discipline has a valid Shared selection before background defaults finish");
                var updatedOffThread = false;
                System.ComponentModel.PropertyChangedEventHandler threadCheck = (_, args) =>
                {
                    if (args.PropertyName is "DisciplineTimersList" or "SelectedTimerSource" or "TimerRows")
                        updatedOffThread |= !Dispatcher.UIThread.CheckAccess();
                };
                timerModel.PropertyChanged += threadCheck;
                if (Program.Field(timerModel, "_loadDefaultsTask") is Task loadDefaults) await loadDefaults;
                timerModel.PropertyChanged -= threadCheck;
                Program.Check(!updatedOffThread, "Default timer loading updates bound selections and rows on the UI thread");
            }
            await Task.Delay(700);
            if (!Program.Baseline) await CheckTimerMenu(overlays, main);
            if (Program.Baseline) await Capture(new OverlayView(overlays), main, "overlay-settings");
            else await CaptureOverlays(overlays, main);
            var timerSetup = Program.Field(overlays, "_timersViewModel") as TimersCreationViewModel;
            if (timerSetup != null)
            {
                timerSetup.SelectedTimerSourceType = TimerType.Encounter;
                SelectFixtureEncounter(timerSetup.EncounterSelectionView);
                await Capture(new TimersCreationView { DataContext = timerSetup }, main, "timer-list");
            }
            var challengeSetup = Program.Field(overlays, "_challengesViewModel") as ChallengeSetupViewModel;
            if (challengeSetup != null)
            {
                SelectFixtureEncounter(challengeSetup.EncounterSelectionView);
                await Capture(new ChallengeSetupView { DataContext = challengeSetup }, main, "challenge-list");
            }
            if (!Program.Baseline)
            {
                await CheckEditorWorkflows(overlays, main);
                await CheckRaidingEditorLayouts(main);
                await CheckAppearance(overlays);
                await CheckEditorClose(main);
            }
            Program.Check(Program.Snapshots.Count >= 10, "Configuration windows render successfully");
        }
        catch (Exception error) { failure = error; }
        finally
        {
            File.WriteAllText(Path.Combine(Program.Output, "report.json"), JsonSerializer.Serialize(new {
                Status = failure == null ? "Passed" : "Failed", Baseline = Program.Baseline,
                Program.Checks, Program.Snapshots, Error = failure?.ToString(), NetworkCalls = 0, ScreenUploads = 0
            }, new JsonSerializerOptions { WriteIndented = true }));
            Environment.Exit(failure == null ? 0 : 1);
        }
    }

    private static async Task CheckTimerMenu(OverlayViewModel overlays, Window main)
    {
        // Exercise the actual menu command without starting the application's
        // live parser, hotkeys, update checks, or network services.
        var menuOwner = (MainWindowViewModel)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(MainWindowViewModel));
        var model = overlays._timersViewModel;
        var window = new TimersCreationView { DataContext = model, ShowActivated = false };
        typeof(MainWindowViewModel).GetField("_overlayViewModel", Program.Private)!.SetValue(menuOwner, overlays);
        typeof(MainWindowViewModel).GetField("_timersView", Program.Private)!.SetValue(menuOwner, window);
        void HideOnClose(object? sender, WindowClosingEventArgs args) { args.Cancel = true; window.Hide(); }
        window.Closing += HideOnClose;
        try
        {
            model.SelectedTimerSourceType = TimerType.Discipline;
            ((ICommand)menuOwner.ShowTimerWindowCommand).Execute(null);
            await Task.Delay(100);
            Program.Check(window.IsVisible, "Timers menu command opens the cached discipline window");
            var sources = window.GetVisualDescendants().OfType<ComboBox>()
                .Single(combo => ReferenceEquals(combo.ItemsSource, model.DisciplineTimersList));
            Program.Check(Equals(sources.SelectedItem, "Shared"), "Discipline dropdown displays Shared when no character is loaded");
            sources.IsDropDownOpen = true;
            await Task.Delay(80);
            Program.Check(sources.IsDropDownOpen, "Timer source dropdown opens without a converter exception");
            sources.IsDropDownOpen = false;
            foreach (var source in model.DisciplineTimersList.Take(3))
            {
                sources.SelectedItem = source;
                await Task.Delay(50);
            }
            var chosenDisciplineSource = sources.SelectedItem as string;
            var chosenDisciplineRows = model.TimerRows.Select(row => row.SourceTimer.Id).ToArray();
            var timerTabs = window.GetVisualDescendants().OfType<TabStrip>().Single();
            timerTabs.SelectedItem = TimerType.Encounter;
            await Task.Delay(80);
            SelectFixtureEncounter(model.EncounterSelectionView);
            var chosenEncounterSource = model.SelectedTimerSource;
            typeof(TimersCreationViewModel).GetMethod("RefreshAvaialbleTriggerOwners", Program.Private)!.Invoke(model, null);
            await Task.Delay(80);
            Program.Check(model.SelectedTimerSourceType == TimerType.Encounter && model.SelectedTimerSource == chosenEncounterSource &&
                Equals(sources.SelectedItem, chosenDisciplineSource),
                "Refreshing available disciplines preserves both selections while Encounter is visible");
            timerTabs.SelectedItem = TimerType.Discipline;
            await Task.Delay(80);
            Program.Check(model.DisciplineTimerSelected && model.SelectedTimerSource == chosenDisciplineSource &&
                Equals(sources.SelectedItem, chosenDisciplineSource) &&
                model.TimerRows.Select(row => row.SourceTimer.Id).SequenceEqual(chosenDisciplineRows),
                "Switching back to Discipline restores its selected dropdown source and rows");
            timerTabs.SelectedItem = TimerType.Encounter;
            await Task.Delay(80);
            Program.Check(!model.DisciplineTimerSelected && model.SelectedTimerSource == chosenEncounterSource,
                "Switching back to Encounter preserves its selected encounter and boss");
            var newDiscipline = SWTORCombatParser.DataStructures.ClassInfos.ClassLoader.LoadAllClasses()
                .Select(characterClass => characterClass.Discipline).First(discipline => !model.DisciplineTimersList.Contains(discipline));
            typeof(TimersCreationViewModel).GetMethod("SelectCurrentDisciplineSource", Program.Private)!.Invoke(model, new object[] { newDiscipline });
            await Task.Delay(80);
            Program.Check(model.SelectedTimerSourceType == TimerType.Encounter && model.SelectedTimerSource == chosenEncounterSource &&
                model.DisciplineTimersList.Contains(newDiscipline),
                "A newly detected discipline is available without replacing the active encounter");
            timerTabs.SelectedItem = TimerType.Discipline;
            await Task.Delay(80);
            Program.Check(Equals(sources.SelectedItem, newDiscipline) && model.SelectedTimerSource == newDiscipline,
                "A newly detected discipline has a valid dropdown selection when its tab opens");
            sources.SelectedItem = chosenDisciplineSource;
            typeof(TimersCreationViewModel).GetMethod("RefreshAvaialbleTriggerOwners", Program.Private)!.Invoke(model, null);
            await Task.Delay(80);
            Program.Check(Equals(sources.SelectedItem, chosenDisciplineSource) && model.SelectedTimerSource == chosenDisciplineSource,
                "Refreshing the discipline list retains a manually selected source");
            Program.Snapshot(window, "timer-menu-discipline");
            window.Close();
            Program.Check(!window.IsVisible, "Closing the cached timer window hides it for reuse");
            ((ICommand)menuOwner.ShowTimerWindowCommand).Execute(null);
            await Task.Delay(80);
            Program.Check(window.IsVisible, "Timers menu command reopens the same window");
            model.SelectedTimerSourceType = TimerType.Encounter;
            SelectFixtureEncounter(model.EncounterSelectionView);
            window.Close();
            ((ICommand)menuOwner.ShowTimerWindowCommand).Execute(null);
            await Task.Delay(80);
            Program.Check(window.IsVisible && model.TimerRows.Count > 0, "Timers menu refreshes and reopens populated encounter rows");

            var sourceModel = new TimerLabelProbe();
            var text = new TextBox();
            using var binding = text.Bind(TextBox.TextProperty, new Avalonia.Data.Binding(nameof(TimerLabelProbe.Trigger))
            {
                Source = sourceModel, Mode = Avalonia.Data.BindingMode.TwoWay,
                Converter = new SWTORCombatParser.Utilities.Converters.EnumDisplayNameConverter()
            });
            text.SetCurrentValue(TextBox.TextProperty, "Effect Gained");
            Program.Check(sourceModel.Trigger == TimerKeyType.CombatStart,
                "Reverse display-label binding does not throw or change the stored timer trigger");
        }
        finally
        {
            window.Closing -= HideOnClose;
            window.Close();
        }
    }

    private sealed class TimerLabelProbe
    {
        public TimerKeyType Trigger { get; set; } = TimerKeyType.CombatStart;
    }

    private static async Task CaptureOverlays(OverlayViewModel model, Window main)
    {
        ((ICommand)model.GenerateOverlay).Execute(model.MainDamageOverlays.First());
        ((ICommand)model.GenerateOverlay).Execute(model.MainHealOverlays.First());
        var window = new OverlayView(model) { ShowActivated = false };
        window.Show(main);
        await Task.Delay(180);
        var tabs = window.FindControl<TabControl>("ConfigurationTabs")!;
        foreach (var (index, name) in new[] { (0, "overlay-metrics"), (1, "overlay-utility"), (2, "overlay-appearance") })
        {
            tabs.SelectedIndex = index;
            window.Width = 940; window.Height = 660;
            await Task.Delay(100);
            Program.Snapshot(window, name);
            window.Width = window.MinWidth; window.Height = window.MinHeight;
            await Task.Delay(100);
            Program.Snapshot(window, name + "-minimum");
        }
        tabs.SelectedIndex = 1;
        await Task.Delay(100);
        var raidSetupButton = window.GetVisualDescendants().OfType<Button>().Single(b => b.Content is string title && title == "Raid frame setup…");
        raidSetupButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Task.Delay(100);
        var raidSetup = (Window)Program.Field(window, "_raidSetup")!;
        Program.Snapshot(raidSetup, "raid-frame-settings");
        raidSetup.Width = raidSetup.MinWidth; raidSetup.Height = raidSetup.MinHeight;
        await Task.Delay(100);
        Program.Snapshot(raidSetup, "raid-frame-settings-minimum");
        raidSetup.Close();
        tabs.SelectedIndex = 2;
        await Task.Delay(100);
        var numericInputs = window.GetVisualDescendants().OfType<NumericUpDown>().ToArray();
        var contentInput = numericInputs.Single(n => n.Minimum == 20);
        var text = contentInput.GetVisualDescendants().OfType<TextBox>().Single();
        text.Text = "65%";
        contentInput.RaiseEvent(new RoutedEventArgs(InputElement.LostFocusEvent));
        await Task.Delay(100);
        Program.Check((double)model.GetType().GetProperty("ContentOpacityPercent")!.GetValue(model)! == 65, "Typed content percentage commits through the real numeric input");
        model.GetType().GetProperty("UseCustomBackground")!.SetValue(model, true);
        var backgroundInput = numericInputs.Single(n => n.Minimum == 0);
        backgroundInput.GetVisualDescendants().OfType<TextBox>().Single().Text = "40%";
        backgroundInput.RaiseEvent(new RoutedEventArgs(InputElement.LostFocusEvent));
        await Task.Delay(100);
        Program.Check((double)model.GetType().GetProperty("BackgroundOpacityPercent")!.GetValue(model)! == 40, "Typed background percentage commits through the real numeric input");
        ((ICommand)model.GetType().GetProperty("ResetAppearanceCommand")!.GetValue(model)!).Execute(null);
        tabs.SelectedIndex = 0;
        await Task.Delay(100);
        var metric = window.GetVisualDescendants().OfType<OverlayOptionView>().First();
        var option = (OverlayOptionViewModel)metric.DataContext!;
        var selected = option.IsSelected;
        var colorButton = metric.GetVisualDescendants().OfType<Button>().Single(b => b.Command == null);
        colorButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Task.Delay(100);
        var picker = ((IClassicDesktopStyleApplicationLifetime)Current!.ApplicationLifetime!).Windows.OfType<MetricColorPickerWindow>().Single();
        Program.Check(option.IsSelected == selected, "Explicit metric color button opens its picker without toggling the overlay");
        var colorModel = (MetricColorPickerViewModel)picker.DataContext!;
        var oldColor = colorModel.MetricColor;
        var swatch = colorButton.GetVisualDescendants().OfType<Border>().Single(border => border.Width == 17 && border.Height == 17);
        var currentOverlays = (ConcurrentDictionary<OverlayType, OverlayInstanceViewModel>)Program.Field(model, "_currentOverlays")!;
        var liveOverlay = currentOverlays[option.Type];
        var coloredLabels = liveOverlay.MainContent.GetVisualDescendants().OfType<TextBlock>()
            .Where(label => label.Foreground is ISolidColorBrush brush && brush.Color == oldColor).ToArray();
        colorModel.MetricColor = Colors.MediumPurple;
        await Task.Delay(100);
        Program.Check(swatch.Background is ISolidColorBrush swatchBrush && swatchBrush.Color == Colors.MediumPurple,
            "Metric color changes update the visible configuration swatch immediately");
        Program.Check(coloredLabels.Length > 0 && coloredLabels.All(label => label.Foreground is ISolidColorBrush brush && brush.Color == Colors.MediumPurple),
            "Metric color changes update the already-enabled overlay immediately");
        colorModel.MetricColor = oldColor;
        picker.Close();
        window.Close();
    }

    private static void SelectFixtureEncounter(Control view)
    {
        var selection = (EncounterSelectionViewModel)view.DataContext!;
        selection.SelectedEncounter = selection.AvailableEncounters.First(e => e.Name == "The Dread Fortress");
        selection.SelectedBoss = "Gate Commander Draxus";
    }

    private static async Task CheckEditorClose(Window owner)
    {
        var draftModel = new PhaseModificationViewModel("The Dread Fortress|Gate Commander Draxus") { Name = "Draft phase", SelectedPhaseStart = PhaseTrigger.EffectGain, SelectedPhaseEnd = PhaseTrigger.AbilityUsage };
        var draftWindow = new PhaseModificationView(draftModel) { ShowActivated = false };
        int draftSaves = 0;
        draftModel.OnNewPhase += _ => draftSaves++;
        draftWindow.Show(owner);
        await Task.Delay(50);
        foreach (var (name, value) in new[] { ("StartValueDraft", "Corrosive Dart"), ("EndValueDraft", "Orbital Strike"), ("StartTargetDraft", "123"), ("EndTargetDraft", "456") })
        {
            if (name.Contains("Target")) { draftModel.SelectedPhaseStart = PhaseTrigger.EntitySpawn; draftModel.SelectedPhaseEnd = PhaseTrigger.EntityDeath; }
            var input = draftWindow.FindControl<TextBox>(name)!;
            input.Text = value;
            var key = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter };
            input.RaiseEvent(key);
            Program.Check(key.Handled && draftSaves == 0 && draftWindow.IsVisible, name + " Enter adds an item without saving the phase");
        }
        Program.Check(draftModel.MultiValueOptions.Count == 1 && draftModel.EndMultiValueOptions.Count == 1 && draftModel.MultiTargetOptions.Count == 1 && draftModel.EndMultiTargetOptions.Count == 1, "Each phase draft adds to its corresponding collection");
        draftWindow.Close();
        foreach (var save in new[] { false, true })
        {
            var source = DefaultPhaseManager.GetExisitingPhases().First();
            var model = new PhaseModificationViewModel(source.PhaseSource);
            model.GetType().GetMethod("Edit", Program.Private)!.Invoke(model, new object[] { source });
            int cancelled = 0, saved = 0;
            model.OnCancelEdit += value => { if (ReferenceEquals(value, source)) cancelled++; };
            model.OnNewPhase += _ => saved++;
            var window = new PhaseModificationView(model) { ShowActivated = false };
            window.Show(owner);
            await Task.Delay(50);
            if (save) ((ICommand)model.SaveCommand).Execute(null); else window.Close();
            Program.Check(save ? saved == 1 && cancelled == 0 : cancelled == 1 && saved == 0,
                save ? "Phase save closes without restoring the cancelled edit" : "Native phase close returns the original edit exactly once");

            var challenge = new Challenge { Id = Guid.NewGuid(), Name = "Priority target", Source = source.PhaseSource, ChallengeSource = source.PhaseSource, ChallengeType = ChallengeType.DamageOut, ChallengeTarget = "Gate Commander Draxus", PhaseMetric = OverlayType.None };
            var challengeModel = new ChallengeModificationViewModel(source.PhaseSource);
            challengeModel.Edit(challenge);
            int challengeCancelled = 0, challengeSaved = 0;
            challengeModel.OnCancelEdit += value => { if (ReferenceEquals(value, challenge)) challengeCancelled++; };
            challengeModel.OnNewChallenge += (_, _) => challengeSaved++;
            var challengeWindow = new ChallengeModificationView(challengeModel) { ShowActivated = false };
            challengeWindow.Show(owner);
            await Task.Delay(50);
            if (save) ((ICommand)challengeModel.SaveCommand).Execute(null); else challengeWindow.Close();
            Program.Check(save ? challengeSaved == 1 && challengeCancelled == 0 : challengeCancelled == 1 && challengeSaved == 0,
                save ? "Challenge save closes without restoring the cancelled edit" : "Native challenge close returns the original edit exactly once");

            var originalTimer = new SWTORCombatParser.DataStructures.Timer { Id = Guid.NewGuid().ToString(), Name = "Original timer", TimerSource = "Shared", TriggerType = TimerKeyType.CombatStart, DurationSec = 10, SpecificEncounter = "All", SpecificBoss = "All" };
            var timerModel = new ModifyTimerViewModel("Shared");
            timerModel.Edit(originalTimer);
            int timerCancelled = 0, timerSaved = 0;
            timerModel.OnCancelEdit += value => { if (ReferenceEquals(value, originalTimer)) timerCancelled++; };
            timerModel.OnNewTimer += (_, _, _) => timerSaved++;
            var timerWindow = new TimerModificationWindow(timerModel) { ShowActivated = false };
            timerWindow.Show(owner);
            await Task.Delay(50);
            if (save) ((ICommand)timerModel.SaveCommand).Execute(null); else timerWindow.Close();
            Program.Check(save ? timerSaved == 1 && timerCancelled == 0 : timerCancelled == 1 && timerSaved == 0,
                save ? "Timer save closes without restoring the cancelled edit" : "Native timer close returns the original edit exactly once");
        }
    }

    private static async Task CheckAppearance(OverlayViewModel model)
    {
        // Reflection keeps this harness buildable and runnable against the pre-appearance assembly.
        void Set(string name, object value) => model.GetType().GetProperty(name)!.SetValue(model, value);
        T Get<T>(string name) => (T)model.GetType().GetProperty(name)!.GetValue(model)!;
        var overlayModel = new HarnessOverlay();
        var window = new BaseOverlayWindow(overlayModel);
        var content = window.FindControl<Control>("ContentObject")!;
        var background = window.FindControl<Control>("BackgroundArea")!;
        Set("ContentOpacityPercent", 55d);
        Set("UseCustomBackground", true);
        Set("BackgroundOpacityPercent", 35d);
        await Task.Delay(100);
        Program.Check(Math.Abs(content.Opacity - .55) < .001 && window.Opacity == 1, "Content opacity updates live without fading the whole window");
        Program.Check(Math.Abs(background.Opacity - overlayModel.BackgroundUnLockedOpacity) < .001, "Unlocked overlay preserves its readable editing background");
        window.GetType().GetMethod("ApplyAppearance", Program.Private)!.Invoke(window, new object?[] { true });
        Program.Check(Math.Abs(background.Opacity - .35) < .001, "Locked overlay applies custom background opacity");
        Program.Check(window.FindControl<Control>("CloseButton")!.Opacity == 1, "Overlay editing controls stay fully opaque");
        Set("ContentOpacityPercent", -100d);
        Set("BackgroundOpacityPercent", 200d);
        Program.Check(Get<double>("ContentOpacityPercent") == 20 && Get<double>("BackgroundOpacityPercent") == 100, "Appearance values clamp to supported ranges");
        ((ICommand)model.GetType().GetProperty("ResetAppearanceCommand")!.GetValue(model)!).Execute(null);
        await Task.Delay(100);
        Program.Check(Get<double>("ContentOpacityPercent") == 100 && !Get<bool>("UseCustomBackground") && content.Opacity == 1, "Restore appearance defaults resets settings and the live overlay");
        window.Close();
    }
}

internal sealed class HarnessOverlay : BaseOverlayViewModel
{
    public HarnessOverlay() : base("Configuration harness") { }
    public override bool ShouldBeVisible => false;
}
