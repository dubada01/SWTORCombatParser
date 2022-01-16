using SWTORCombatParser.DataStructures;
using SWTORCombatParser.ViewModels.Timers;
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

namespace SWTORCombatParser.Views.Timers
{
    /// <summary>
    /// Interaction logic for TimerModificationWindow.xaml
    /// </summary>
    public partial class TimerModificationWindow : Window
    {
        private ModifyTimerViewModel _vm;
        public TimerModificationWindow(ModifyTimerViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            _vm = vm;
            _vm.OnNewTimer += CloseWindow;
            CancelButton.Click += Cancel;
        }
        private void CloseWindow(Timer throwAway, bool meh)
        {
            Close();
        }
        private void Cancel(object sender, RoutedEventArgs e)
        {
            _vm.Cancel();
            Close();
        }
    }
}
