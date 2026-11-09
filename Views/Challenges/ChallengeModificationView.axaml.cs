using Avalonia.Controls;
using Avalonia.Interactivity;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.ViewModels.Challenges;

namespace SWTORCombatParser.Views.Challenges;

public partial class ChallengeModificationView : Window
{
    private readonly ChallengeModificationViewModel _viewModel;
    private bool _saved;

    public ChallengeModificationView(ChallengeModificationViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        _viewModel = viewModel;
        _viewModel.OnNewChallenge += CloseAfterSave;
        CancelButton.Click += Cancel;
        Closed += (_, _) =>
        {
            _viewModel.OnNewChallenge -= CloseAfterSave;
            if (!_saved) _viewModel.Cancel();
        };
    }

    private void CloseAfterSave(Challenge challenge, bool wasEditing)
    {
        _saved = true;
        Close();
    }

    private void Cancel(object? sender, RoutedEventArgs args) => Close();
}
