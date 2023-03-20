using SWTORCombatParser.ViewModels.Home_View_Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SWTORCombatParser.Views.Death_Review
{
    /// <summary>
    /// Interaction logic for DeathPlayerInstance.xaml
    /// </summary>
    public partial class DeathPlayerInstance : UserControl
    {
        private ParticipantViewModel _viewModel;
        private bool _isSelected;

        public DeathPlayerInstance()
        {
            InitializeComponent();
        }
        private void Border_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _viewModel = DataContext as ParticipantViewModel;
            _isSelected = !_isSelected;
            _viewModel.ToggleSelection();
        }
    }
}
