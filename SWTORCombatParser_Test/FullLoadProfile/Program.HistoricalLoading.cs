using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.Phases;
using SWTORCombatParser.ViewModels;
using SWTORCombatParser.ViewModels.Combat_Monitoring;
using SWTORCombatParser.ViewModels.DataGrid;

public sealed partial class FullLoadApp
{
    private static async Task RunHistoricalLoadingChecks(Window window, MainWindowViewModel main, CombatMonitorViewModel monitor)
    {
        var checks = new List<string>();
        var observations = new List<object>();
        var loadField = typeof(PastCombat).GetField("_loadCombat", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var loaders = new Dictionary<PastCombat, Func<string, Combat>>();
        var gates = new List<ManualResetEventSlim>();
        var rows = monitor.PastEncounters.SelectMany(e => e.EncounterCombats).Where(r => r.IsBossCombat && !r.IsCurrentCombat).ToArray();
        var original = rows.Single(r => r.IsSelected);
        var candidates = rows.Where(r => !r.IsSelected).OrderByDescending(CacheBytes).Take(4).ToArray();
        var cache = typeof(Combat).Assembly.GetType("SWTORCombatParser.Model.CombatCaching.CombatDiskCacheManager")!;
        var clearCache = cache.GetMethod("ClearRecentCombats", BindingFlags.NonPublic | BindingFlags.Static)!;
        var grid = (DataGridViewModel)typeof(MainWindowViewModel).GetField("_dataGridViewModel", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(main)!;
        var dataReadyEvent = typeof(CombatMonitorViewModel).GetEvent("HistoricalCombatDataReady", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var stagedData = new List<(DateTime Start, bool Visible, bool PhasesAlreadyPublished)>();
        Action<Combat> onData = combat => stagedData.Add((combat.StartTime,
            ReferenceEquals(main.CurrentlyDisplayedCombat, combat) && grid.PartyMembers.Any(p => !p.IsTotalsRow), PhaseManager.IsDisplayingCombat(combat)));
        dataReadyEvent.GetAddMethod(true)!.Invoke(monitor, new object[] { onData });
        void Check(bool condition, string text)
        {
            if (!condition) throw new InvalidOperationException(text);
            checks.Add(text);
        }
        async Task Settle()
        {
            await Task.Delay(60);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        }
        async Task Until(Func<bool> ready, string text)
        {
            var clock = Stopwatch.StartNew();
            while (!ready())
            {
                if (clock.Elapsed > TimeSpan.FromSeconds(30)) throw new TimeoutException(text);
                await Settle();
            }
            await Settle();
        }
        async Task Ready(PastCombat row)
        {
            await Until(() => monitor.HistoricalSelection is { Stage: HistoricalCombatSelectionStage.Ready } selection &&
                selection.Row == row && main.UnfilteredDisplayedCombat?.StartTime == row.CombatStartTime, "Ready: " + row.CombatLabel);
        }
        void Cold(PastCombat row)
        {
            row.UnloadCombat(); clearCache.Invoke(null, null);
            loaders.TryAdd(row, (Func<string, Combat>)loadField.GetValue(row)!);
        }
        void Snapshot(string name)
        {
            using var bitmap = new RenderTargetBitmap(PixelSize.FromSize(window.Bounds.Size, 1), new Vector(96, 96));
            bitmap.Render(window); bitmap.Save(Path.Combine(Program.OutputDirectory, name + ".png"));
        }
        try
        {
            main.SelectedTabIndex = 0; await Settle();
            var cold = candidates[0]; Cold(cold);
            var gate = new ManualResetEventSlim(); gates.Add(gate);
            var entered = false;
            loadField.SetValue(cold, new Func<string, Combat>(path =>
            {
                Volatile.Write(ref entered, true);
                if (!gate.Wait(TimeSpan.FromSeconds(25))) throw new TimeoutException("UI gate did not release.");
                return loaders[cold](path);
            }));
            var watch = Stopwatch.StartNew();
            cold.SelectionToggle();
            Check(monitor.HistoricalSelection is { Stage: HistoricalCombatSelectionStage.Opening, ShowProgress: false } pending && pending.Row == cold,
                "Selecting a cold fight publishes its context synchronously without flashing a spinner");
            Check(monitor.HistoricalSelection!.Title.Contains(cold.CombatLabel) && monitor.HistoricalSelection.Details.Contains(cold.CombatStartTime.ToString("g")),
                "Boss, start time, difficulty and duration are available before cache reading finishes");
            await Until(() => Volatile.Read(ref entered) && monitor.HistoricalSelection?.ShowProgress == true, "Subtle delayed progress");
            var progress = window.GetVisualDescendants().OfType<ProgressBar>().Single(c => c.Name == "HistoricalCombatProgress");
            Snapshot("historical-opening");
            Check(progress.IsEffectivelyVisible && progress.Bounds.Height <= 3 && !monitor.CanShowCombatPhases,
                $"A delayed two-pixel progress indicator appears while stale phase controls are hidden (visible={progress.IsEffectivelyVisible}, local={progress.IsVisible}, size={progress.Bounds}, phases={monitor.CanShowCombatPhases})");
            gate.Set();
            await Ready(cold);
            observations.Add(new { Case = "Cold with intentional gate", Milliseconds = watch.Elapsed.TotalMilliseconds });
            Check(stagedData.Any(item => item.Start == cold.CombatStartTime && item.Visible && !item.PhasesAlreadyPublished),
                "The active Raid Data view is populated before independent phase reconstruction is published");
            Check(!monitor.HistoricalSelection!.ShowProgress && monitor.CanShowCombatPhases && !monitor.HistoricalSelection.HasError,
                "Completing selection clears the loading state and restores ready phases");
            loadField.SetValue(cold, loaders[cold]);
            Snapshot("historical-ready");

            var progressWasShown = false;
            PropertyChangedEventHandler observeWarm = (_, args) =>
            {
                if (args.PropertyName == nameof(monitor.HistoricalSelection)) progressWasShown |= monitor.HistoricalSelection?.ShowProgress == true;
            };
            monitor.PropertyChanged += observeWarm;
            watch.Restart(); cold.SelectCombat(); await Ready(cold);
            var warmMs = watch.Elapsed.TotalMilliseconds;
            await Task.Delay(200); monitor.PropertyChanged -= observeWarm;
            observations.Add(new { Case = "Warm resident selection", Milliseconds = warmMs, IndicatorShown = progressWasShown });
            Check(!monitor.HistoricalSelection!.ShowProgress && (warmMs >= 150 || !progressWasShown),
                "Fast warm selections do not flash a late loading indicator");

            var slow = candidates[1]; var final = candidates[2]; Cold(slow); Cold(final);
            var staleGate = new ManualResetEventSlim(); gates.Add(staleGate);
            var staleEntered = false;
            loadField.SetValue(slow, new Func<string, Combat>(path =>
            {
                Volatile.Write(ref staleEntered, true);
                if (!staleGate.Wait(TimeSpan.FromSeconds(25))) throw new TimeoutException("Stale gate did not release.");
                return loaders[slow](path);
            }));
            slow.SelectionToggle(); await Until(() => Volatile.Read(ref staleEntered), "Slow selection starts");
            final.SelectionToggle(); await Ready(final);
            staleGate.Set(); await Task.Delay(900); await Settle();
            Check(monitor.HistoricalSelection?.Row == final && main.UnfilteredDisplayedCombat.StartTime == final.CombatStartTime,
                "A slower superseded combat cannot overwrite the latest selection or its status");
            loadField.SetValue(slow, loaders[slow]);

            var failed = candidates[3]; Cold(failed);
            loadField.SetValue(failed, new Func<string, Combat>(_ => throw new IOException("Isolated missing-cache probe")));
            var desktop = (IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
            var windowCount = desktop.Windows.Count;
            failed.SelectionToggle();
            await Until(() => monitor.HistoricalSelection?.Stage == HistoricalCombatSelectionStage.Unavailable, "Inline unavailable state");
            Check(monitor.HistoricalSelection is { HasError: true, ShowProgress: false } && desktop.Windows.Count == windowCount,
                "A cache-read failure is shown inline without a modal dialog or stuck loading indicator");
            Check(main.UnfilteredDisplayedCombat.StartTime == final.CombatStartTime,
                "Unavailable data does not replace the last valid combat with an empty placeholder");
            Snapshot("historical-unavailable");
            loadField.SetValue(failed, loaders[failed]); failed.SelectCombat(); await Ready(failed);
            Check(!monitor.HistoricalSelection!.HasError, "Reselecting a repaired cache retries successfully and clears its error");

            original.SelectionToggle(); await Ready(original);
            File.WriteAllText(Path.Combine(Program.OutputDirectory, "historical-loading.json"), System.Text.Json.JsonSerializer.Serialize(new
            {
                Status = "Passed", Observations = observations, StagedCombatCount = stagedData.Count, Checks = checks,
                Note = "Cold and superseded reads use test-only gates; those durations are not real-world speed benchmarks."
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
        finally
        {
            foreach (var gate in gates) gate.Set();
            foreach (var (row, loader) in loaders) loadField.SetValue(row, loader);
            dataReadyEvent.GetRemoveMethod(true)!.Invoke(monitor, new object[] { onData });
        }
    }
}
