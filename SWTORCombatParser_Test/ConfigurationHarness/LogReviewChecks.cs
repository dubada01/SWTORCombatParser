using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.LogReview;
using SWTORCombatParser.ViewModels.BattleReview;
using SWTORCombatParser.Views.Battle_Review;

public sealed partial class ConfigurationHarnessApp
{
    private static Combat ReviewCombat(int count)
    {
        var combat = new Combat { StartTime = new DateTime(2026, 11, 8, 20, 0, 0), EndTime = new DateTime(2026, 11, 8, 20, 8, 0) };
        var players = Enumerable.Range(1, 16).Select(i => new Entity { Name = "Raider " + i.ToString("00"), Id = i, LogId = i, IsCharacter = true }).ToArray();
        foreach (var player in players) combat.CharacterParticipants[player.LogId] = player;
        var boss = new Entity { Name = "Dread Master Brontes", Id = 100, LogId = 100, IsBoss = true };
        combat.Targets[100] = boss;
        for (var i = 0; i < count; i++)
        {
            var kind = i % 10;
            var source = kind < 3 || kind == 9 ? boss : players[i % players.Length];
            var target = kind < 5 || kind == 9 ? players[(i + 1) % players.Length] : boss;
            var effectId = kind == 9 ? _7_0LogParsing.DeathCombatId : kind == 8 ? _7_0LogParsing.AbilityActivateId : kind is 3 or 4 ? _7_0LogParsing._healEffectId : _7_0LogParsing._damageEffectId;
            var ability = kind switch { 0 => "Arcing Assault", 1 => "Force Lightning", 2 => "Force Slam", 3 => "Roaming Mend", 4 => "Resurgence", 5 => "Ambush", 6 => "Rail Shot", 7 => "Annihilate", 8 => "Force Barrier", _ => "Defeated" };
            var amount = kind >= 8 ? 0 : 1200 + (i * 7919 % 90000);
            combat.AllLogs[i] = new ParsedLogEntry {
                TimeStamp = combat.StartTime.AddSeconds(i * 480d / count), Ability = ability, AbilityId = (ulong)(1000 + kind),
                SourceInfo = new EntityInfo { Entity = source, MaxHP = source.IsBoss ? 12000000u : 420000u, CurrentHP = 250000 },
                TargetInfo = new EntityInfo { Entity = target, MaxHP = target.IsBoss ? 12000000u : 420000u, CurrentHP = kind == 9 ? 0u : 150000u },
                Effect = new Effect { EffectId = effectId, EffectType = kind >= 8 ? EffectType.Event : EffectType.Apply,
                    EffectName = kind == 9 ? "Death" : kind == 8 ? "AbilityActivate" : kind is 3 or 4 ? "Heal" : "Damage" },
                Value = new Value { DblValue = amount * 1.2, EffectiveDblValue = amount, MitigatedDblValue = amount * 0.2,
                    DisplayValue = amount == 0 ? "" : amount.ToString(), ValueType = kind is 3 or 4 ? DamageType.heal : kind >= 8 ? DamageType.none : DamageType.energy,
                    WasCrit = i % 7 == 0 }, LogLineNumber = i, LogName = "synthetic-review.txt"
            };
        }
        return combat;
    }

