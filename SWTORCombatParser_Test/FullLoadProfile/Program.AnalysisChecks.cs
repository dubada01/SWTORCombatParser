using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SWTORCombatParser.ViewModels;
using SWTORCombatParser.ViewModels.DataGrid;
using SWTORCombatParser.ViewModels.Home_View_Models;
using SWTORCombatParser.ViewModels.Overviews;

public sealed partial class FullLoadApp
{
    private static async Task RunAnalysisChecks(Window window, MainWindowViewModel main, TableViewModel details,
        PlotViewModel plot, DataGridViewModel raid, Func<int, Task> tab, Action<bool, string> check)
    {
        T Control<T>(string name) where T : Control => window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
        async Task Settle() { await Task.Delay(180); await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); }
        void Snapshot(string name)
        {
            using var bitmap = new RenderTargetBitmap(PixelSize.FromSize(window.Bounds.Size, 1), new Vector(96, 96));
            bitmap.Render(window); bitmap.Save(Path.Combine(Program.OutputDirectory, name + ".png"));
        }
        window.Width = 1400; window.Height = 900; await Settle();
        await tab(0); Snapshot("analysis-raid-data");
        var metrics = Control<Button>("MetricsButton");
        metrics.Flyout!.ShowAt(metrics); await Settle();
        var panel = (Control)((Flyout)metrics.Flyout).Content!;
        var choices = panel.GetVisualDescendants().OfType<CheckBox>().ToArray();
        check(choices.Length == raid.MetricOptions.Count && choices.Length > 20, "Metric picker presents every metric in real checkbox controls");
        foreach (var box in choices.Where(b => b.IsChecked != true).Take(8 - raid.MetricCount)) box.SetCurrentValue(ToggleButton.IsCheckedProperty, true);
        await Settle();
        check(raid.MetricCount == 8 && choices.Where(b => b.IsChecked != true).All(b => !b.IsEnabled), "Metric picker enforces the eight-column limit without disabling selected columns");
        var popup = (Visual)panel.GetVisualRoot()!;
        using (var bitmap = new RenderTargetBitmap(PixelSize.FromSize(popup.Bounds.Size, 1), new Vector(96, 96)))
        { bitmap.Render(popup); bitmap.Save(Path.Combine(Program.OutputDirectory, "analysis-metrics.png")); }
        raid.ResetMetricsCommand.Execute().Subscribe(); await Settle();
        check(raid.MetricCount == 6 && choices.Count(b => b.IsChecked == true) == 6, $"Restore defaults updates the live metric picker and table (model {raid.MetricCount}, checked {choices.Count(b => b.IsChecked == true)})");
        metrics.Flyout.Hide();

