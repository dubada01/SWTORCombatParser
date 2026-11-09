using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Phases;
using SWTORCombatParser.Model.Progression;
using SWTORCombatParser.ViewModels;
using SWTORCombatParser.ViewModels.Combat_Monitoring;
using SWTORCombatParser.ViewModels.Progression;
using SWTORCombatParser.Views.Progression;

public sealed partial class FullLoadApp
{
    private static async Task RunProgressionReviewChecks(Window mainWindow, MainWindowViewModel main, CombatMonitorViewModel monitor)
    {
        var checks = new List<string>();
        var timings = new Dictionary<string, double>();
        void Check(bool condition, string text)
        {
            if (!condition) throw new InvalidOperationException(text);
            checks.Add(text);
        }
        async Task Settle()
        {
            await Task.Delay(100);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        }
        async Task Until(Func<bool> ready, string description)
        {
            var timeout = Stopwatch.StartNew();
            while (!ready())
            {
                if (timeout.Elapsed > TimeSpan.FromMinutes(2)) throw new TimeoutException(description);
                await Settle();
            }
            await Settle();
        }
        var desktop = (IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
        ProgressionReviewWindow ReviewWindow() => desktop.Windows.OfType<ProgressionReviewWindow>().Single(w => w.IsVisible);
        T Control<T>(Window window, string name) where T : Control => window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
        void Snapshot(Window window, string name)
        {
            using var bitmap = new RenderTargetBitmap(PixelSize.FromSize(window.Bounds.Size, 1), new Vector(96, 96));
            bitmap.Render(window);
            bitmap.Save(Path.Combine(Program.OutputDirectory, name + ".png"));
        }
        void OpenMenu() => ((ICommand)main.GetType().GetProperty("OpenProgressionReviewCommand")!.GetValue(main)!).Execute(null);

        var phase = PhaseManager.ActivePhases.FirstOrDefault(p => p.PhaseEnd > p.PhaseStart);
        Check(phase != null, "The real raid fixture contains a completed phase");
        typeof(PhaseManager).GetMethod("TogglePhaseInstance", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, new object[] { phase! });
        await Settle();
        Check(PhaseManager.SelectedPhases.Count > 0, "A main-window phase filter is active before progression testing");
        var mainCombat = main.CurrentlyDisplayedCombat;
        var selectedPhases = PhaseManager.SelectedPhases.ToArray();
        var allRows = monitor.PastEncounters.SelectMany(e => e.EncounterCombats).Where(r => r.IsBossCombat && !r.IsCurrentCombat).ToArray();
        var residentCount = allRows.Count(r => r.Combat != null);
        var requested = allRows.Where(r => !r.IsSelected).OrderByDescending(r => r.CombatStartTime).First();
        var watch = Stopwatch.StartNew();
        ((ICommand)requested.DisplayWipeReviewCommand).Execute(null);
        timings["OpenCommandMs"] = watch.Elapsed.TotalMilliseconds;
        var window = ReviewWindow();
        var vm = (ProgressionReviewViewModel)window.DataContext!;
        Check(window.IsVisible, "The actual wipe-review action opens progression immediately");
        await Until(() => vm.SelectedPull?.Summary.State == ProgressionDataState.Available, "Requested pull summary");
        timings["RequestedSummaryReadyMs"] = watch.Elapsed.TotalMilliseconds;
        Check(vm.SelectedPull!.Summary.Metadata.Start == requested.CombatStartTime, "Wipe Review focuses the requested pull");
        await Until(() => !vm.IsIndexing, "All raid summaries indexed");
        timings["RaidIndexReadyMs"] = watch.Elapsed.TotalMilliseconds;
        Check(allRows.Count(r => r.Combat != null) == residentCount, "Progression indexing does not hydrate additional full combats");
        Check(ReferenceEquals(main.CurrentlyDisplayedCombat, mainCombat) && PhaseManager.SelectedPhases.SequenceEqual(selectedPhases),
            "Opening progression preserves the main combat and phase selection");

        var boss = Control<ComboBox>(window, "BossSelector");
        var pulls = Control<DataGrid>(window, "PullGrid");
        Check(Equals(boss.SelectedItem, vm.SelectedBoss) && ReferenceEquals(pulls.SelectedItem, vm.SelectedPull),
            "The initial visible boss and pull selections match the requested attempt");
        foreach (var item in boss.Items.Cast<string>().ToArray())
        {
            var refreshing = vm.GetType().GetField("_refreshing", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(vm);
            var initialSelection = $"VM={vm.SelectedBoss}, UI={boss.SelectedItem}, context={boss.DataContext?.GetType().Name}, refreshing={refreshing}";
            boss.IsDropDownOpen = true; await Settle(); boss.SelectedItem = item; await Settle();
            var openSelection = $"VM={vm.SelectedBoss}, UI={boss.SelectedItem}";
            boss.IsDropDownOpen = false; await Settle();
            Check(vm.SelectedBoss == item && (item == "All bosses" || vm.Pulls.All(p => p.Summary.Metadata.Boss == item)),
                $"Boss dropdown filters attempts: {item} (initial: {initialSelection}; open: {openSelection}; closed: VM={vm.SelectedBoss}, UI={boss.SelectedItem})");
            Check(ReferenceEquals(pulls.ItemsSource, vm.Pulls), "The pull grid remains bound after switching to " + item);
        }
        boss.SelectedItem = "All bosses"; await Settle();
        Check(vm.Pulls.Count > 1, "The operation view includes multiple boss attempts");
        foreach (var columnName in new[] { "Duration", "First death" })
        {
            pulls.Columns.Single(c => c.Header?.ToString() == columnName).Sort(System.ComponentModel.ListSortDirection.Ascending);
            await Settle();
            var rendered = pulls.GetVisualDescendants().OfType<DataGridRow>().Where(r => r.IsEffectivelyVisible && r.Bounds.Height > 0)
                .OrderBy(r => r.TranslatePoint(new Point(), pulls)!.Value.Y).Select(r => (ProgressionPullRow)r.DataContext!).ToArray();
            var numbers = columnName == "Duration" ? rendered.Select(r => r.Summary.Metadata.DurationSeconds).ToArray()
                : rendered.Where(r => r.FirstDeathSeconds != null).Select(r => r.FirstDeathSeconds!.Value).ToArray();
            Check(numbers.Length > 1 && numbers.SequenceEqual(numbers.Order()), "The " + columnName + " column sorts numeric elapsed times");
        }
        pulls.Columns.Single(c => c.Header?.ToString() == "Started").Sort(System.ComponentModel.ListSortDirection.Ascending);
        await Settle();
        var chosen = vm.Pulls.Where(p => p.Summary.Deaths.Count > 0).OrderByDescending(p => p.Summary.Deaths.Count).First();
        File.WriteAllText(Path.Combine(Program.OutputDirectory, "progression-attempt.json"),
            JsonSerializer.Serialize(chosen.Summary, new JsonSerializerOptions { WriteIndented = true }));
        pulls.SelectedItem = chosen; await Settle();
        Check(ReferenceEquals(vm.SelectedPull, chosen) && vm.Deaths.Count == chosen.Summary.Deaths.Count,
            "Selecting a pull updates its ordered death recap");
        var deathGrid = Control<DataGrid>(window, "DeathGrid");
        foreach (var death in vm.Deaths.Take(3).ToArray())
        {
            deathGrid.SelectedItem = death; await Settle();
            Check(ReferenceEquals(vm.SelectedDeath, death) && vm.Leadup.Count == death.Death.Leadup.Count,
                "Selecting a death updates the saved damage and healing lead-up for " + death.Death.Player);
            Check(death.Death.Leadup.All(e => e.Time <= death.Death.Time && (death.Death.Time - e.Time).TotalSeconds <= 10),
                "The lead-up contains only events in the ten seconds preceding death");
        }

        var source = allRows.Single(r => r.CombatStartTime == chosen.Summary.Metadata.Start);
        var logs = await Task.Run(() => (List<ParsedLogEntry>)typeof(Combat).Assembly
            .GetType("SWTORCombatParser.Model.CombatCaching.CombatDiskCacheManager")!
            .GetMethod("LoadLogs", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, new object[] { source.CombatCachePath })!);
        var actualDeaths = logs.Where(l => l.Effect?.EffectId == _7_0LogParsing.DeathCombatId &&
                l.TargetInfo?.Entity is { IsCharacter: true, IsCompanion: false } && !string.IsNullOrWhiteSpace(l.Target.Name))
            .Select(l => (l.Target.Id, l.Target.Name, l.TimeStamp)).Distinct().OrderBy(d => d.TimeStamp).ToArray();
        Check(chosen.Summary.Deaths.Count == actualDeaths.Length && chosen.Summary.Deaths.Select(d => (d.Player, d.Time))
            .OrderBy(d => d.Time).SequenceEqual(actualDeaths.Select(d => (d.Name, d.TimeStamp))),
            "Saved death identities and timestamps match the original 16-player combat cache");
        foreach (var death in chosen.Summary.Deaths.Where(d => d.LastHit != null))
        {
            var hit = death.LastHit!;
            Check(logs.Any(l => l.TargetInfo?.Entity?.Name == death.Player && l.TimeStamp == hit.Time &&
                l.AbilityId == hit.AbilityId && l.Effect?.EffectId == _7_0LogParsing._damageEffectId) &&
                hit.Time <= death.Time && (death.Time - hit.Time).TotalSeconds <= 5,
                "Last-hit attribution is backed by a recent incoming damage event for " + death.Player);
        }
        logs = null!;
        window.Width = 1280; window.Height = 860; await Settle(); Snapshot(window, "progression-operation");
        boss.SelectedItem = chosen.Summary.Metadata.Boss; await Settle();
        pulls.SelectedItem = vm.Pulls.Single(p => p.Summary.Metadata.Id == chosen.Summary.Metadata.Id); await Settle();
        Snapshot(window, "progression-boss");

        var detailedButton = Control<Button>(window, "DetailedReviewButton");
        detailedButton.Command!.Execute(detailedButton.CommandParameter);
        await Until(() => desktop.Windows.OfType<SWTORCombatParser.Views.Death_Review.RaidwideBattleReviewWindow>().Any(w => w.IsVisible),
            "Detailed pull analysis");
        var detailed = desktop.Windows.OfType<SWTORCombatParser.Views.Death_Review.RaidwideBattleReviewWindow>().Single(w => w.IsVisible);
        var detailVm = (SWTORCombatParser.ViewModels.Death_Review.RaidwideBattleReviewViewModel)detailed.DataContext!;
        var detailTabs = detailed.GetVisualDescendants().OfType<TabStrip>().Single();
        foreach (var tab in new[] { 0, 1, 2, 0 })
        {
            detailTabs.SelectedIndex = tab;
            await Settle();
            Check(detailVm.SelectedTabIndex == tab && detailVm.CurrentReviewContent.IsEffectivelyVisible,
                "Detailed analysis opens and reopens tab " + tab);
        }
        detailed.Close(); await Settle();
        Check(window.IsVisible && vm.SelectedPull?.Summary.Metadata.Id == chosen.Summary.Metadata.Id,
            "Closing detailed analysis returns to the same progression pull");

        var teamBox = Control<TextBox>(window, "TeamNameBox");
        teamBox.Text = "QA Night Team";
        await Settle();
        var saveTeam = Control<Button>(window, "SaveTeamButton");
        Check(saveTeam.Command?.CanExecute(saveTeam.CommandParameter) == true, "A named team can be assigned through the raid controls");
        saveTeam.Command!.Execute(saveTeam.CommandParameter); await Settle();
        var scope = Control<ComboBox>(window, "ProgressionScope");
        scope.IsDropDownOpen = true; await Settle(); scope.SelectedItem = ProgressionScope.SavedHistory; scope.IsDropDownOpen = false; await Settle();
        var teams = Control<ComboBox>(window, "HistoryTeamSelector");
        await Until(() => teams.Items.Cast<string>().Contains("QA Night Team"), "Saved team assignment");
        Check(teams.Items.Cast<string>().Contains("QA Night Team"), "Assigned team is available in saved history");
        teams.SelectedItem = "QA Night Team"; await Settle();
        Check(vm.Pulls.Count > 0 && vm.SelectedTeam == "QA Night Team", "Named team history retains its saved pulls");
        // Detached synthetic nights exercise cross-session filtering through the real store and UI.
        var service = main.GetType().GetField("_progressionReviewService", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(main)!;
        var store = service.GetType().GetField("_store", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(service)!;
        ProgressionPullSummary AddNight(char key, int days, string team, ProgressionCohort nightCohort)
        {
            var raidId = new string(key, 64);
            var delta = TimeSpan.FromDays(days);
            var summary = chosen.Summary with
            {
                Metadata = chosen.Summary.Metadata with { Id = raidId + "-pull", RaidId = raidId, Cohort = nightCohort,
                    Start = chosen.Summary.Metadata.Start + delta, End = chosen.Summary.Metadata.End + delta },
                Deaths = chosen.Summary.Deaths.Select(d => d with { Time = d.Time + delta,
                    LastHit = d.LastHit is { } hit ? hit with { Time = hit.Time + delta } : null,
                    Leadup = d.Leadup.Select(e => e with { Time = e.Time + delta }).ToArray() }).ToArray()
            };
            var raid = new ProgressionRaidChoice(raidId, nightCohort.Operation, summary.Metadata.Start, false);
            store.GetType().GetMethod("Upsert", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(store, new object[] { raid, new[] { summary } });
            store.GetType().GetMethod("AssignTeam", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(store, new object[] { raid, team });
            return summary;
        }
        var sameNight = await Task.Run(() => AddNight('B', 1, "QA Night Team", chosen.Summary.Metadata.Cohort));
        var otherMode = await Task.Run(() => AddNight('C', 2, "QA Night Team", chosen.Summary.Metadata.Cohort with { Difficulty = "Veteran", GroupSize = "8-player" }));
        var otherTeam = await Task.Run(() => AddNight('D', 3, "QA Other Team", chosen.Summary.Metadata.Cohort));
        var savedSnapshot = store.GetType().GetMethod("Read", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(store, null);
        service.GetType().GetMethod("SetSaved", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(service, new[] { savedSnapshot });
        scope.SelectedItem = ProgressionScope.CurrentRaid; scope.SelectedItem = ProgressionScope.SavedHistory;
        boss.SelectedItem = chosen.Summary.Metadata.Boss; await Settle();
        Check(vm.Pulls.Any(p => p.Summary.Metadata.Id == chosen.Summary.Metadata.Id) && vm.Pulls.Any(p => p.Summary.Metadata.Id == sameNight.Metadata.Id),
            "The same team's boss progression includes pulls from multiple raid nights");
        Check(vm.Pulls.All(p => p.Summary.Metadata.Id != otherMode.Metadata.Id && p.Summary.Metadata.Id != otherTeam.Metadata.Id),
            "A team's progression excludes another team's pulls and different difficulty/group size");
        var cohort = Control<ComboBox>(window, "CohortSelector");
        foreach (var item in cohort.Items.Cast<ProgressionCohort>().ToArray())
        {
            cohort.IsDropDownOpen = true; await Settle(); cohort.SelectedItem = item; cohort.IsDropDownOpen = false; await Settle();
            Check(vm.Pulls.All(p => p.Summary.Metadata.Cohort == item), "History separates operation, difficulty and group size: " + item.Label);
        }
        cohort.SelectedItem = chosen.Summary.Metadata.Cohort; await Settle();
        for (var i = 0; i < 6; i++)
            scope.SelectedItem = i % 2 == 0 ? ProgressionScope.CurrentRaid : ProgressionScope.SavedHistory;
        await Settle();
        Check(vm.Scope == ProgressionScope.SavedHistory && vm.Pulls.Count > 0, "Rapid scope switching settles on the final selection");
        Snapshot(window, "progression-history");
        window.Width = window.MinWidth; window.Height = window.MinHeight; await Settle();
        Check(pulls.Bounds.Width > 250 && deathGrid.Bounds.Height > 70, "Attempt and death lists remain usable at the supported minimum window size");
        Snapshot(window, "progression-minimum");
        var savedCount = vm.Pulls.Count;
        window.Close(); await Settle();
        watch.Restart();
        OpenMenu(); await Settle(); window = ReviewWindow(); vm = (ProgressionReviewViewModel)window.DataContext!;
        await Until(() => !vm.IsIndexing && vm.SelectedPull != null, "Reopened progression view");
        timings["ReopenMs"] = watch.Elapsed.TotalMilliseconds;
        Check(window.IsVisible && vm.Pulls.Count > 0, "The Sessions menu reopens a populated progression window");
        Check(ReferenceEquals(main.CurrentlyDisplayedCombat, mainCombat) && PhaseManager.SelectedPhases.SequenceEqual(selectedPhases),
            "All progression interactions leave the main combat and phase selection intact");
        window.Close(); await Settle();
        monitor.PastEncounters.Clear(); await Settle();
        OpenMenu(); await Settle(); window = ReviewWindow(); vm = (ProgressionReviewViewModel)window.DataContext!;
        Control<ComboBox>(window, "ProgressionScope").SelectedItem = ProgressionScope.SavedHistory;
        Control<ComboBox>(window, "HistoryTeamSelector").SelectedItem = "QA Night Team";
        await Until(() => !vm.IsIndexing && vm.Pulls.Count > 0, "Saved history without loaded source logs");
        Control<ComboBox>(window, "BossSelector").SelectedItem = "All bosses";
        await Settle();
        var archived = vm.Pulls.First(p => p.Summary.Metadata.Id == chosen.Summary.Metadata.Id);
        Control<DataGrid>(window, "PullGrid").SelectedItem = archived; await Settle();
        Check(vm.Deaths.Count == archived.Summary.Deaths.Count && vm.Deaths.Count > 0,
            "Saved death recaps remain usable with no source raid loaded");
        Control<DataGrid>(window, "DeathGrid").SelectedItem = vm.Deaths.First(d => d.Death.Leadup.Count > 0); await Settle();
        Check(vm.Leadup.Count > 0, "Saved damage and healing lead-ups remain available without the combat cache");
        Check(!((ICommand)vm.OpenDetailedReviewCommand).CanExecute(null),
            "Full combat analysis is disabled when its source raid is no longer loaded");
        Snapshot(window, "progression-archived");
        window.Close(); await Settle();
        File.WriteAllText(Path.Combine(Program.OutputDirectory, "progression-review.json"), JsonSerializer.Serialize(new
        {
            Status = "Passed", TimingsMs = timings, SavedHistoryPulls = savedCount, Checks = checks
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
