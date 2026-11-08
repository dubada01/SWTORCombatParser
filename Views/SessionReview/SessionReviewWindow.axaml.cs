using System;
using Avalonia.Controls;
using SWTORCombatParser.Model.SessionReview;
using SWTORCombatParser.ViewModels.SessionReview;

namespace SWTORCombatParser.Views.SessionReview;

public partial class SessionReviewWindow : Window
{
    private readonly SessionReviewViewModel _viewModel;
    public SessionReviewWindow(SessionReviewService service)
    {
        InitializeComponent();
        _viewModel = new SessionReviewViewModel(service);
        DataContext = _viewModel;
        Closed += OnClosed;
    }
    private void OnClosed(object? sender, EventArgs args)
    {
        Closed -= OnClosed;
        _viewModel.Dispose();
    }
}