        await tab(2); await details.ActiveTable.LastUpdate;
        var categories = Control<TabStrip>("DetailsTabs");
        var grouping = Control<ComboBox>("DetailsGrouping");
        var participant = Control<ComboBox>("DetailsParticipant");
        check(((TableInstanceViewModel)details.HealingVM).DataToView.Count == 0, "Details defers hidden categories until they are opened");
        for (var i = 0; i < 5; i++)
        {
            categories.SelectedIndex = i; await details.ActiveTable.LastUpdate;
            foreach (var group in Enum.GetValues<SortingOption>())
            {
                grouping.SelectedItem = group; await details.ActiveTable.LastUpdate; await Settle();
                var table = Control<DataGrid>("DetailsGrid");
                check(details.SelectedOrdering == group && ReferenceEquals(table.ItemsSource, details.ActiveTable.DataToView) && !details.ActiveTable.IsLoading,
                    $"Details category {i} opens and groups by {group} through its actual controls");
                var aggregates = details.ActiveTable.Overall;
                var data = details.ActiveTable.DataToView;
                if (data.Count > 0)
                {
                    var footer = Control<Grid>("TotalsCells");
                    check(footer.IsEffectivelyVisible && ReferenceEquals(footer.DataContext, aggregates) && footer.Children.Count == 10 &&
                        aggregates!.Count == data.Sum(r => r.Count) && Math.Abs(aggregates.Average - data.Sum(r => r.Total) / aggregates.Count) < .001 &&
                        Math.Abs(aggregates.CritPercent - data.Sum(r => r.CritPercent * r.Count) / aggregates.Count) < .001 &&
                        aggregates.Max == data.Max(r => r.Max) && aggregates.MaxCrit == data.Max(r => r.MaxCrit),
                        $"Category {i}, {group}: totals retain weighted averages, maximums, critical percentage and event count after rendering");
                }
            }
        }
        categories.SelectedIndex = 0; grouping.SelectedItem = SortingOption.ByAbility;
        participant.SelectedItem = details.AvailableParticipants.First(p => p.IsLocalPlayer); await details.ActiveTable.LastUpdate;
        var values = details.ActiveTable.DataToView;
        check(values.Count > 0 && details.ActiveTable.Totals[0].Count == values.Sum(r => r.Count), "Details totals include all events from the active participant");
        var name = values[0].SortItem;
        var search = Control<TextBox>("DetailsSearch"); search.Text = name; await Settle();
        check(values.Count > 0 && values.All(r => r.SortItem.Contains(name, StringComparison.OrdinalIgnoreCase)), "Details text search filters the active table");
        search.Text = "no-such-ability-123"; await Settle();
        check(details.ActiveTable.IsEmpty && values.Count == 0, "Details search exposes a clear no-results state");
        check(Control<Grid>("TotalsCells").IsEffectivelyVisible && details.ActiveTable.Overall!.Count > 0, "Searching Details never drops the overall totals strip");
        search.Text = ""; await Settle();
        var first = values[0]; var tableGrid = Control<DataGrid>("DetailsGrid");
        tableGrid.SelectedItem = first;
        var total = tableGrid.Columns.Single(c => c.Header?.ToString() == "Total");
        total.Sort(System.ComponentModel.ListSortDirection.Ascending); await Settle();
        var visible = tableGrid.GetVisualDescendants().OfType<DataGridRow>().Where(r => r.IsEffectivelyVisible && r.Bounds.Height > 0)
            .OrderBy(r => r.TranslatePoint(new Point(), tableGrid)!.Value.Y).Select(r => (CombatInfoInstance)r.DataContext!).ToArray();
        check(visible.Length > 1 && visible.Select(r => r.Total).SequenceEqual(visible.Select(r => r.Total).Order()), "Details Total column sorts numbers in the rendered rows");
        await tab(0); await tab(2); await details.ActiveTable.LastUpdate; await Settle();
        check(ReferenceEquals(values, details.ActiveTable.DataToView) && values.Contains(first) && ReferenceEquals(tableGrid.SelectedItem, first), "Reopening Details retains row objects, selection, and results");
        participant.SelectedItem = details.AvailableParticipants.Last(); var older = details.ActiveTable.LastUpdate;
        participant.SelectedItem = details.AvailableParticipants.First(p => p.IsLocalPlayer);
        await Task.WhenAll(older, details.ActiveTable.LastUpdate);
        check(ReferenceEquals(details.SelectedEntity, participant.SelectedItem) && !details.ActiveTable.IsLoading, "Rapid participant changes publish the latest Details result");
        total.Sort(System.ComponentModel.ListSortDirection.Descending); await Settle(); Snapshot("analysis-details");
        var share = tableGrid.Columns[1]; var oldShareWidth = share.Width;
        share.Width = new DataGridLength(64); await Settle();
        var narrowBar = tableGrid.GetVisualDescendants().OfType<SWTORCombatParser.Views.Overviews.ContributionBar>().First(b => b.IsEffectivelyVisible);
        var smallWidth = narrowBar.Bounds.Width;
        check(smallWidth > 20 && smallWidth <= 64, "Share bar remains visible in a narrowed 64px column");
        Snapshot("details-share-narrow");
        share.Width = new DataGridLength(240); await Settle();
        check(narrowBar.Bounds.Width > smallWidth + 150 && narrowBar.Bounds.Width <= share.ActualWidth, "Share bar grows with the actual column width");
        var footerCells = Control<Grid>("TotalsCells");
        check(Math.Abs(footerCells.ColumnDefinitions[1].Width.Value - share.ActualWidth) < 1, "Totals strip tracks resized column widths");
        share.DisplayIndex = 4; await Settle();
        check(Grid.GetColumn(footerCells.Children[1]) == 4, "Totals cells follow reordered columns");
        share.DisplayIndex = 1; share.Width = oldShareWidth; await Settle();

