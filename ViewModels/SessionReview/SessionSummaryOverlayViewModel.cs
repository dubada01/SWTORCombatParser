using System;
using SWTORCombatParser.Model.SessionReview;
using SWTORCombatParser.Views.SessionReview;

namespace SWTORCombatParser.ViewModels.SessionReview;

public sealed class SessionSummaryOverlayViewModel : BaseOverlayViewModel, IDisposable
{
    private readonly SessionReviewService _service;
    private IDisposable? _lease;
    public SessionPerformanceViewModel Summary { get; } = new();
    public override bool ShouldBeVisible => Summary.HasData;
    public override string OverlayDisplayName => "Session Summary";

    public SessionSummaryOverlayViewModel(SessionReviewService service) : base("SessionSummary")
    {
        _service = service;
        _service.Changed += Update;
        ActiveChanged += OnActiveChanged;
        MainContent = new SessionSummaryOverlayView { DataContext = this };
        OnActiveChanged(Active);
        Update();
    }

    private void OnActiveChanged(bool active)
    {
        if (active) _lease ??= _service.Acquire();
        else { _lease?.Dispose(); _lease = null; }
    }
    private void Update()
    {
        Summary.Update(_service.Snapshot);
        UpdateVisibility();
    }
    public void Dispose()
    {
        _service.Changed -= Update;
        ActiveChanged -= OnActiveChanged;
        _lease?.Dispose();
        _lease = null;
        UnsubscribeFromLogEvents();
        _overlayWindow?.Close();
    }
}
