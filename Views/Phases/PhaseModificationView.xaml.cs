using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.Phases;
using SWTORCombatParser.Model.Timers;
using SWTORCombatParser.ViewModels.Phases;
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
using System.Windows.Shapes;

namespace SWTORCombatParser.Views.Phases
{
    /// <summary>
    /// Interaction logic for PhaseModificationView.xaml
    /// </summary>
    public partial class PhaseModificationView : Window
    {
        private PhaseModificationViewModel _vm;

        public PhaseModificationView(PhaseModificationViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            _vm = vm;
            Left = Application.Current.MainWindow.Left + (Application.Current.MainWindow.ActualWidth / 2) - (750 / 2d);
            Top = Application.Current.MainWindow.Top + (Application.Current.MainWindow.ActualHeight / 2) - (450 / 2d);
            _vm.OnNewPhase += CloseWindow;
            CancelButton.Click += Cancel;
        }
        private void CloseWindow(Phase throwAway)
        {
            Close();
        }
        private void Cancel(object sender, RoutedEventArgs e)
        {
            _vm.Cancel();
            Close();
        }

        public void DragWindow(object sender, MouseButtonEventArgs args)
        {
            DragMove();
        }
    }
}