    private static async Task RunLogReviewChecks(Window main)
    {
        Exception? failure = null;
        var timings = new Dictionary<string, double>();
        try
        {
            var combat = await Task.Run(() => ReviewCombat(200000));
            var iconsReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            SWTORCombatParser.Utilities.IconGetter._abilityToIconDict[1001] = "log-review-test-icon";
            SWTORCombatParser.Utilities.IconGetter.SetIconsReadyTask(iconsReady.Task);
            var model = new BattleReviewViewModel();
            var view = new BattleReviewView(model);
            main.Title = "Orbs Log Review test"; main.Width = 1280; main.Height = 740; main.Content = view;
            var grid = model.EventViewContent.FindControl<DataGrid>("DataArea")!;
            var clock = Stopwatch.StartNew();
            var pulse = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            var previousPulse = clock.Elapsed.TotalMilliseconds; var maxPulse = 0d;
            pulse.Tick += (_, _) => { var now = clock.Elapsed.TotalMilliseconds; maxPulse = Math.Max(maxPulse, now - previousPulse); previousPulse = now; };
            pulse.Start();
            var watch = Stopwatch.StartNew();
            model.CombatSelected(combat); await model.LastUpdate.WaitAsync(TimeSpan.FromSeconds(5));
            timings["Initial200kMs"] = watch.Elapsed.TotalMilliseconds;
            await SettleOverlay();
            timings["InitialMaterializedRows"] = model.EventHistory.MaterializedRowCount;
            Program.Check(model.EventHistory.LogsToDisplay.Count == 200000, "All 200,000 events are available without a row limit");
            Program.Check(model.EventHistory.MaterializedRowCount < 200, "Initial rendering materializes fewer than 200 rows for 200,000 events");
            Program.Check(grid.GetVisualDescendants().OfType<DataGridRow>().Count() < 100, "The actual table virtualizes row containers");
            Program.Check(!iconsReady.Task.IsCompleted, "Slow icon availability never delays the results");
            iconsReady.SetResult();
            SWTORCombatParser.Utilities.IconGetter.SetIconsReadyTask(Task.CompletedTask);
            Program.Snapshot(main, "log-review-large");
            if (Program.LogReviewInteractive)
            {
                pulse.Stop();
                main.Closed += (_, _) => Environment.Exit(0);
                return;
            }
            var search = view.FindControl<TextBox>("SearchBox")!;
            async Task Search(string text)
            {
                SWTORCombatParser.Utilities.LogLoadDiagnostics.Start(text);
                var timer = Stopwatch.StartNew();
                search.Text = text;
                search.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
                await model.LastUpdate;
                timings[text] = timer.Elapsed.TotalMilliseconds;
                timings["Materialized after " + text] = model.EventHistory.MaterializedRowCount;
                SWTORCombatParser.Utilities.LogLoadDiagnostics.WriteReport(text);
                File.Copy(Path.Combine(Program.Output, "profile.json"), Path.Combine(Program.Output, "search-profile-" + timings.Count + ".json"));
                await SettleOverlay();
            }
            await Search("lightning -type:healing");
            Program.Check(model.EventHistory.LogsToDisplay.Count == 20000 && model.EventHistory.LogsToDisplay[0].Ability == "Force Lightning", "Plain words and exclusions filter the real search binding");
            await Search("target:\"Raider 02\" type:damage value:>=10000 after:1:20 before:1:35");
            Program.Check(model.EventHistory.LogsToDisplay.Count > 0 && model.EventHistory.LogsToDisplay.All(r => r.Target == "Raider 02" && r.Kind == "Damage" && r.ElapsedSeconds >= 80 && r.ElapsedSeconds <= 95 && r.LogEntry!.Value.EffectiveDblValue >= 10000),
                "Quoted participants, kind, effective amount and inclusive time bounds combine correctly");
            await Search("ability:\"Force Lightning\" crit:true | type:death");
            Program.Check(model.EventHistory.LogsToDisplay.Count > 20000 && model.EventHistory.LogsToDisplay.All(r => r.Kind == "Death" || (r.Ability == "Force Lightning" && r.WasValueCrit)), "Alternative groups and critical-hit filters compose");
            await Search("ability:1001 after:120 before:121");
            Program.Check(model.EventHistory.LogsToDisplay.Count > 0 && model.EventHistory.LogsToDisplay.All(r => r.LogEntry!.AbilityId == 1001), "Exact ability IDs and numeric times work");
            grid.SelectedIndex = 0; await SettleOverlay();
            var selected = model.EventHistory.SelectedEntry!;
            Program.Check(selected.TargetDetail.Contains("HP") && selected.AmountDetail.Contains("Raw:") && selected.AmountDetail.Contains("Effective:") && selected.TimeDetail.Contains("line"), "Selected row exposes HP, raw/effective values and file location");
            Program.Snapshot(main, "log-review-selected");
            var oldSearch = model.LogFilter;
            model.ShowContextCommand.Execute().Subscribe(); await model.LastUpdate; await SettleOverlay();
            Program.Check(model.CanReturnToResults && model.EventHistory.LogsToDisplay.All(r => Math.Abs(r.ElapsedSeconds - selected.ElapsedSeconds) <= 5.001), "Surrounding events removes other filters and limits the view to plus/minus five seconds");
            Program.Check(grid.GetVisualDescendants().OfType<DataGridRow>().Any(r => r.IsEffectivelyVisible && r.Bounds.Height > 0 && ReferenceEquals(r.DataContext, selected)),
                "The selected event is actually visible in its surrounding context, even in a dense log");
            model.ReturnToResultsCommand.Execute().Subscribe(); await model.LastUpdate; await SettleOverlay();
            Program.Check(model.LogFilter == oldSearch && !model.CanReturnToResults && ReferenceEquals(model.EventHistory.SelectedEntry, selected), "Back to results restores filters and the selected event");
            var stable = model.EventHistory.LogsToDisplay;
            model.ApplySearch(); await model.LastUpdate;
            Program.Check(ReferenceEquals(stable, model.EventHistory.LogsToDisplay), "Unchanged results retain ItemsSource and row identities");
            await Search("ability:\"Force Lightning");
            Program.Check(model.EventHistory.HasSearchError && ReferenceEquals(stable, model.EventHistory.LogsToDisplay), "An unfinished quote shows an error and keeps the last valid results");
            view.FindControl<CheckBox>("RegexMode")!.IsChecked = true;
            await Search("[");
            Program.Check(model.EventHistory.HasSearchError && model.EventHistory.SearchError.Contains("regular expression"), "Invalid regex is visible instead of silently showing all events");
            Program.Snapshot(main, "log-review-invalid-search");
            await Search("Force (Lightning|Slam)");
            Program.Check(!model.EventHistory.HasSearchError && model.EventHistory.LogsToDisplay.Count == 40000, "Explicit regex mode finds alternatives");
            model.ClearFilters(); await model.LastUpdate; await SettleOverlay();
            var types = view.FindControl<ComboBox>("EventType")!;
            foreach (var type in model.AvailableDisplayTypes)
            {
                types.SelectedItem = type; await model.LastUpdate; await SettleOverlay();
                Program.Check(model.SelectedDisplayType == type && !model.EventHistory.HasSearchError, "Event-type dropdown selects " + type);
            }
            Program.Check(model.EventHistory.LogsToDisplay.Count == 20000 && model.EventHistory.LogsToDisplay[0].Kind == "Activation", "Abilities shows activations rather than all Event entries");
            model.ClearFilters(); await model.LastUpdate;
            var participant = model.AvailableEntities.Single(e => e.Entity.LogId == 2);
            var role = view.FindControl<ComboBox>("ParticipantRole")!;
            Program.Check(role.SelectedItem is ParticipantRole.Either, "Participant direction defaults to either source or target");
            participant.Selected = true; types.SelectedItem = DisplayType.Damage; role.SelectedItem = ParticipantRole.Target; await model.LastUpdate;
            Program.Check(!model.AvailableEntities[0].Selected && model.EventHistory.LogsToDisplay.Count > 0 && model.EventHistory.LogsToDisplay.All(r => r.LogEntry!.Target.LogId == 2), "Participant selection and incoming damage filter by target");
            var incomingCount = model.EventHistory.LogsToDisplay.Count;
            role.SelectedItem = ParticipantRole.Source; await model.LastUpdate;
            Program.Check(model.SelectedParticipantRole == ParticipantRole.Source && model.EventHistory.LogsToDisplay.Count > 0 && model.EventHistory.LogsToDisplay.All(r => r.LogEntry!.Source.LogId == 2), "Source direction composes with the Damage category");
            var outgoingCount = model.EventHistory.LogsToDisplay.Count;
            role.SelectedItem = ParticipantRole.Either; await model.LastUpdate;
            Program.Check(model.EventHistory.LogsToDisplay.Count == incomingCount + outgoingCount && model.EventHistory.LogsToDisplay.All(r => r.LogEntry!.Source.LogId == 2 || r.LogEntry.Target.LogId == 2), "Either direction includes incoming and outgoing events exactly once");
            role.SelectedItem = ParticipantRole.Target; await model.LastUpdate;
            grid.SelectedIndex = 0;
            model.ShowContextCommand.Execute().Subscribe(); await model.LastUpdate;
            Program.Check(model.SelectedParticipantRole == ParticipantRole.Either, "Surrounding context temporarily includes both participant directions");
            model.ReturnToResultsCommand.Execute().Subscribe(); await model.LastUpdate;
            Program.Check(role.SelectedItem is ParticipantRole.Target && model.EventHistory.LogsToDisplay.Count == incomingCount, "Returning from context restores participant direction and exact results");
            types.SelectedItem = DisplayType.All; await model.LastUpdate;
            Program.Check(model.EventHistory.LogsToDisplay.All(r => r.LogEntry!.Target.LogId == 2), "Direction also applies to the All events category");
            model.AvailableEntities.Single(e => e.Entity.LogId == 3).Selected = true; await model.LastUpdate;
            Program.Check(model.EventHistory.LogsToDisplay.All(r => r.LogEntry!.Target.LogId is 2 or 3), "Multiple selected participants share the chosen direction");
            model.AvailableEntities.Single(e => e.Entity.LogId == 3).Selected = false;
            participant.Selected = false; await model.LastUpdate;
            Program.Check(model.EventHistory.LogsToDisplay.Count == 0 && model.EventHistory.IsEmpty, "Deselecting all participants produces an explicit empty state");
            model.ClearFilters(); await model.LastUpdate; await SettleOverlay();
            var rowCountBeforeScroll = model.EventHistory.MaterializedRowCount;
            var scrollTimer = Stopwatch.StartNew();
            grid.ScrollIntoView(model.EventHistory.LogsToDisplay[190000], grid.Columns[0]); await SettleOverlay();
            timings["ScrollTo190000Ms"] = scrollTimer.Elapsed.TotalMilliseconds;
            Program.Check(model.EventHistory.MaterializedRowCount - rowCountBeforeScroll < 200, "Scrolling deep into the log only creates nearby rows");
            Program.Check(grid.GetVisualDescendants().OfType<DataGridRow>().Any(r => r.IsEffectivelyVisible && r.DataContext is DisplayableLogEntry entry && entry.LogEntry!.LogLineNumber >= 190000), "Deep scrolling shows the expected actual log rows");
            var source = grid.ItemsSource;
            var scrollbar = grid.GetVisualDescendants().OfType<ScrollBar>().First(b => b.Orientation == Avalonia.Layout.Orientation.Vertical);
            var offset = scrollbar.Value;
            model.ApplySearch(); await model.LastUpdate; await SettleOverlay();
            Program.Check(ReferenceEquals(source, grid.ItemsSource) && scrollbar.Value == offset, "An identical refresh preserves deep scroll position");
            var nextLog = ReviewCombat(1).AllLogs[0]; nextLog.TimeStamp = combat.EndTime.AddSeconds(1); nextLog.LogLineNumber = 200000;
            combat.AllLogs[200000] = nextLog;
            model.CombatSelected(combat); await model.LastUpdate; await SettleOverlay();
            Program.Check(model.EventHistory.LogsToDisplay.Count == 200001 && ReferenceEquals(source, grid.ItemsSource) && scrollbar.Value == offset,
                "New live events append without replacing the table or moving its scrolled viewport");
            model.LogFilter = "lightning"; model.ApplySearch(); var older = model.LastUpdate;
            model.LogFilter = "Resurgence"; model.ApplySearch(); await Task.WhenAll(older, model.LastUpdate);
            Program.Check(model.EventHistory.LogsToDisplay.Count == 20000 && model.EventHistory.LogsToDisplay[0].Ability == "Resurgence", "Rapid searches cancel stale work and publish only the latest query");
            await Search("not-a-real-ability");
            Program.Check(model.EventHistory.LogsToDisplay.Count == 0 && model.EventHistory.IsEmpty, "No-match search has an explicit empty state");
            Program.Snapshot(main, "log-review-empty");
            model.ClearFilters(); await model.LastUpdate; await SettleOverlay();
            main.Width = 900; main.Height = 540; grid.SelectedIndex = 2; await SettleOverlay();
            Program.Snapshot(main, "log-review-narrow");
            Program.Check(search.Bounds.Width >= 300 && view.FindControl<Button>("ClearFilters")!.IsEffectivelyVisible, "Search and reset remain usable in a narrow window");
            main.Hide(); main.Show(); await SettleOverlay();
            Program.Check(model.EventHistory.LogsToDisplay.Count == 200001 && ReferenceEquals(grid.ItemsSource, model.EventHistory.LogsToDisplay), "Reopening retains the results and real table binding");
            var replacement = ReviewCombat(100);
            model.CombatSelected(replacement); await model.LastUpdate; await SettleOverlay();
            Program.Check(model.EventHistory.LogsToDisplay.Count == 100 && model.EventHistory.LogsToDisplay[0].LogEntry == replacement.AllLogs[0], "Switching combats discards old row data");
            await Search("ability:\"unfinished");
            model.CombatSelected(ReviewCombat(20)); await model.LastUpdate;
            Program.Check(model.EventHistory.HasSearchError && model.EventHistory.LogsToDisplay.Count == 0, "Invalid search on a new combat never leaves the previous combat's events displayed");
            model.ClearFilters(); await model.LastUpdate;
            Program.Check(model.EventHistory.LogsToDisplay.Count == 20, "Reset recovers immediately after a search error during combat switching");
            var death = new EventHistoryViewModel(); death.SetDisplayType(DisplayType.DeathRecap); death.SetViewableEntities(new() { new Entity { Name = "All" } });
            var deathStart = await Task.Run(() => death.SelectCombat(replacement, true));
            Program.Check(deathStart == replacement.AllLogs[9].TimeStamp.AddSeconds(-15) && death.DeathReview, "The shared death-review cutoff is preserved");
            var seek = typeof(EventHistoryViewModel).GetMethod("Seek", Program.Private)!;
            var infos = (List<EntityInfo>)seek.Invoke(death, new object[] { 300d })!;
            Program.Check(infos.Count > 0 && death.SelectedIndex >= 0, "Death-review seeking returns nearby entity HP information");
            var lookupCombat = ReviewCombat(3);
            lookupCombat.AllLogs[0].Ability = "Force Lightning"; lookupCombat.AllLogs[0].AbilityId = 808252715565056;
            lookupCombat.AllLogs[0].Effect.EffectName = "Immobilized"; lookupCombat.AllLogs[0].Effect.EffectId = 808252715565423;
            lookupCombat.AllLogs[1].Ability = "Force Lightning"; lookupCombat.AllLogs[1].AbilityId = 808252715565056;
            lookupCombat.AllLogs[2].AbilityId = 0;
            model.CombatSelected(lookupCombat); await model.LastUpdate;
            grid.SelectedIndex = 0; await SettleOverlay();
            var lookupButton = view.FindControl<Button>("JedipediaButton")!;
            lookupButton.Flyout!.ShowAt(lookupButton); await SettleOverlay();
            var lookupItems = ((MenuFlyout)lookupButton.Flyout).Items.OfType<MenuItem>().ToArray();
            Program.Check(lookupButton.IsEnabled && lookupItems.Length == 2 && lookupItems.All(i => i.IsEnabled) &&
                lookupItems[0].Header?.ToString() == "Ability: Force Lightning" && lookupItems[1].Header?.ToString() == "Effect: Immobilized" &&
                lookupItems.All(i => ReferenceEquals(i.Command, model.EventHistory.SelectedEntry!.OpenJedipediaCommand)),
                "Jedipedia's real menu names the selected ability/effect and binds both lookup actions");
            Program.Snapshot(main, "log-review-jedipedia");
            lookupButton.Flyout.Hide();
            grid.SelectedIndex = 1; await SettleOverlay();
            lookupButton.Flyout.ShowAt(lookupButton); await SettleOverlay();
            Program.Check(lookupItems[0].IsEnabled && !lookupItems[1].IsEnabled &&
                lookupItems.All(i => ReferenceEquals(i.Command, model.EventHistory.SelectedEntry!.OpenJedipediaCommand)),
                "Reopening the lookup menu follows the new selection and disables generic damage effects");
            lookupButton.Flyout.Hide();
            grid.SelectedIndex = 2; await SettleOverlay();
            Program.Check(!lookupButton.IsEnabled, "A row without a lookupable ability or effect disables the Jedipedia button");
            pulse.Stop(); timings["MaxDispatcherPulseMs"] = maxPulse;
        }
        catch (Exception error) { failure = error; }
        finally
        {
            if (!Program.LogReviewInteractive || failure != null)
            {
                File.WriteAllText(Path.Combine(Program.Output, "report.json"), JsonSerializer.Serialize(new { Status = failure == null ? "Passed" : "Failed", Program.Checks, Program.Snapshots, Timings = timings, Error = failure?.ToString() }, new JsonSerializerOptions { WriteIndented = true }));
                Environment.Exit(failure == null ? 0 : 1);
            }
        }
    }
}