        await tab(1); await Settle();
        var picker = (ParticipantSelectionViewModel)plot.ParticipantSelectionContent.DataContext!;
        var items = picker.AvailableParticipants.ToArray();
        picker.UpdateParticipantsData(main.CurrentlyDisplayedCombat);
        check(items.SequenceEqual(picker.AvailableParticipants), "Graph refresh retains participant control models and updates their data");
        var bosses = Control<CheckBox>("ShowBosses"); bosses.SetCurrentValue(ToggleButton.IsCheckedProperty, true); await Settle();
        check(picker.AvailableParticipants.Any(p => p.Entity.IsBoss), "Bosses toggle exposes boss participants in the graph picker");
        bosses.SetCurrentValue(ToggleButton.IsCheckedProperty, false); await Settle();
        check(picker.AvailableParticipants.All(p => p.Entity.IsCharacter), "Disabling bosses restores player-only selection");
        var smoothing = Control<NumericUpDown>("Smoothing");
        smoothing.Value = 3; smoothing.Value = 7; await Task.Delay(400);
        check(plot.AverageWindowSeconds == 7 && CapturePlot(main).ScatterPointCount > 0, "Rolling-average control rebuilds the graph after rapid edits");
        var expandedChartWidth = Control<Control>("GridView").Bounds.Width;
        var collapse = Control<Button>("CollapseEffects"); collapse.Command!.Execute(collapse.CommandParameter); await Settle();
        var expand = Control<Button>("ExpandEffects");
        check(!plot.ShowEffects && !plot.EffectsView.IsEffectivelyVisible && expand.IsEffectivelyVisible && Control<Control>("GridView").Bounds.Width > expandedChartWidth + 170,
            "Collapsing the effects tray leaves a slim reopen tab and expands the chart");
        check(!SWTORCombatParser.Utilities.Settings.ReadSettingOfType<bool>("plot_effects_expanded"), "The collapsed effects-tray preference is saved");
        plot.FollowLive = false;
        dynamic chart = typeof(PlotViewModel).GetField("GraphView", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(plot)!;
        var points = Control<CheckBox>("ShowPoints"); var trends = Control<CheckBox>("ShowTrends");
        var chartSeries = ((System.Collections.IEnumerable)typeof(PlotViewModel).GetField("_seriesToPlot", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(plot)!).Cast<object>().ToArray();
        object[] SeriesPlots(string property) => chartSeries.SelectMany(s => ((System.Collections.IDictionary)s.GetType().GetProperty(property)!.GetValue(s)!).Values.Cast<object>()).ToArray();
        bool Visible(object item) => (bool)item.GetType().GetProperty("IsVisible")!.GetValue(item)!;
        var originalPointPlots = SeriesPlots("Points"); var originalLines = SeriesPlots("Line");
        check(!plot.ShowPoints && plot.ShowTrends && originalPointPlots.All(p => !Visible(p)) && originalLines.Any(Visible), "The graph defaults to clean trend lines without event markers");
        foreach (var mode in new[] { (true, true), (true, false), (false, false), (false, true) })
        {
            points.SetCurrentValue(ToggleButton.IsCheckedProperty, mode.Item1); trends.SetCurrentValue(ToggleButton.IsCheckedProperty, mode.Item2); await Settle();
            check(originalPointPlots.Any(Visible) == mode.Item1 && originalLines.Any(Visible) == mode.Item2 &&
                originalPointPlots.SequenceEqual(SeriesPlots("Points")) && originalLines.SequenceEqual(SeriesPlots("Line")),
                $"Points={mode.Item1}, Trends={mode.Item2}: real controls switch layers without rebuilding chart data");
        }
        var trendTop = (double)chart.Plot.Axes.GetLimits().Top;
        points.SetCurrentValue(ToggleButton.IsCheckedProperty, true); await Settle();
        check((double)chart.Plot.Axes.GetLimits().Top > trendTop * 1.2, "Vertical scale follows the visible layers instead of reserving space for hidden event spikes");
        points.SetCurrentValue(ToggleButton.IsCheckedProperty, false);
        check(!SWTORCombatParser.Utilities.Settings.ReadSettingOfType<bool>("plot_show_points") && SWTORCombatParser.Utilities.Settings.ReadSettingOfType<bool>("plot_show_trends"), "Points and Trends preferences are saved for the next launch");
        var hpLegend = plot.LegendItems.Single(l => l.Name == "HP"); hpLegend.Checked = true;
        check((bool)chart.Plot.Axes.Right.TickLabelStyle.IsVisible && (double)chart.Plot.Axes.Right.Min == 0 && (double)chart.Plot.Axes.Right.Max == 1, "The optional health trace uses a fixed 0–100% axis");
        trends.SetCurrentValue(ToggleButton.IsCheckedProperty, false);
        check(!(bool)chart.Plot.Axes.Right.TickLabelStyle.IsVisible, "Hiding trends also hides the health axis");
        trends.SetCurrentValue(ToggleButton.IsCheckedProperty, true); hpLegend.Checked = false;
        var frameTimes = new Dictionary<string, double>();
        foreach (var enabled in new[] { true, false })
        {
            points.SetCurrentValue(ToggleButton.IsCheckedProperty, enabled); await Settle();
            chart.Plot.RenderInMemory(1000, 520);
            var samples = new List<double>();
            for (var frame = 0; frame < 25; frame++)
            { var clock = System.Diagnostics.Stopwatch.StartNew(); chart.Plot.RenderInMemory(1000, 520); samples.Add(clock.Elapsed.TotalMilliseconds); }
            frameTimes[enabled ? "PointsAndTrendsMedianMs" : "TrendsOnlyMedianMs"] = samples.Order().ElementAt(samples.Count / 2);
        }
        System.IO.File.WriteAllText(Path.Combine(Program.OutputDirectory, "graph-frame-times.json"), System.Text.Json.JsonSerializer.Serialize(frameTimes));
        chart.Plot.Axes.SetLimitsX(30d, 60d);
        plot.UpdateLivePlot(main.CurrentlyDisplayedCombat);
        dynamic heldLimits = chart.Plot.Axes.GetLimits();
        check(!plot.FollowLive && Math.Abs((double)heldLimits.Left - 30) < 0.001 && Math.Abs((double)heldLimits.Right - 60) < 0.001 && CapturePlot(main).ScatterPointCount > 0, "Live graph refresh preserves the user's zoom while follow-live is off");
        var fit = Control<Button>("FitPlot"); fit.Command!.Execute(fit.CommandParameter);
        check(plot.FollowLive, "Fit view resets the chart and resumes live following");
        var legend = plot.LegendItems.First(); var oldChecked = legend.Checked;
        var legendBox = window.GetVisualDescendants().OfType<CheckBox>().First(b => ReferenceEquals(b.DataContext, legend) && b.Content?.ToString() == legend.Name);
        legendBox.IsChecked = !oldChecked; await Settle();
        check(legend.Checked == !oldChecked, "Graph legend checkbox updates its series visibility");
        legendBox.IsChecked = oldChecked; expand.Command!.Execute(expand.CommandParameter); await Settle();
        var directions = Control<ComboBox>("EffectDirection");
        var effectModel = (SWTORCombatParser.ViewModels.CombatMetaData.CombatEfffectViewModel)plot.EffectsView.DataContext!;
        foreach (var direction in effectModel.AvailableEffectTypes)
        {
            directions.SelectedItem = direction; await Settle();
            check(effectModel.SelectedEffectType == direction && Control<ComboBox>("EffectOther").IsVisible == direction.Contains("Other"), "Effect direction opens the appropriate participant controls: " + direction);
        }
        directions.SelectedIndex = 0; await Settle();
        var effectList = Control<ListBox>("EffectList"); effectList.SelectedIndex = 0;
        check(effectModel.CombatEffects.Any(e => e.Selected), "Selecting an effect marks its intervals for highlighting");
        var selectedEffectName = effectModel.SelectedEffect.Name;
        collapse.Command!.Execute(collapse.CommandParameter); await Settle(); Snapshot("analysis-effects-collapsed");
        await tab(0); await tab(1); await Settle();
        check(!plot.ShowEffects && expand.IsEffectivelyVisible, "Reopening Plot preserves the collapsed effects tray");
        expand.Command!.Execute(expand.CommandParameter); await Settle();
        check(plot.ShowEffects && effectList.IsEffectivelyVisible && effectModel.SelectedEffect?.Name == selectedEffectName && effectModel.CombatEffects.Any(e => e.Selected),
            "Reopening the effects tray restores the selected effect and its highlight");
        var clearEffects = Control<Button>("ClearEffects"); clearEffects.Command!.Execute(clearEffects.CommandParameter);
        await Settle();
        check(effectModel.CombatEffects.All(e => !e.Selected) && effectList.SelectedItem == null, "Clear highlight removes both graph highlight and visible list selection");
        await tab(0); await tab(1); await Settle();
        check(plot.AverageWindowSeconds == 7 && plot.ShowEffects && CapturePlot(main).ScatterPointCount > 0, "Reopening Plot preserves chart settings and data");
        Snapshot("analysis-plot");
        var playerButtons = plot.ParticipantSelectionContent.GetVisualDescendants().OfType<Button>().Where(b => b.DataContext is ParticipantViewModel).ToArray();
        check(playerButtons.Length == 16 && playerButtons.All(b => b.Bounds.Width > 100 && b.Bounds.Height >= 36) && plot.ParticipantSelectionContent.Bounds.Height <= 85,
            "Sixteen players use full-width readable buttons in two compact rows");

        window.Width = 960; window.Height = 540; await Settle();
        foreach (var index in new[] { 0, 1, 2, 3 })
        {
            await tab(index); await Settle(); Snapshot("analysis-narrow-" + index);
            if (index == 2) check(Control<TextBox>("DetailsSearch").Bounds.Width >= 100, "Details search remains usable at the minimum window size");
            if (index == 2)
            {
                var grid = Control<DataGrid>("DetailsGrid");
                var horizontal = grid.GetVisualDescendants().OfType<ScrollBar>().First(b => b.Orientation == Avalonia.Layout.Orientation.Horizontal);
                grid.ScrollIntoView(details.ActiveTable.DataToView[0], grid.Columns.Last()); await Settle();
                check(horizontal.Value > 0 && Math.Abs(Control<ScrollViewer>("TotalsScroll").Offset.X - horizontal.Value) < 1, "Totals remain aligned when the narrow Details table scrolls horizontally");
                var bodyAmount = grid.GetVisualDescendants().OfType<TextBlock>().First(t => t.IsEffectivelyVisible && t.Bounds.Width > 0 && t.DataContext is CombatInfoInstance row && t.Text == row.Total.ToString("N0"));
                var footerAmount = Control<Grid>("TotalsCells").Children[2];
                var bodyRight = bodyAmount.TranslatePoint(new Point(bodyAmount.Bounds.Width, 0), window)!.Value.X;
                var footerRight = footerAmount.TranslatePoint(new Point(footerAmount.Bounds.Width, 0), window)!.Value.X;
                check(Math.Abs(bodyRight - footerRight) < 12, $"Rendered totals align with the body after scrolling ({bodyRight:0.0}/{footerRight:0.0})");
                Snapshot("details-totals-scrolled");
            }
        }
        await tab(0);
    }
}
