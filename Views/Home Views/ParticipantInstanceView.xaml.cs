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

namespace SWTORCombatParser.Views.Home_Views
{
    /// <summary>
    /// Interaction logic for ParticipantInstanceView.xaml
    /// </summary>
    public partial class ParticipantInstanceView : UserControl
    {
        private ParticipantViewModel _viewModel;
        private bool _isSelected = false;
        public ParticipantInstanceView()
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
