using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using SWTORCombatParser.Model.RunRace;
using SWTORCombatParser.ViewModels.RunRace;

namespace SWTORCombatParser.Views.RunRace;

/// <summary>Two time-proportional lanes, rendered directly so the live clock never rebuilds a control tree.</summary>
public sealed class RunRaceTimelineControl : Control
{
    public static readonly StyledProperty<RunRaceSnapshot?> SnapshotProperty =
        AvaloniaProperty.Register<RunRaceTimelineControl, RunRaceSnapshot?>(nameof(Snapshot));

    private static readonly IBrush Muted = Brush.Parse("#879C96");
    private static readonly IBrush Soft = Brush.Parse("#BBCCC4");
    private static readonly IBrush Gold = Brush.Parse("#D9BB7B");
    private static readonly IBrush GoldFill = Brush.Parse("#685437");
    private static readonly IBrush Green = Brush.Parse("#7DE0AE");
    private static readonly IBrush GreenFill = Brush.Parse("#275A42");
    private static readonly IBrush Cyan = Brush.Parse("#8AE3EA");
    private static readonly IBrush CyanFill = Brush.Parse("#24545B");
    private static readonly IBrush Behind = Brush.Parse("#E5B482");
    private static readonly IBrush GridBrush = Brush.Parse("#283C35");
    private static readonly IBrush TrackBrush = Brush.Parse("#172921");
    private static readonly Typeface Font = new("Inter, Segoe UI, sans-serif");
    private readonly List<(Rect Bounds, string Text)> _hitTargets = new();
    private string? _tooltip;

    public RunRaceSnapshot? Snapshot { get => GetValue(SnapshotProperty); set => SetValue(SnapshotProperty, value); }
    static RunRaceTimelineControl() => AffectsRender<RunRaceTimelineControl>(SnapshotProperty);

    public RunRaceTimelineControl()
    {
        ClipToBounds = true;
        ToolTip.SetShowDelay(this, 150);
        ToolTip.SetBetweenShowDelay(this, 0);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        _hitTargets.Clear();
        var snapshot = Snapshot;
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width < 100 || height < 35) return;
        var left = width < 430 ? 38d : 50d;
        var right = width - 10;
        var plotWidth = Math.Max(1, right - left);
        var scaleY = Math.Min(1, height / 148d);
        var compressed = height < 105;
        var referenceY = compressed ? Math.Max(14, height * 0.27) : 35 * scaleY;
        var actualY = compressed ? Math.Max(referenceY + 16, height * 0.62) : 91 * scaleY;
        var laneHeight = compressed ? Math.Clamp(height * 0.14, 6, 10) : Math.Max(9, 16 * scaleY);
        var bottom = actualY + laneHeight + 3;
        var checkpoints = snapshot?.Checkpoints ?? Array.Empty<RaceCheckpoint>();
        var elapsed = Valid(snapshot?.ElapsedSeconds) ?? 0;
        var end = Math.Max(elapsed, Valid(snapshot?.ReferenceDurationSeconds) ?? 0);
        foreach (var checkpoint in checkpoints)
            end = Math.Max(end, Math.Max(Valid(checkpoint.Seconds) ?? 0, Valid(checkpoint.GhostSeconds) ?? 0));
        var step = TickStep(Math.Max(60, end) / Math.Max(2, plotWidth / 90));
        var horizon = Math.Max(step * 2, Math.Ceiling((end + Math.Max(5, end * 0.025)) / step) * step);
        double X(double seconds) => left + Math.Clamp(seconds / horizon, 0, 1) * plotWidth;

