using System.Collections.Specialized;
using System.ComponentModel;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.DataGrid;
using SWTORCombatParser.Views.DataGrid_Views;

public sealed partial class ConfigurationHarnessApp
{
    private static async Task PrepareRaidDataInteractive(Window main)
    {
        Settings.WriteSetting("grid_sort", "Damage_+_1");
        main.Title = "Raid Data sorting test";
        main.Width = 1100; main.Height = 440;
        var model = new DataGridViewModel();
        var view = new DataGridView(model);
        main.Content = view;
        var combat = RaidDataCombat();
        combat.TotalAbilites[combat.CharacterParticipants[1]] = 5;
        combat.TotalAbilites[combat.CharacterParticipants[2]] = 15;
        combat.TotalAbilites[combat.CharacterParticipants[3]] = 10;
        model.UpdateCombat(combat);
        await SettleOverlay();
        var grid = view.FindControl<DataGrid>("DynamicDataGrid")!;
        var ticks = 0;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) =>
        {
            foreach (var player in combat.CharacterParticipants.Values) combat.TotalEffectiveFluffDamage[player] += 100;
            model.UpdateCombat(combat);
            File.WriteAllText(Path.Combine(Program.Output, "sort-state.json"), JsonSerializer.Serialize(new {
                Ticks = ++ticks, Sort = Settings.ReadSettingOfType<string>("grid_sort"), Model = model.PartyMembers.Select(m => m.PlayerName),
                Displayed = DisplayedRaidOrder(grid)
            }));
        };
        timer.Start();
        main.Closed += (_, _) => { timer.Stop(); Environment.Exit(0); };
    }

    private static string[] DisplayedRaidOrder(DataGrid grid) => grid.GetVisualDescendants().OfType<DataGridRow>()
        .Where(r => r.IsEffectivelyVisible && r.Bounds.Height > 0 && r.DataContext is MemberInfoViewModel)
        .OrderBy(r => r.TranslatePoint(new Point(), grid)?.Y)
        .Select(r => ((MemberInfoViewModel)r.DataContext!).PlayerName).Distinct().ToArray();

    private static Combat RaidDataCombat(int count = 3, long firstId = 1)
    {
        var combat = new Combat { StartTime = DateTime.Today, EndTime = DateTime.Today.AddSeconds(10) };
        for (var i = 0; i < count; i++)
        {
            var entity = new Entity { Id = firstId + i, LogId = firstId + i, Name = "Player " + (i + 1).ToString("00"), IsCharacter = true, IsLocalPlayer = i == 0 };
            combat.CharacterParticipants[entity.LogId] = entity;
            combat.CharacterClases[entity] = new SWTORClass { Name = "Sniper", Discipline = "Virulence", Role = Role.DPS };
            combat.TotalEffectiveFluffDamage[entity] = (count - i) * 20000;
            combat.TotalEffectiveFocusDamage[entity] = 0;
            combat.TotalFluffDamage[entity] = (count - i) * 25000;
            combat.TotalFocusDamage[entity] = 0;
        }
        return combat;
    }

    private static string GridHeader(DataGridColumn column) => column.Header is TextBlock text ? text.Text! : column.Header!.ToString()!;
    private static DataGridRow RaidRow(DataGrid grid, MemberInfoViewModel member) =>
        grid.GetVisualDescendants().OfType<DataGridRow>().First(row => row.IsEffectivelyVisible && row.Bounds.Height > 0 && ReferenceEquals(row.DataContext, member));
    private static TextBlock DamageCell(DataGrid grid, MemberInfoViewModel member) =>
        grid.GetVisualDescendants().OfType<TextBlock>().First(t => t.IsEffectivelyVisible && t.Bounds.Width > 0 && t.Text == member.StatsSlots.Single(s => s.OverlayType == OverlayType.Damage).Value);

    private static async Task RunRaidDataChecks(Window owner)
    {
        Exception? failure = null;
        try
        {
            Settings.WriteSetting("grid_sort", "Damage_+_1");
            var model = new DataGridViewModel();
            var view = new DataGridView(model);
            var grid = view.FindControl<DataGrid>("DynamicDataGrid")!;
            var window = new Window { Title = "Raid Data refresh checks", Width = 1040, Height = 460, Content = view, ShowActivated = false };
            window.Show(owner);
            var combat = RaidDataCombat();
            combat.TotalAbilites[combat.CharacterParticipants[1]] = 5;
            combat.TotalAbilites[combat.CharacterParticipants[2]] = 15;
            combat.TotalAbilites[combat.CharacterParticipants[3]] = 10;
            model.UpdateCombat(combat);
            await SettleOverlay();
            var members = model.PartyMembers.ToArray();
            var totals = members.Single(m => m.IsTotalsRow);
            var alice = members.Single(m => m._entity?.LogId == 1);
            var damageColumn = grid.Columns.Single(c => GridHeader(c) == "Damage");
            damageColumn.Width = new DataGridLength(160);
            grid.SelectedItem = alice;
            await SettleOverlay();
            Program.Check(ReferenceEquals(grid.ItemsSource, model.PartyMembers), "Raid Data retains its observable ItemsSource binding");
            Program.Check(model.PartyMembers.Last().IsTotalsRow, "Totals start below player rows");
            Program.Snapshot(window, "raid-data-before-refresh");
            File.WriteAllText(Path.Combine(Program.Output, "initial-grid.json"), JsonSerializer.Serialize(new {
                Columns = grid.Columns.Select(c => new { Header = GridHeader(c), c.ActualWidth, Width = c.Width.ToString() }),
                Rows = model.PartyMembers.Select(m => new { m.PlayerName, Slots = m.StatsSlots.Select(s => new { s.Header, s.Value }),
                    Texts = RaidRow(grid, m).GetVisualDescendants().OfType<TextBlock>().Select(t => new { t.Text, Bounds = t.Bounds.ToString(), Position = t.TranslatePoint(new Point(), grid)?.ToString() }).ToArray() })
            }, new JsonSerializerOptions { WriteIndented = true }));
            var initialColumns = grid.Columns.ToArray();
            var aliceVisual = RaidRow(grid, alice);
            var aliceCell = DamageCell(grid, alice);
            var totalCell = DamageCell(grid, totals);
            var rowsChanged = 0; var columnsChanged = 0; var cellsDetached = 0;
            model.PartyMembers.CollectionChanged += (_, _) => rowsChanged++;
            ((INotifyCollectionChanged)grid.Columns).CollectionChanged += (_, _) => columnsChanged++;
            aliceCell.DetachedFromVisualTree += (_, _) => cellsDetached++;
            totalCell.DetachedFromVisualTree += (_, _) => cellsDetached++;
            for (var tick = 0; tick < 100; tick++)
            {
                foreach (var player in combat.CharacterParticipants.Values) combat.TotalEffectiveFluffDamage[player] += 100;
                model.UpdateCombat(combat);
                await Task.Delay(12); // Allow real layout/render passes between updates.
                if (grid.Columns.Count == 0 || !aliceVisual.IsEffectivelyVisible || string.IsNullOrEmpty(aliceCell.Text))
                    throw new InvalidOperationException("Visible table content disappeared during refresh " + tick);
            }
            Program.Check(rowsChanged == 0 && columnsChanged == 0 && cellsDetached == 0,
                "100 value refreshes cause zero row/column collection changes or visible cell detachments");
            Program.Check(initialColumns.SequenceEqual(grid.Columns) && ReferenceEquals(aliceVisual, RaidRow(grid, alice)) && ReferenceEquals(aliceCell, DamageCell(grid, alice)),
                "Columns, row containers and numeric TextBlocks retain their identities during refresh");
            Program.Check(aliceCell.Text == "70,000" && totalCell.Text == "150,000", "Existing player and total cells display updated combat values");
            Program.Check(ReferenceEquals(grid.SelectedItem, alice) && damageColumn.Width.Value == 160, "Refresh preserves selected player and resized column width");
            Program.Check(ReferenceEquals(totals, model.PartyMembers.Single(m => m.IsTotalsRow)) &&
                model.PartyMembers.All(m => m.StatsSlots.Count == 7 && m.StatsSlots.Select(s => s.OverlayType).Distinct().Count() == 7),
                "Totals are reused and metric slots do not accumulate blank or duplicate cells");
            Program.Snapshot(window, "raid-data-after-100-refreshes");

            // Exercise the grid's actual sorting entry point, then change a live rank.
            damageColumn.Sort();
            await SettleOverlay();
            Program.Check(model.PartyMembers[0]._entity.LogId == 3 && model.PartyMembers.Last().IsTotalsRow, "Header sorting switches to ascending while totals remain last");
            Program.Check(DisplayedRaidOrder(grid).SequenceEqual(model.PartyMembers.Select(m => m.PlayerName)), "Ascending sort changes the actual displayed row order");
            damageColumn.Sort();
            await SettleOverlay();
            Program.Check(model.PartyMembers[0] == alice && model.PartyMembers.Last().IsTotalsRow, "Header sorting switches back to descending");
            Program.Check(DisplayedRaidOrder(grid).SequenceEqual(model.PartyMembers.Select(m => m.PlayerName)), "Descending sort changes the actual displayed row order");
            Program.Check(ReferenceEquals(grid.SelectedItem, alice), "Changing sort direction preserves the selected player even when its row moves");

            var apmColumn = grid.Columns.Single(c => GridHeader(c) == "APM");
            apmColumn.Sort(); await SettleOverlay();
            Program.Check(DisplayedRaidOrder(grid).SequenceEqual(new[] { "Player 01", "Player 03", "Player 02", "Total" }),
                "Switching to APM sorts visible rows by that metric instead of the previous column");
            apmColumn.Sort(); await SettleOverlay();
            Program.Check(DisplayedRaidOrder(grid).SequenceEqual(new[] { "Player 02", "Player 03", "Player 01", "Total" }) &&
                Settings.ReadSettingOfType<string>("grid_sort") == "APM_+_1", "The second metric toggles direction and saves the selected sort");
            damageColumn.Sort(); await SettleOverlay();
            damageColumn.Sort(); await SettleOverlay();
            Program.Check(DisplayedRaidOrder(grid).SequenceEqual(new[] { "Player 01", "Player 02", "Player 03", "Total" }),
                "Returning to Damage restores its correct displayed order");
            var bob = combat.CharacterParticipants[2];
            combat.TotalEffectiveFluffDamage[bob] = 90000;
            model.UpdateCombat(combat); await SettleOverlay();
            Program.Check(model.PartyMembers[0]._entity.LogId == 2 && ReferenceEquals(grid.SelectedItem, alice), "Live rank changes move rows without losing the selected player");
            Program.Check(DisplayedRaidOrder(grid).SequenceEqual(model.PartyMembers.Select(m => m.PlayerName)), "Live rank changes update the displayed table order");
            Program.Check(ReferenceEquals(grid.ItemsSource, model.PartyMembers) && initialColumns.SequenceEqual(grid.Columns), "Sorting and live rank changes never replace ItemsSource or columns");
            Program.Snapshot(window, "raid-data-rank-change");
            combat.TotalEffectiveFluffDamage[combat.CharacterParticipants[1]] = 90000;
            var changesBeforeTie = rowsChanged;
            model.UpdateCombat(combat); await SettleOverlay();
            Program.Check(rowsChanged == changesBeforeTie && DisplayedRaidOrder(grid).SequenceEqual(new[] { "Player 02", "Player 01", "Player 03", "Total" }),
                "Equal values retain their displayed order without row collection changes");

            model.AddHeader("Raw DPS"); await SettleOverlay();
            Program.Check(grid.Columns.Any(c => GridHeader(c) == "Raw DPS") && model.PartyMembers.All(m => m.StatsSlots.Any(s => s.OverlayType == OverlayType.NonEDPS)),
                "Adding a metric updates the real header and every row including totals");
            model.RemoveHeader(OverlayType.Damage); await SettleOverlay();
            Program.Check(grid.Columns.All(c => GridHeader(c) != "Damage") && model.PartyMembers.All(m => m.StatsSlots.All(s => s.OverlayType != OverlayType.Damage)),
                "Removing the sorted metric removes all corresponding slots and chooses an available sort column");
            model.AddHeader("Damage"); await SettleOverlay();
            Program.Check(model.PartyMembers.All(m => m.StatsSlots.Select(s => s.OverlayType).SequenceEqual(model.PartyMembers[0].StatsSlots.Select(s => s.OverlayType))),
                "Re-added columns retain consistent metric ordering for every player and totals");

            var raid = RaidDataCombat(24);
            model.UpdateCombat(raid); await SettleOverlay();
            var selected = model.PartyMembers.Single(m => m._entity?.LogId == 20);
            grid.SelectedItem = selected; grid.ScrollIntoView(selected, grid.Columns.Last());
            await SettleOverlay();
            var scroll = grid.GetVisualDescendants().OfType<ScrollBar>().First(s => s.Orientation == Avalonia.Layout.Orientation.Vertical);
            var offset = scroll.Value;
            Program.Check(offset > 0, "Large-raid fixture scrolls below the first visible rows");
            for (var tick = 0; tick < 20; tick++)
            {
                foreach (var player in raid.CharacterParticipants.Values) raid.TotalEffectiveFluffDamage[player] += 10;
                model.UpdateCombat(raid); await Task.Delay(12);
            }
            Program.Check(scroll.Value == offset && ReferenceEquals(grid.SelectedItem, selected), "Repeated large-raid updates retain scroll offset and selection");
            Program.Snapshot(window, "raid-data-scrolled-refresh");
            raid.TotalEffectiveFluffDamage[raid.CharacterParticipants[20]] = 900000;
            model.UpdateCombat(raid); await SettleOverlay();
            Program.Check(model.PartyMembers[0] == selected && ReferenceEquals(grid.SelectedItem, selected) && Math.Abs(scroll.Value - offset) < 1,
                "A selected player changing rank preserves selection and the scrolled viewport");
            var displayedNames = DisplayedRaidOrder(grid);
            var expectedNames = model.PartyMembers.Select(m => m.PlayerName).Where(displayedNames.Contains);
            Program.Check(displayedNames.SequenceEqual(expectedNames), "Visible rows follow the new sort order while scrolled");
            raid.CharacterParticipants.TryRemove(1, out _);
            var joiner = new Entity { LogId = 99, Id = 99, Name = "New player", IsCharacter = true };
            raid.CharacterParticipants[99] = joiner;
            raid.TotalEffectiveFluffDamage[joiner] = 5000; raid.TotalEffectiveFocusDamage[joiner] = 0;
            model.UpdateCombat(raid); await SettleOverlay();
            Program.Check(model.PartyMembers.All(m => m._entity?.LogId != 1) && model.PartyMembers.Any(m => m._entity?.LogId == 99) &&
                ReferenceEquals(selected, model.PartyMembers.Single(m => m._entity?.LogId == 20)),
                "Player joins and departures preserve surviving row objects");

            var replacement = RaidDataCombat(3, 200);
            await Task.Run(() => model.UpdateCombat(replacement)); await SettleOverlay();
            Program.Check(model.PartyMembers.Count == 4 && model.PartyMembers.Where(m => !m.IsTotalsRow).All(m => m._entity.LogId >= 200),
                "Switching combats from a background caller publishes only the new players on the UI thread");
            Program.Check(model.PartyMembers.Single(m => m.IsTotalsRow).StatsSlots.Single(s => s.OverlayType == OverlayType.Damage).Value == "120,000",
                "The reused totals row resets to the newly selected combat");
            grid.ScrollIntoView(model.PartyMembers[0], grid.Columns[0]); await SettleOverlay();
            var oldRow = model.PartyMembers.Single(m => m._entity?.LogId == 200);
            var renamed = new Entity { Id = 200, LogId = 200, Name = "Renamed player", IsCharacter = true, IsLocalPlayer = false };
            replacement.CharacterClases.Remove(oldRow._entity);
            replacement.CharacterParticipants[200] = renamed;
            replacement.CharacterClases[renamed] = new SWTORClass { Name = "Sorcerer", Discipline = "Corruption", Role = Role.Healer };
            model.UpdateCombat(replacement); await SettleOverlay();
            Program.Check(ReferenceEquals(oldRow, model.PartyMembers.Single(m => m._entity?.LogId == 200)) && oldRow.ClassName == "Sorcerer/Corruption" &&
                RaidRow(grid, oldRow).GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Renamed player"),
                "Reused rows update character names and class metadata from replacement snapshots");
            window.Hide(); window.Show(owner); await SettleOverlay();
            model.UpdateCombat(replacement); await SettleOverlay();
            Program.Check(ReferenceEquals(grid.ItemsSource, model.PartyMembers) && grid.Columns.Count == 8, "Reopening retains the table binding and configured columns");
            model.UpdateCombat(new Combat()); await SettleOverlay();
            Program.Check(model.PartyMembers.Count == 1 && model.PartyMembers[0].IsTotalsRow && model.PartyMembers[0].StatsSlots.Skip(1).All(s => s.Value == "0"),
                "An empty combat clears previous players and values without stale totals");
            window.Close();
        }
        catch (Exception error) { failure = error; }
        finally
        {
            File.WriteAllText(Path.Combine(Program.Output, "report.json"), JsonSerializer.Serialize(new {
                Status = failure == null ? "Passed" : "Failed", Program.Checks, Program.Snapshots, Error = failure?.ToString()
            }, new JsonSerializerOptions { WriteIndented = true }));
            Environment.Exit(failure == null ? 0 : 1);
        }
    }
}
