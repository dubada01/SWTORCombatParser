using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using System.Windows.Input;
using SWTORCombatParser.Model.Phases;
using SWTORCombatParser.ViewModels.Phases;

namespace SWTORCombatParser.Views.Phases;

public partial class PhaseModificationView : Window
{
    private readonly PhaseModificationViewModel _viewModel;
    private bool _saved;

    public PhaseModificationView(PhaseModificationViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        _viewModel = viewModel;
        _viewModel.OnNewPhase += CloseAfterSave;
        CancelButton.Click += Cancel;
        Closed += (_, _) =>
        {
            _viewModel.OnNewPhase -= CloseAfterSave;
            if (!_saved) _viewModel.Cancel();
        };
    }

    private void CloseAfterSave(Phase phase)
    {
        _saved = true;
        Close();
    }

    private void Cancel(object? sender, RoutedEventArgs args) => Close();

    private void AddDraftOnEnter(object? sender, KeyEventArgs args)
    {
        if (args.Key != Key.Enter || sender is not TextBox { Tag: ICommand command }) return;
        args.Handled = true;
        if (command.CanExecute(null)) command.Execute(null);
    }
}
