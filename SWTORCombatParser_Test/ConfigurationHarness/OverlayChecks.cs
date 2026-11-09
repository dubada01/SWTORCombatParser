using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Model.RunRace;
using SWTORCombatParser.Model.SessionReview;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels;
using SWTORCombatParser.ViewModels.Challenges;
using SWTORCombatParser.ViewModels.Combat_Monitoring;
using SWTORCombatParser.ViewModels.Overlays;
using SWTORCombatParser.ViewModels.Overlays.BossFrame;
using SWTORCombatParser.ViewModels.RunRace;
using SWTORCombatParser.ViewModels.Timers;
using SWTORCombatParser.Views;
using SWTORCombatParser.Views.Overlay;
using SWTORCombatParser.Views.Timers;
using Timer = SWTORCombatParser.DataStructures.Timer;

public sealed partial class ConfigurationHarnessApp
{
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int ReadWindowStyle(IntPtr handle, int index);
    private static bool ClicksPassThrough(Window window) =>
        (ReadWindowStyle(window.TryGetPlatformHandle()!.Handle, -20) & 0x20) != 0;

    private static TimerInstanceViewModel OverlayTimer(string name, double value, Color color, TimerKeyType trigger = TimerKeyType.CombatStart) =>
        new(new Timer { Id = Guid.NewGuid().ToString(), Name = name, DurationSec = 90, TimerColor = color,
            TriggerType = trigger, UseAudio = false })
        { DisplayTimer = true, DisplayTimerValue = true, TimerValue = value, Scale = 1 };