        using (context.PushClip(new Rect(Bounds.Size)))
        {
            context.DrawRectangle(TrackBrush, null, new Rect(left, referenceY - 2, plotWidth, laneHeight + 4), 3, 3);
            context.DrawRectangle(TrackBrush, null, new Rect(left, actualY - 2, plotWidth, laneHeight + 4), 3, 3);
            for (double seconds = 0; seconds <= horizon + 0.1; seconds += step)
            {
                var x = X(seconds);
                context.DrawLine(new Pen(GridBrush, 1), new Point(x, compressed ? 13 : 21 * scaleY), new Point(x, bottom));
                var label = Text(RunRaceViewModel.FormatTime(seconds), 10, Muted);
                context.DrawText(label, new Point(Math.Clamp(x - label.Width / 2, left, Math.Max(left, right - label.Width)), 2));
            }
            DrawText(context, "PACE", new Point(0, referenceY + 1), 9, Gold);
            DrawText(context, "YOU", new Point(0, actualY + 1), 9, Green);

            var referencePoints = checkpoints.Select((checkpoint, index) => (Checkpoint: checkpoint, Number: index + 1))
                .Where(item => Valid(item.Checkpoint.GhostSeconds).HasValue).OrderBy(item => item.Checkpoint.GhostSeconds).ToArray();
            for (var i = 0; i < referencePoints.Length; i++)
            {
                var (checkpoint, number) = referencePoints[i];
                var finish = Valid(checkpoint.GhostSeconds)!.Value;
                var start = Math.Min(finish, Valid(checkpoint.GhostStartSeconds) ?? finish);
                var rectangle = Interval(X(start), X(finish), referenceY, laneHeight);
                context.DrawRectangle(GoldFill, new Pen(Gold, 1), rectangle, 3, 3);
                DrawCheckpoint(context, X(finish), referenceY + laneHeight / 2, Gold, 3);
                DrawNumber(context, rectangle, number, Gold);

                var slotLeft = i == 0 ? left : (X(referencePoints[i - 1].Checkpoint.GhostSeconds!.Value) + X(finish)) / 2 + 3;
                var slotRight = i == referencePoints.Length - 1 ? right : (X(referencePoints[i + 1].Checkpoint.GhostSeconds!.Value) + X(finish)) / 2 - 3;
                var labelWidth = Math.Max(1, slotRight - slotLeft);
                var name = labelWidth >= 42 ? checkpoint.Boss : number.ToString("00");
                var label = Text(name, 10, Soft, labelWidth);
                var labelX = Math.Clamp(X(finish) - label.Width / 2, slotLeft, Math.Max(slotLeft, slotRight - label.Width));
                if (!compressed)
                {
                    context.DrawLine(new Pen(GoldFill, 1), new Point(X(finish), referenceY + laneHeight + 1), new Point(X(finish), referenceY + laneHeight + 6));
                    context.DrawText(label, new Point(labelX, referenceY + laneHeight + 8));
                }
                var tooltip = $"{number:00} · {checkpoint.Boss}\nReference: " + (checkpoint.GhostStartSeconds.HasValue
                    ? $"{Time(start)} → {Time(finish)} ({Time(finish - start)} combat)" : $"{Time(finish)} checkpoint");
                if (checkpoint.ReferenceSamples > 0) tooltip += $"\n{checkpoint.ReferenceSamples:N0} timing samples";
                _hitTargets.Add((new Rect(rectangle.X - 4, rectangle.Y - 4, rectangle.Width + 8, rectangle.Height + (compressed ? 8 : 25)), tooltip));
            }

            if (referencePoints.Length == 0)
                DrawText(context, snapshot?.Cohort == null ? "Waiting for an eligible run" : snapshot.ReferenceLoading ? "Loading reference timings…"
                        : snapshot.IsLeaderboardReference ? "No matching leaderboard timings available" : "No saved ghost for this route yet",
                    new Point(left + 8, referenceY + 2), 10, Muted, plotWidth - 16);

            var deltaLabels = new List<Rect>();
            foreach (var (checkpoint, index) in checkpoints.Select((checkpoint, index) => (checkpoint, index)).OrderByDescending(item => item.checkpoint.Seconds))
            {
                if (Valid(checkpoint.Seconds) is not { } finish) continue;
                var start = Math.Min(finish, Valid(checkpoint.StartSeconds) ?? finish);
                var rectangle = Interval(X(start), X(finish), actualY, laneHeight);
                var color = checkpoint.DeltaSeconds > 0 ? Behind : Green;
                context.DrawRectangle(GreenFill, new Pen(color, 1), rectangle, 3, 3);
                DrawCheckpoint(context, X(finish), actualY + laneHeight / 2, color, 3.5);
                DrawNumber(context, rectangle, index + 1, Green);
                var delta = checkpoint.DeltaSeconds.HasValue ? CompactDelta(checkpoint.DeltaSeconds.Value) : checkpoint.RouteMatches ? "✓" : "route";
                var label = Text(delta, 10, color);
                var labelX = Math.Clamp(X(finish) - label.Width / 2, left, Math.Max(left, right - label.Width));
                var labelBounds = new Rect(labelX, actualY + laneHeight + 10, label.Width, label.Height);
                if (!compressed && deltaLabels.All(bounds => !bounds.Intersects(labelBounds.Inflate(3))))
                {
                    context.DrawText(label, labelBounds.TopLeft);
                    deltaLabels.Add(labelBounds);
                }
                var tooltip = $"{index + 1:00} · {checkpoint.Boss}\nYou: " + (checkpoint.StartSeconds.HasValue
                    ? $"{Time(start)} → {Time(finish)} ({Time(finish - start)} combat)" : $"{Time(finish)} checkpoint") + $"\n{checkpoint.Attempts} attempts";
                if (checkpoint.DeltaSeconds.HasValue) tooltip += "\n" + RunRaceViewModel.FormatDelta(checkpoint.DeltaSeconds.Value);
                else if (!checkpoint.RouteMatches) tooltip += "\nDifferent preceding checkpoints; split is not comparable.";
                _hitTargets.Add((new Rect(rectangle.X - 4, rectangle.Y - 4, rectangle.Width + 8, rectangle.Height + (compressed ? 8 : 23)), tooltip));
            }

            if (!string.IsNullOrEmpty(snapshot?.ActiveBossName) && Valid(snapshot.ActiveBossStartSeconds) is { } activeStart && activeStart <= elapsed && !snapshot.IsComplete)
            {
                var rectangle = Interval(X(activeStart), X(elapsed), actualY, laneHeight);
                context.DrawRectangle(CyanFill, new Pen(Cyan, 1.5), rectangle, 3, 3);
                if (rectangle.Width > 48) DrawText(context, "IN COMBAT", new Point(rectangle.X + 5, rectangle.Y + 2), 8, Cyan, rectangle.Width - 10);
                _hitTargets.Add((rectangle.Inflate(4), $"{snapshot.ActiveBossName} · in progress\nStarted {Time(activeStart)} · {Time(elapsed - activeStart)} in combat"));
            }

            if (snapshot?.Cohort != null)
            {
                var cursor = X(elapsed);
                context.DrawLine(new Pen(Cyan, 1.5), new Point(cursor, compressed ? 12 : 22 * scaleY), new Point(cursor, bottom + 2));
                context.DrawEllipse(Cyan, null, new Point(cursor, compressed ? 12 : 23 * scaleY), 3, 3);
                _hitTargets.Add((new Rect(cursor - 5, 18 * scaleY, 10, bottom - 14 * scaleY),
                    $"{(snapshot.IsComplete ? "Finished" : snapshot.IsPaused ? "Stopped at" : snapshot.IsHistorical ? "Log time" : "Run time")}: {Time(elapsed)} from area entry"));
            }
            if (height >= 145) DrawText(context, "Boss intervals · gaps include travel & downtime", new Point(left, height - 12), 9, Muted, plotWidth);
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        var hit = _hitTargets.LastOrDefault(target => target.Bounds.Contains(point));
        var tooltip = hit.Text;
        if (tooltip == _tooltip) return;
        _tooltip = tooltip;
        ToolTip.SetTip(this, tooltip);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _tooltip = null;
        ToolTip.SetTip(this, null);
    }

