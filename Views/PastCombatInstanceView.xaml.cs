using SWTORCombatParser.Model.CombatParsing;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SWTORCombatParser.Views
{
    /// <summary>
    /// Interaction logic for PastCombatInstanceView.xaml
    /// </summary>
    public partial class PastCombatInstanceView : UserControl
    {
        public PastCombatInstanceView()
        {
            InitializeComponent();
        }

        private void Border_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var viewModel = DataContext as PastCombat;
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                viewModel.AdditiveSelectionToggle();
            }
            else
            {
                viewModel.SelectionToggle();
            }
        }
    }
}
