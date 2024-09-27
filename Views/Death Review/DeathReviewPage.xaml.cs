﻿using SWTORCombatParser.ViewModels.Death_Review;
using System.Windows.Controls;

namespace SWTORCombatParser.Views
{
    /// <summary>
    /// Interaction logic for DeathReviewPage.xaml
    /// </summary>
    public partial class DeathReviewPage : UserControl
    {
        public DeathReviewPage(DeathReviewViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