    private static Rect Interval(double from, double to, double y, double height) => new(from, y, Math.Max(2, to - from), height);
    private static void DrawCheckpoint(DrawingContext context, double x, double y, IBrush color, double radius) =>
        context.DrawEllipse(color, null, new Point(x, y), radius, radius);
    private static void DrawNumber(DrawingContext context, Rect interval, int number, IBrush color)
    {
        if (interval.Width < 20 || interval.Height < 10) return;
        var text = Text(number.ToString("00"), 9, color);
        context.DrawText(text, new Point(interval.X + (interval.Width - text.Width) / 2, interval.Y + (interval.Height - text.Height) / 2));
    }
    private static void DrawText(DrawingContext context, string value, Point at, double size, IBrush color, double maxWidth = double.PositiveInfinity) =>
        context.DrawText(Text(value, size, color, maxWidth), at);
    private static FormattedText Text(string value, double size, IBrush color, double maxWidth = double.PositiveInfinity)
    {
        var text = new FormattedText(value, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, Font, size, color);
        if (double.IsFinite(maxWidth)) { text.MaxTextWidth = Math.Max(1, maxWidth); text.MaxTextHeight = size + 5; text.Trimming = TextTrimming.CharacterEllipsis; }
        return text;
    }
    private static double? Valid(double? seconds) => seconds.HasValue && double.IsFinite(seconds.Value) && seconds.Value >= 0 && seconds.Value <= TimeSpan.MaxValue.TotalSeconds - 1
        ? seconds : null;
    private static string Time(double seconds) => RunRaceViewModel.FormatTime(seconds);
    private static string CompactDelta(double delta) => Math.Abs(delta) < 0.5 ? "±0:00" : (delta < 0 ? "−" : "+") + Time(Math.Abs(delta));
    private static double TickStep(double desired)
    {
        foreach (var step in new[] { 15d, 30, 60, 120, 300, 600, 900, 1800, 3600, 7200, 14400, 28800 })
            if (step >= desired) return step;
        return Math.Ceiling(desired / 3600) * 3600;
    }
}
