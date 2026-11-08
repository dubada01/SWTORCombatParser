using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using SWTORCombatParser.DataStructures.SessionReview;
using SWTORCombatParser.ViewModels.SessionReview;

namespace SWTORCombatParser.Views.SessionReview;

/// <summary>A time-proportional strip. Resizing redraws it without rebuilding a canvas tree.</summary>
public sealed class SessionTimelineControl : Control
{
    public static readonly StyledProperty<SessionReviewSnapshot?> SnapshotProperty =
        AvaloniaProperty.Register<SessionTimelineControl, SessionReviewSnapshot?>(nameof(Snapshot));
    public SessionReviewSnapshot? Snapshot { get => GetValue(SnapshotProperty); set => SetValue(SnapshotProperty, value); }
    static SessionTimelineControl() => AffectsRender<SessionTimelineControl>(SnapshotProperty);

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.DrawRectangle(Brush.Parse("#28382F"), null, new Rect(Bounds.Size), 4, 4);
        var snapshot = Snapshot;
        if (snapshot is null || snapshot.ElapsedSeconds <= 0) return;
        using (context.PushClip(new Rect(Bounds.Size)))
        {
            foreach (var combat in snapshot.Combats)
            {
                var left = (combat.Start - snapshot.Start).TotalSeconds / snapshot.ElapsedSeconds * Bounds.Width;
                var width = Math.Max(2, combat.DurationSeconds / snapshot.ElapsedSeconds * Bounds.Width);
                context.DrawRectangle(SessionReviewViewModel.OutcomeColor(combat.Outcome), null,
                    new Rect(left, 3, width, Math.Max(0, Bounds.Height - 6)), 2, 2);
            }
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var snapshot = Snapshot;
        if (snapshot is null || Bounds.Width <= 0) return;
        var seconds = e.GetPosition(this).X / Bounds.Width * snapshot.ElapsedSeconds;
        var time = snapshot.Start.AddSeconds(seconds);
        foreach (var combat in snapshot.Combats)
        {
            if (time < combat.Start || time > combat.End) continue;
            ToolTip.SetTip(this, $"{combat.Name} · {combat.Outcome} · {SessionPerformanceViewModel.Duration(combat.DurationSeconds)}");
            return;
        }
        ToolTip.SetTip(this, "Out of combat");
    }
}
