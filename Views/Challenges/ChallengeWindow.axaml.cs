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
    public partial class ChallengeWindow : UserControl
    {
        public ChallengeWindow(ChallengeWindowViewModel viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();
        }
    }
}
