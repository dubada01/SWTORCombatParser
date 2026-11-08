using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SWTORCombatParser.Model.RunRace;
using SWTORCombatParser.ViewModels.RunRace;

namespace SWTORCombatParser.Views.RunRace;

public partial class RunRaceWindow : Window
{
    private readonly RunRaceViewModel _viewModel;
    public event Action? LoadPreviousLogRequested;
    public RunRaceWindow(RunRaceService service)
    {
        InitializeComponent();
        _viewModel = new RunRaceViewModel(service);
        DataContext = _viewModel;
        Closed += OnClosed;
    }
    private void LoadLog_Click(object? sender, RoutedEventArgs e) => LoadPreviousLogRequested?.Invoke();
    private void OnClosed(object? sender, EventArgs args)
    {
        Closed -= OnClosed;
        LoadPreviousLogRequested = null;
        _viewModel.Dispose();
    }
}
