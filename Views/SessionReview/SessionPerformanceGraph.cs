using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using SWTORCombatParser.Model.SessionReview;
using SWTORCombatParser.ViewModels.SessionReview;

namespace SWTORCombatParser.Views.SessionReview;

/// <summary>Direct drawing avoids a control for every sample. Coincident screen pixels retain min/max spikes.</summary>
public sealed class SessionPerformanceGraph : Control
{
    public static readonly StyledProperty<SessionGraphData?> GraphProperty =
        AvaloniaProperty.Register<SessionPerformanceGraph, SessionGraphData?>(nameof(Graph));
    public SessionGraphData? Graph { get => GetValue(GraphProperty); set => SetValue(GraphProperty, value); }
    private static readonly Typeface Font = new("Inter, Segoe UI, sans-serif");
    private static readonly IBrush Muted = Brush.Parse("#A5B5AC");
    private static readonly IBrush GridBrush = Brush.Parse("#303E36");
    private double? _pointerX;
    private string? _tip;
    private const double Left = 54;
    private Rect Plot => new(Left, 22, Math.Max(1, Bounds.Width - Left - 12), Math.Max(1, Bounds.Height - 57));

    static SessionPerformanceGraph() => AffectsRender<SessionPerformanceGraph>(GraphProperty);
    public SessionPerformanceGraph()
    {
        ClipToBounds = true;
        ToolTip.SetShowDelay(this, 100);
        ToolTip.SetBetweenShowDelay(this, 0);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Bounds.Width < 100 || Bounds.Height < 70) return;
        var plot = Plot;
        var graph = Graph;
        context.DrawRectangle(Brush.Parse("#191F1B"), null, plot, 3, 3);
        if (graph is null || graph.Snapshot.ElapsedSeconds <= 0 || graph.Snapshot.Combats.Count == 0)
        {
            DrawText(context, "No combats in this view", new Point(Left + 15, plot.Center.Y), Muted);
            return;
        }
        var duration = graph.Snapshot.ElapsedSeconds;
        var max = graph.Metric == SessionGraphMetric.HealthPercent ? 100 : Math.Max(1,
            graph.Series.SelectMany(s => s.Points).Where(p => p.Value.HasValue).Select(p => p.Value!.Value).DefaultIfEmpty().Max() * 1.08);
        double X(double seconds) => plot.Left + seconds / duration * plot.Width;
        double Y(double value) => plot.Bottom - value / max * plot.Height;
        for (var i = 0; i <= 4; i++)
        {
            var y = plot.Bottom - i * plot.Height / 4;
            context.DrawLine(new Pen(GridBrush), new Point(plot.Left, y), new Point(plot.Right, y));
            var text = Text(Compact(max * i / 4), Muted);
            context.DrawText(text, new Point(Math.Max(0, plot.Left - text.Width - 7), y - text.Height / 2));
        }
        var ticks = Math.Clamp((int)(plot.Width / 115), 2, 10);
        for (var i = 0; i <= ticks; i++)
        {
            var x = X(duration * i / ticks);
            context.DrawLine(new Pen(GridBrush), new Point(x, plot.Top), new Point(x, plot.Bottom));
            var text = Text(SessionPerformanceViewModel.Duration(duration * i / ticks), Muted);
            context.DrawText(text, new Point(Math.Clamp(x - text.Width / 2, plot.Left, Math.Max(plot.Left, plot.Right - text.Width)), plot.Bottom + 12));
        }
        using (context.PushClip(plot))
        {
            foreach (var series in graph.Series)
                DrawSeries(context, series, X, Y);
        }
        // An aligned attempt strip provides fight context without obscuring comparison lines.
        foreach (var combat in graph.Snapshot.Combats)
        {
            var from = X((combat.Start - graph.Snapshot.Start).TotalSeconds);
            var to = X((combat.End - graph.Snapshot.Start).TotalSeconds);
            context.DrawRectangle(SessionReviewViewModel.OutcomeColor(combat.Outcome), null,
                new Rect(from, plot.Bottom + 3, Math.Max(1, to - from), 4));
        }
        DrawText(context, graph.Metric switch
        {
            SessionGraphMetric.DirectEhps => "Direct EHPS · excludes absorbs", SessionGraphMetric.Dtps => "DTPS",
            SessionGraphMetric.Health => "HP · observed", SessionGraphMetric.HealthPercent => "HP % · observed", _ => "DPS"
        }, new Point(Left, 1), Muted);
        if (graph.Series.Count == 0)
            DrawText(context, "Select players to compare", new Point(Left + 15, plot.Center.Y), Muted);
        else if (!graph.Series.Any(s => s.Points.Any(p => p.Value.HasValue)))
            DrawText(context, "No observations for these players in this view", new Point(Left + 15, plot.Center.Y), Muted);
        if (_pointerX is { } pointer && pointer >= plot.Left && pointer <= plot.Right)
            context.DrawLine(new Pen(Brush.Parse("#9FCAB3"), 1), new Point(pointer, plot.Top), new Point(pointer, plot.Bottom));
    }

    private static void DrawSeries(DrawingContext context, SessionGraphSeries series, Func<double, double> x, Func<double, double> y)
    {
        var brush = SessionReviewViewModel.SeriesColor(series.ColorIndex);
        var pen = new Pen(brush, 1.5);
        // Aggregate sub-pixel samples into a min/max envelope. Unknown samples terminate connections.
        Point? previous = null;
        double? column = null;
        double firstY = 0, lastY = 0, minY = 0, maxY = 0, endX = 0;
        void Flush()
        {
            if (column is not { } px) return;
            var first = new Point(px, firstY);
            if (previous is { } p) context.DrawLine(pen, p, first);
            context.DrawLine(pen, new Point(px, minY), new Point(px, maxY));
            context.DrawLine(pen, new Point(px, lastY), new Point(Math.Max(px + 1, endX), lastY));
            previous = new Point(Math.Max(px, endX), lastY);
            column = null;
        }
        foreach (var point in series.Points)
        {
            if (!point.Value.HasValue) { Flush(); previous = null; continue; }
            var px = Math.Floor(x(point.StartSeconds));
            var py = y(point.Value.Value);
            if (column != px)
            {
                Flush(); column = px; firstY = lastY = minY = maxY = py;
            }
            else { minY = Math.Min(minY, py); maxY = Math.Max(maxY, py); lastY = py; }
            endX = x(point.EndSeconds);
        }
        Flush();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var graph = Graph;
        var position = e.GetPosition(this);
        if (graph is null || !Plot.Contains(position)) { ClearPointer(); return; }
        _pointerX = position.X;
        var seconds = (position.X - Plot.Left) / Plot.Width * graph.Snapshot.ElapsedSeconds;
        var time = graph.Snapshot.Start.AddSeconds(seconds);
        var combat = graph.Snapshot.Combats.FirstOrDefault(c => time >= c.Start && time <= c.End);
        var lines = new List<string> { $"+{SessionPerformanceViewModel.Duration(seconds)} · {time:T}", combat is null ? "Between or excluded combats" : $"{combat.Name} · {combat.Outcome}" };
        foreach (var series in graph.Series)
        {
            var sample = FindSample(series.Points, seconds);
            var value = sample?.Value is { } number ? number.ToString(graph.Metric == SessionGraphMetric.HealthPercent ? "N1" : "N0") + (graph.Metric == SessionGraphMetric.HealthPercent ? "%" : "") : "—";
            lines.Add($"{series.Player}: {value}");
        }
        var tip = string.Join(Environment.NewLine, lines);
        if (tip != _tip) { _tip = tip; ToolTip.SetTip(this, tip); }
        InvalidateVisual();
    }

    private static SessionGraphPoint? FindSample(IReadOnlyList<SessionGraphPoint> points, double seconds)
    {
        var low = 0;
        var high = points.Count - 1;
        while (low <= high)
        {
            var middle = low + (high - low) / 2;
            var point = points[middle];
            if (point.EndSeconds <= seconds) low = middle + 1;
            else if (point.StartSeconds > seconds) high = middle - 1;
            else return point;
        }
        return null;
    }
    protected override void OnPointerExited(PointerEventArgs e) { base.OnPointerExited(e); ClearPointer(); }
    private void ClearPointer() { _pointerX = null; _tip = null; ToolTip.SetTip(this, null); InvalidateVisual(); }
    private static string Compact(double value) => value >= 1000000 ? $"{value / 1000000:0.#}m" : value >= 1000 ? $"{value / 1000:0.#}k" : value.ToString("0");
    private static FormattedText Text(string value, IBrush brush) => new(value, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Font, 10, brush);
    private static void DrawText(DrawingContext context, string value, Point point, IBrush brush) => context.DrawText(Text(value, brush), point);
}
