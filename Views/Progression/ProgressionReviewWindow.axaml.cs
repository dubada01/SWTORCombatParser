using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using SWTORCombatParser.Model.Progression;
using SWTORCombatParser.ViewModels.Progression;

namespace SWTORCombatParser.Views.Progression;

public partial class ProgressionReviewWindow : Window
{
    private readonly ProgressionReviewViewModel _viewModel;
    private string? _scrolledPull;

    public ProgressionReviewWindow(ProgressionReviewViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
        // Selection changes can replace another selector's ItemsSource. Keep that
        // cascade in the view model instead of round-tripping transient nulls.
        ProgressionScope.SelectionChanged += (_, _) =>
        {
            if (ProgressionScope.SelectedItem is ViewModels.Progression.ProgressionScope scope) _viewModel.Scope = scope;
        };
        RaidSelector.SelectionChanged += (_, _) => _viewModel.SelectedRaid = RaidSelector.SelectedItem as ProgressionRaidChoice;
        HistoryTeamSelector.SelectionChanged += (_, _) =>
        {
            if (HistoryTeamSelector.SelectedItem is string team) _viewModel.SelectedTeam = team;
        };
        CohortSelector.SelectionChanged += (_, _) => _viewModel.SelectedCohort = CohortSelector.SelectedItem as ProgressionCohort;
        BossSelector.SelectionChanged += (_, _) =>
        {
            if (BossSelector.SelectedItem is string boss) _viewModel.SelectedBoss = boss;
        };
        PullGrid.SelectionChanged += (_, _) => _viewModel.SelectedPull = PullGrid.SelectedItem as ProgressionPullRow;
        DeathGrid.SelectionChanged += (_, _) => _viewModel.SelectedDeath = DeathGrid.SelectedItem as ProgressionDeathRow;
        TeamNameBox.TextChanged += (_, _) => _viewModel.TeamName = TeamNameBox.Text ?? "";
        _viewModel.PropertyChanged += OnViewModelChanged;
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(ProgressionReviewViewModel.SelectedPull) || _viewModel.SelectedPull is not { } pull ||
            _scrolledPull == pull.Summary.Metadata.Id) return;
        _scrolledPull = pull.Summary.Metadata.Id;
        Dispatcher.UIThread.Post(() =>
        {
            if (IsVisible && _viewModel.SelectedPull is { } selected && selected.Summary.Metadata.Id == _scrolledPull)
                PullGrid.ScrollIntoView(selected, null);
        }, DispatcherPriority.Background);
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.PropertyChanged -= OnViewModelChanged;
        _viewModel.Dispose();
        base.OnClosed(e);
    }
}
