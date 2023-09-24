using SWTORCombatParser.DataStructures;
using SWTORCombatParser.ViewModels.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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
            _vm = vm;
            DataContext = vm;
            Left = Application.Current.MainWindow.Left + (Application.Current.MainWindow.ActualWidth / 2) - (750 / 2d);
            Top = Application.Current.MainWindow.Top + (Application.Current.MainWindow.ActualHeight / 2) - (450 / 2d);
            TimerName.TextChanged += UpdateNameHelpText;
            EffectName.TextChanged += UpdateValueHelpText;
            AbilityName.TextChanged += UpdateValueHelpText;
            _vm.OnNewTimer += CloseWindow;
            CancelButton.Click += Cancel;
            VariableCheck.Click += CheckForForceToVisualsTab;
        }

        private void CheckForForceToVisualsTab(object sender, RoutedEventArgs e)
        {
            if (!VariableCheck.IsChecked.Value)
            {
                EffectsTabControl.SelectedIndex = 0;
            }
            if (VariableCheck.IsChecked.Value && EffectsTabControl.SelectedIndex == 0)
            {
                EffectsTabControl.SelectedIndex = 1;
            }
        }

        private void UpdateNameHelpText(object sender, TextChangedEventArgs e)
        {
            //if (!string.IsNullOrEmpty(TimerName.Text))
            //    TimerHelpText.Visibility = Visibility.Hidden;
            //else
            //    TimerHelpText.Visibility = Visibility.Visible;
        }
        private void UpdateValueHelpText(object sender, TextChangedEventArgs e)
        {
            //if (!string.IsNullOrEmpty(EffectName.Text) || !string.IsNullOrEmpty(AbilityName.Text))
            //    ValueHelpText.Visibility = Visibility.Hidden;
            //else
            //    ValueHelpText.Visibility = Visibility.Visible;
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

        private void SourceEntered(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
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
        public void DragWindow(object sender, MouseButtonEventArgs args)
        {
            DragMove();
        }
    }
}
