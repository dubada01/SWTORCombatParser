using System.Reflection;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.Phases;
using SWTORCombatParser.ViewModels;
using SWTORCombatParser.ViewModels.BattleReview;
using SWTORCombatParser.ViewModels.Combat_Monitoring;
using SWTORCombatParser.ViewModels.DataGrid;
using SWTORCombatParser.ViewModels.Overviews;

public sealed partial class FullLoadApp
{
    private static async Task RunReviewTabChecks(Window window, MainWindowViewModel vm, CombatMonitorViewModel monitor)
    {
        var checks = new List<string>();
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            checks.Add(message);
        }
        object? Field(object owner, string name) => owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner);
        var tabs = window.GetVisualDescendants().OfType<TabStrip>().Single();
        var review = (BattleReviewViewModel)Field(vm, "_reviewViewModel")!;
        var details = (TableViewModel)Field(vm, "_tableViewModel")!;
        var grid = (DataGridViewModel)Field(vm, "_dataGridViewModel")!;
        var plot = Field(vm, "_plotViewModel")!;
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        async Task Tab(int index)
        {
            await Dispatcher.UIThread.InvokeAsync(() => tabs.SelectedIndex = index);
            await WaitForMeasuredWorkAndIdle(timeout.Token);
            Check(vm.SelectedTabIndex == index && vm.SelectedTab.TabContent == vm.ContentTabs[index].TabContent,
                "Actual tab control opens " + vm.ContentTabs[index].HeaderText);
        }

        var original = monitor.PastEncounters.SelectMany(e => e.EncounterCombats).Single(c => c.IsSelected);
        var other = monitor.PastEncounters.SelectMany(e => e.EncounterCombats)
            .Where(c => c.IsBossCombat && !c.IsSelected && !c.IsCurrentCombat).OrderByDescending(CacheBytes).First();
        var returnTimings = new List<double>();
        await SelectAndSettle(other);
        foreach (var row in new[] { original, other, original, other, original })
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            await SelectAndSettle(row);
            returnTimings.Add(clock.Elapsed.TotalMilliseconds);
        }
        await Tab(1);
        Check(ReferenceEquals(Field(plot, "_currentCombat"), vm.CurrentlyDisplayedCombat) && CapturePlot(vm).ScatterPointCount > 0,
            "Plot builds the selected fight on first opening");
        await Tab(2);
        Check(ReferenceEquals(details.DamageVM.SelectedCombat, vm.CurrentlyDisplayedCombat), "Details builds the selected fight on first opening");
        await Tab(3);
        Check(review.EventHistory.LogsToDisplay.Count > 0 && ReferenceEquals(Field(review, "_combat"), vm.CurrentlyDisplayedCombat),
            "Log Review loads actual events on first opening");
        var rows = review.EventHistory.LogsToDisplay;
        await Tab(0); await Tab(3);
        Check(ReferenceEquals(rows, review.EventHistory.LogsToDisplay), "Reopening an unchanged tab retains its results");
        rows = null;
        await Tab(0); await SelectAndSettle(other);
        Check(Field(plot, "_currentCombat") == null && details.DamageVM.SelectedCombat == null &&
              review.EventHistory.LogsToDisplay.Count == 0, "Selecting in Raid Data releases hidden pages without rebuilding them");
        await Tab(3);
        Check(ReferenceEquals(Field(review, "_combat"), vm.CurrentlyDisplayedCombat), "Log Review opens the new selection without stale events");
        await Tab(1); await SelectAndSettle(original);
        Check(ReferenceEquals(Field(plot, "_currentCombat"), vm.CurrentlyDisplayedCombat) && grid.PartyMembers.Count == 0,
            "Selecting in Plot updates Plot and releases hidden Raid Data rows");
        await Tab(0);
        Check(grid.PartyMembers.Any(p => !p.IsTotalsRow), "Raid Data repopulates when reopened");

        var phase = PhaseManager.ActivePhases.FirstOrDefault(p => p.PhaseEnd > p.PhaseStart);
        if (phase != null)
        {
            typeof(PhaseManager).GetMethod("TogglePhaseInstance", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { phase });
            await WaitForMeasuredWorkAndIdle(timeout.Token);
            var filtered = vm.CurrentlyDisplayedCombat;
            Check(!ReferenceEquals(filtered, vm.UnfilteredDisplayedCombat), "Phase selection creates a filtered view");
            foreach (var index in new[] { 1, 2, 3, 0 }) await Tab(index);
            Check(ReferenceEquals(filtered, vm.CurrentlyDisplayedCombat) && PhaseManager.SelectedPhases.Count == 1,
                "Switching every tab preserves the active phase filter");
            typeof(PhaseManager).GetMethod("TogglePhaseInstance", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { phase });
            await WaitForMeasuredWorkAndIdle(timeout.Token);
            Check(ReferenceEquals(vm.CurrentlyDisplayedCombat, vm.UnfilteredDisplayedCombat), "Clearing the phase restores the full fight");
        }
        else throw new InvalidOperationException("Review-tab fixture must contain a completed phase.");
        foreach (var index in new[] { 1, 3, 0 })
        {
            await Tab(index);
            await Dispatcher.UIThread.InvokeAsync(() => typeof(MainWindowViewModel)
                .GetMethod("UpdateCombat", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(vm, new object[] { vm.CurrentlyDisplayedCombat }));
            await WaitForMeasuredWorkAndIdle(timeout.Token);
        }
        await Tab(1);
        Check(ReferenceEquals(Field(plot, "_currentCombat"), vm.CurrentlyDisplayedCombat),
            "Live-update path and reopening keep Plot's current combat attached");
        await Tab(3);
        Check(ReferenceEquals(Field(review, "_combat"), vm.CurrentlyDisplayedCombat) && review.EventHistory.LogsToDisplay.Count > 0,
            "Live-update path and reopening keep Log Review populated");
        await Tab(0);
        await RunAnalysisChecks(window, vm, details, (SWTORCombatParser.ViewModels.Home_View_Models.PlotViewModel)plot, grid, Tab, Check);
        File.WriteAllText(Path.Combine(Program.OutputDirectory, "review-tabs.json"),
            JsonSerializer.Serialize(new { Status = "Passed", RecentCombatReturnMs = returnTimings,
                TimingNote = "Includes the harness's two 50 ms idle checks after all measured work finishes.", Checks = checks },
                new JsonSerializerOptions { WriteIndented = true }));
    }
}
