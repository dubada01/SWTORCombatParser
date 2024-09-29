using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.ViewModels.Challenges;
using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SWTORCombatParser.Views.Challenges
{
    /// <summary>
    /// Interaction logic for ChallengeWindow.xaml
    /// </summary>
    public partial class ChallengeWindow : BaseOverlayWindow
    {
        private ChallengeWindowViewModel _viewModel;
        public ChallengeWindow(ChallengeWindowViewModel viewModel):base(viewModel)
        {
            DataContext = viewModel;
            _viewModel = viewModel;
            InitializeComponent();
        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }
    }
}
