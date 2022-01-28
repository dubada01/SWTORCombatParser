using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.Timers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
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
            this.Left = Application.Current.MainWindow.Left + (Application.Current.MainWindow.ActualWidth/2) - (750/2d);
            this.Top = Application.Current.MainWindow.Top + (Application.Current.MainWindow.ActualHeight/2) - (450/2d);
            TimerName.TextChanged += UpdateNameHelpText;
            EffectName.TextChanged += UpdateValueHelpText;
            AbilityName.TextChanged += UpdateValueHelpText;
            _vm.OnNewTimer += CloseWindow;
            CancelButton.Click += Cancel;
            
        }
        
        private void UpdateNameHelpText(object sender, TextChangedEventArgs e)
        {
            if (!string.IsNullOrEmpty(TimerName.Text))
                TimerHelpText.Visibility = Visibility.Hidden;
            else
                TimerHelpText.Visibility = Visibility.Visible;
        }
        private void UpdateValueHelpText(object sender, TextChangedEventArgs e)
        {
            if (!string.IsNullOrEmpty(EffectName.Text) || !string.IsNullOrEmpty(AbilityName.Text))
                ValueHelpText.Visibility = Visibility.Hidden;
            else
                ValueHelpText.Visibility = Visibility.Visible;
        }
        private void CloseWindow(Timer throwAway, bool meh)
        {
            Close();
            ObscureWindowFactory.CloseObscureWindow();
        }
        private void Cancel(object sender, RoutedEventArgs e)
        {
            _vm.Cancel();
            ObscureWindowFactory.CloseObscureWindow();
            Close();
        }

        private void SourceEntered(object sender, KeyEventArgs e)
        {
            if(e.Key == Key.Enter)
            {
                _vm.SaveSource();
            }
        }
        private void TargetEntered(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                _vm.SaveTarget();
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _vm.Cancel();
        }
    }
}