    private static async Task SettleOverlay() { await Task.Delay(100); await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background); }

    private static async Task CheckOverlayModes(BaseOverlayViewModel model, string name, double width, double height)
    {
        var window = model._overlayWindow;
        model.OverlaysMoveable = true;
        model.Active = true;
        window.Show();
        await SettleOverlay();
        window.Position = new PixelPoint(120, 100);
        window.Width = width; window.Height = height;
        await SettleOverlay();
        typeof(BaseOverlayWindow).GetMethod("UpdateState", Program.Private)!.Invoke(window, null);
        var selector = window.FindControl<ComboBox>("PresentationSelector")!;
        Program.Check(selector.IsVisible && selector.SelectedIndex == 0, name + " defaults to Compact and offers its real style selector");
        Program.Check(!ClicksPassThrough(window), name + " is interactive while unlocked");
        Program.Check(!window.ShowActivated && !window.ShowInTaskbar && window.Topmost, name + " preserves non-activating utility-window behavior");
        selector.IsDropDownOpen = true;
        await SettleOverlay();
        Program.Check(selector.IsDropDownOpen, name + " presentation dropdown opens");
        selector.IsDropDownOpen = false;
        Program.Snapshot(window, name + "-unlocked");
        selector.SelectedIndex = 1;
        await SettleOverlay();
        Program.Check(model.PresentationIndex == 1 && window.Classes.Contains("OverlayDetailed"), name + " dropdown persists and applies Detailed");
        model.OverlaysMoveable = false;
        await SettleOverlay();
        Program.Check(window.IsVisible && ClicksPassThrough(window), name + " locks into a visible click-through window");
        var position = window.Position;
        var originalWidth = window.Width;
        Program.Snapshot(window, name + "-detailed");
        Program.Check(window.FindControl<Control>("PresentationLayout")!.GetVisualDescendants().OfType<TextBlock>()
            .Where(t => t.Classes.Contains("OverlayDetail")).All(t => t.IsVisible), name + " reveals detailed text");
        OverlayPresentationSettings.Write(model.OverlayDisplayName == "Run Race" ? "Timeline" : name switch {
            "boss" => "BossFrame", "encounter-timers" => "Encounter", "discipline-timers" => "Discipline",
            "metrics" => "DPS", "challenges" => "Challenges", _ => name }, 0);
        await SettleOverlay();
        Program.Check(ClicksPassThrough(window) && window.Classes.Contains("OverlayCompact"), name + " changes style while locked without capturing clicks");
        Program.Check(window.Position == position && Math.Abs(window.Width - originalWidth) < .1, name + " keeps position and width when switching style");
        Program.Snapshot(window, name + "-compact");
        window.Hide(); window.Show();
        await SettleOverlay();
        Program.Check(ClicksPassThrough(window) && model.PresentationIndex == 0, name + " reopens with the saved style and click-through state");
        Program.Check(window.Position == position && Math.Abs(window.Width - originalWidth) < .1, name + " reopens at its saved position and width");
        var detailControls = window.GetVisualDescendants().OfType<Control>().Where(c => c.Classes.Contains("OverlayDetail")).ToArray();
        Program.Check(detailControls.All(c => !c.IsVisible), name + " hides optional detail in Compact");
        window.Width = Math.Min(width, name == "race" ? 380 : 250);
        await SettleOverlay();
        Program.Snapshot(window, name + "-narrow");
        if (model is TimersWindowViewModel)
        {
            foreach (var timerView in window.GetVisualDescendants().OfType<TimerInstanceView>())
            {
                var label = timerView.FindControl<TextBlock>("NameText")!;
                var value = timerView.FindControl<TextBlock>("ValueText")!;
                Program.Check(label.FontSize == 14 && value.Bounds.Width > 10 && label.Bounds.Width > 20,
                    name + " retains readable text and a separate countdown at narrow width");
            }
        }
        window.Hide();
    }

    private static async Task RunOverlayChecks(Window main)
    {
        Exception? failure = null;
        try
        {
            var encounter = new EncounterTimerWindowViewModel("Encounter");
            typeof(EncounterTimerWindowViewModel).GetField("inBossRoom", Program.Private)!.SetValue(encounter, true);
            encounter.SwtorTimers.Add(OverlayTimer("Mass Affliction — move away from the group", 12.4, Colors.CornflowerBlue));
            encounter.SwtorTimers.Add(OverlayTimer("Dread Touch", 3.2, Colors.OrangeRed));
            encounter.SwtorTimers[1].TimerBackground = new SolidColorBrush(Colors.OrangeRed);
            encounter.SwtorTimers.Add(OverlayTimer("Next add wave", 38.6, Colors.MediumPurple));
            await CheckOverlayModes(encounter, "encounter-timers", 360, 210);
            var countdown = encounter.SwtorTimers[0];
            countdown.TriggerTimeTimer(TimeUtility.CorrectedTime);
            await Task.Delay(250);
            var countdownView = encounter.MainContent.GetVisualDescendants().OfType<TimerInstanceView>().First(v => ReferenceEquals(v.DataContext, countdown));
            Program.Check(countdown.TimerValue < 90 && countdownView.FindControl<TextBlock>("ValueText")!.Text == countdown.OverlayTimerValue,
                "Live countdown ticks update the visible numeric column");
            countdown.Dispose();

            var discipline = new DisciplineTimersWindowViewModel("Discipline");
            discipline.SwtorTimers.Add(OverlayTimer("Corrosive Dart on Dread Master Brontes", 17.8, Colors.MediumSeaGreen));
            discipline.SwtorTimers.Add(OverlayTimer("Adrenal", 6.1, Colors.Goldenrod));
            discipline.SwtorTimers.Add(OverlayTimer("Shield remaining", 45000, Colors.SteelBlue, TimerKeyType.AbsorbShield));
            var absorb = discipline.SwtorTimers[2];
            var absorbTask = absorb.TriggerAbsorbTimer(60000);
            absorb.DamageDoneToAbsorb = 15000;
            await Task.Delay(150);
            await CheckOverlayModes(discipline, "discipline-timers", 360, 210);
            Program.Check(absorb.TimerName.Contains("45,000/60,000") && !absorb.DisplayTimerValue && Math.Abs(absorb.CurrentRatio - .75) < .001,
                "Absorb timer keeps remaining shield information and its correct bar ratio");
            var absorbView = discipline.MainContent.GetVisualDescendants().OfType<TimerInstanceView>().First(v => ReferenceEquals(v.DataContext, absorb));
            Program.Check(absorbView.FindControl<TextBlock>("ValueText")!.Text == "45,000", "Absorb remaining amount stays in the numeric column instead of being truncated with the name");
            absorb.Dispose(); await absorbTask;
            Program.Check(OverlayPresentationSettings.Read("Encounter") == 0, "Discipline presentation changes do not change encounter presentation");

            var boss = new BossFrameConfigViewModel("BossFrame") { CombatDuration = "04:38", CombatDurationVisible = true };
            var bossInfo = new EntityInfo { Entity = new Entity { Name = "Dread Master Brontes", Id = 123, LogId = 123, IsBoss = true }, MaxHP = 28_000_000, CurrentHP = 5_124_000 };
            var bossRow = new BossFrameViewModel(bossInfo, false, 1);
            boss.BossesDetected.Add(bossRow);
            var hp = (HPModuleViewModel)bossRow.HPContent.DataContext!;
            hp.CurrentTargetIndicatorVisible = true; hp.CurrentBossTarget = "Tanking-player-with-a-long-name"; hp.UpdateHP(5_124_000);
            var second = new BossFrameViewModel(new EntityInfo { Entity = new Entity { Name = "Dread Master Brontes — Kephess clone", Id = 124, LogId = 124 }, MaxHP = 3_600_000 }, false, 1);
            boss.BossesDetected.Add(second);
            ((HPModuleViewModel)second.HPContent.DataContext!).UpdateHP(1900000);
            var dotModule = (DotModuleViewModel)bossRow.DOTSContent.DataContext!;
            dotModule.ActiveDOTS.Add(OverlayTimer("Corrosive Dart", 14.8, Colors.MediumSeaGreen));
            var mechanics = (MechanicsTimersModuleViewModel)bossRow.MechanicsModule.DataContext!;
            var healthTimer = OverlayTimer("Burn phase", 20, Colors.Goldenrod, TimerKeyType.EntityHP);
            healthTimer.SourceTimer.HPPercentage = 20; healthTimer.MaxTimerValue = 100;
            mechanics.UpcomingMechanics.Add(healthTimer);
            await CheckOverlayModes(boss, "boss", 360, 260);
            Program.Check(healthTimer.HealthTimerLabel == "Burn phase" && healthTimer.HealthTimerValue == "20%", "Boss mechanics show one clear health threshold alongside the name");
            boss._overlayWindow.Show();
            boss.OverlaysMoveable = true;
            await SettleOverlay();
            var decrease = boss._overlayWindow.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "DecreaseScale");
            for (var i = 0; i < 20; i++)
                ((Avalonia.Automation.Provider.IInvokeProvider)new Avalonia.Automation.Peers.ButtonAutomationPeer(decrease)).Invoke();
            Program.Check(boss.CurrentScale == .1 && hp.Height > 0, "Boss scale buttons stop at a valid positive size");
            boss.CurrentScale = 1;
            boss.OverlaysMoveable = false;
            Settings.WriteSetting(Settings.BossFrameTargetSetting, false);
            Settings.WriteSetting(Settings.BossFrameDOTVisibilitySetting, false);
            Program.Check(!hp.CurrentTargetIndicatorVisible && !dotModule.PersonalDOTSAreEnabled, "Boss target and DoT visibility settings still apply");
            Settings.WriteSetting(Settings.BossFrameTargetSetting, true);
            Settings.WriteSetting(Settings.BossFrameDOTVisibilitySetting, true);
            foreach (var value in new[] { 0d, 28_000_000d, 35_000_000d, double.NaN, double.PositiveInfinity })
            {
                hp.UpdateHP(value); await SettleOverlay();
                Program.Check(double.IsFinite(hp.BarWidth.Value) && hp.BarWidth.Value is >= 0 and <= 1 && !hp.HPPercentText.Contains("NaN"), "Boss HP stays finite and bounded for " + value);
            }
            hp.BossMaxHP = 0;
            Program.Check(hp.HPPercentText == "—", "Unknown boss max HP shows a dash instead of NaN or infinity");
            Program.Snapshot(boss._overlayWindow, "boss-unknown-health");
            hp.BossMaxHP = 28_000_000; hp.UpdateHP(5124000);
            Program.Check(hp.HPPercentText == "18.3%", "Boss percentage and fill recover when valid health arrives");
            boss._overlayWindow.Hide();

            var metrics = new OverlayInstanceViewModel(OverlayType.DPS) { MetricTotal = "113,482", UsingLeaderboard = false };
            foreach (var (name, value) in new[] { ("A very long character name", 39870d), ("Orbs", 38240d), ("Mercenary", 35372d) })
                metrics.MetricBars.Add(new OverlayMetricInfo { Player = new Entity { Name = name, IsCharacter = true, IsLocalPlayer = name == "Orbs" }, Type = OverlayType.DPS, Value = value, RelativeLength = value / 39870, SizeScalar = 1 });
            await CheckOverlayModes(metrics, "metrics", 360, 180);

            var challenge = new ChallengeWindowViewModel("Challenges");
            typeof(ChallengeWindowViewModel).GetField("inBossRoom", Program.Private)!.SetValue(challenge, true);
            var challengeRow = new ChallengeInstanceViewModel(new Challenge { Name = "Burn phase damage", ChallengeType = ChallengeType.DamageOut, UseRawValues = true, BackgroundColor = "92,149,176" }) { MetricTotal = "5,236,820" };
            foreach (var (name, value) in new[] { ("A very long character name", 1987820d), ("Orbs", 1799000d), ("Mercenary", 1450000d) })
                challengeRow.MetricBars.Add(new ChallengeOverlayMetricInfo(new SolidColorBrush(Colors.SteelBlue)) { Player = new Entity { Name = name }, Value = value, RelativeLength = value / 1987820, SizeScalar = 1 });
            challenge.ActiveChallengeInstances.Add(challengeRow);
            await CheckOverlayModes(challenge, "challenges", 360, 210);

            using var raceService = new RunRaceService(new RunGhostStore(Path.Combine(Program.Output, "overlay-ghosts.json")), isOffline: () => true);
            using var race = new RunRaceOverlayViewModel(raceService);
            typeof(RunRaceViewModel).GetMethod("OnUpdated", Program.Private)!.Invoke(race.Race, new object[] {
                new RunRaceSnapshot(new RunCohort("The Dread Fortress", "Story", "8 Players"), DateTime.Now, 1058,
                    new[] { new RaceCheckpoint("Nefra", 1, 112, 130, -18, true, 30, 35), new RaceCheckpoint("Draxus", 1, 550, 592, -42, true, 220, 235), new RaceCheckpoint("Grob'thok", 1, null, 1100, null, true), new RaceCheckpoint("Corruptor Zero", 0, null, 1480, null, true), new RaceCheckpoint("Brontes", 0, null, 1960, null, true) },
                    0, false, null, "Personal best", "Run in progress", ReferenceLabel: "Personal best", ReferenceDurationSeconds: 1960, ActiveBossName: "Grob'thok", ActiveBossStartSeconds: 780) });
            await SettleOverlay();
            await CheckOverlayModes(race, "race", 440, 310);
            Program.Check(race.MainContent.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "The Dread Fortress"), "Race overlay displays the fixture through the real snapshot update path");

            // The config dropdown and the unlocked window edit the same saved per-overlay setting.
            using var session = new SessionReviewService(new CombatMonitorViewModel());
            var settingsModel = new OverlayViewModel(session, raceService);
            var settingsWindow = new OverlayView(settingsModel);
            settingsWindow.Show(main);
            settingsWindow.FindControl<TabControl>("ConfigurationTabs")!.SelectedIndex = 2;
            await SettleOverlay();
            var overlayChoice = settingsWindow.FindControl<ComboBox>("PresentationOverlay")!;
            var styleChoice = settingsWindow.FindControl<ComboBox>("PresentationStyle")!;
            overlayChoice.SelectedItem = settingsModel.PresentationOverlays.Single(o => o.Key == "BossFrame");
            styleChoice.SelectedIndex = 1;
            await SettleOverlay();
            Program.Check(boss.PresentationIndex == 1 && boss._overlayWindow.Classes.Contains("OverlayDetailed"), "Appearance tab updates the existing boss overlay live");
            overlayChoice.SelectedItem = settingsModel.PresentationOverlays.Single(o => o.Key == "Discipline");
            await SettleOverlay();
            Program.Check(styleChoice.SelectedIndex == 0 && OverlayPresentationSettings.Read("BossFrame") == 1, "Switching the settings overlay selector preserves each independent style");
            OverlayPresentationSettings.Write("Discipline", 1);
            await SettleOverlay();
            Program.Check(styleChoice.SelectedIndex == 1, "Appearance dropdown follows style changes made elsewhere");
            Program.Snapshot(settingsWindow, "overlay-presentation-settings");
            settingsWindow.Close();
            var reopened = new DisciplineTimersWindowViewModel("Discipline");
            Program.Check(reopened.PresentationIndex == 1 && reopened._overlayWindow.Classes.Contains("OverlayDetailed"), "A newly created overlay reads its persisted style");
            var unsupported = new HarnessOverlay(); unsupported.PresentationIndex = 1;
            Program.Check(unsupported.PresentationIndex == 0 && !unsupported.SupportsPresentation, "Other overlays and raid-grid geometry do not opt into presentation scaling");
            OverlayAppearanceSettings.SetBackgroundOpacity(0);
            await SettleOverlay();
            var surface = (ISolidColorBrush)boss._overlayWindow.Resources["OverlaySurfaceBrush"]!;
            var card = boss.MainContent.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("OverlayCard"));
            Program.Check(surface.Opacity == 0 && card.Background is ISolidColorBrush cardBrush && cardBrush.Opacity == 0,
                "Zero background opacity also removes the new overlay surface backgrounds");
            await CaptureOverlayContrast(main);
        }
        catch (Exception error) { failure = error; }
        finally
        {
            File.WriteAllText(Path.Combine(Program.Output, "report.json"), JsonSerializer.Serialize(new {
                Status = failure == null ? "Passed" : "Failed", Program.Checks, Program.Snapshots,
                Error = failure?.ToString(), NetworkCalls = 0, ScreenUploads = 0
            }, new JsonSerializerOptions { WriteIndented = true }));
            Environment.Exit(failure == null ? 0 : 1);
        }
    }

    private static async Task CaptureOverlayContrast(Window owner)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*"), RowDefinitions = new RowDefinitions("Auto,*"), Margin = new Thickness(14) };
        var names = new[] { "boss-compact", "boss-detailed", "encounter-timers-compact", "encounter-timers-detailed" };
        for (var i = 0; i < names.Length; i++)
        {
            var label = new TextBlock { Text = names[i], Foreground = Brushes.WhiteSmoke, Margin = new Thickness(5,0,5,10), FontSize = 13 };
            Grid.SetColumn(label, i); grid.Children.Add(label);
            var scene = new Grid { Margin = new Thickness(5,0), ClipToBounds = true,
                Background = new LinearGradientBrush { StartPoint = new RelativePoint(0,0,RelativeUnit.Relative), EndPoint = new RelativePoint(1,1,RelativeUnit.Relative),
                    GradientStops = new GradientStops { new(Colors.LightSkyBlue, 0), new(Color.Parse("#CBA56A"), .4), new(Color.Parse("#22382F"), .65), new(Color.Parse("#213955"), 1) } } };
            var texture = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*,*,*"), Opacity = .2 };
            for (var stripe = 0; stripe < 6; stripe++) { var line = new Border { Background = stripe % 2 == 0 ? Brushes.White : Brushes.Black, Margin = new Thickness(10,0) }; Grid.SetColumn(line, stripe); texture.Children.Add(line); }
            scene.Children.Add(texture);
            scene.Children.Add(new Image { Source = new Avalonia.Media.Imaging.Bitmap(Path.Combine(Program.Output, names[i] + ".png")), Stretch = Stretch.Uniform, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top });
            Grid.SetColumn(scene, i); Grid.SetRow(scene, 1); grid.Children.Add(scene);
        }
        var window = new Window { Title = "Overlay contrast check — synthetic bright and dark background", Width = 1480, Height = 520, Background = Brush.Parse("#121B18"), Content = grid, ShowActivated = false };
        window.Show(owner); await SettleOverlay(); Program.Snapshot(window, "overlay-contrast"); window.Close();
    }
}
