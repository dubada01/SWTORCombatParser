using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using SWTORCombatParser.ViewModels.Timers;
using RoutedEventArgs = Avalonia.Interactivity.RoutedEventArgs;
using TextChangedEventArgs = Avalonia.Controls.TextChangedEventArgs;
using Timer = SWTORCombatParser.DataStructures.Timer;

namespace SWTORCombatParser.Views.Timers
{
    /// <summary>
    /// Interaction logic for TimerModificationWindow.xaml
    /// </summary>
    public partial class TimerModificationWindow : Window
    {
        private ModifyTimerViewModel _vm;
        private bool _isDragging;
        private Point _startPoint;

        public TimerModificationWindow()
        {
            InitializeComponent();
            _vm = new ModifyTimerViewModel("Shared");
            DataContext = _vm;
        }
        public TimerModificationWindow(ModifyTimerViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            DataContext = vm;
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                Position = new PixelPoint((int)(desktop.MainWindow.Position.X + (desktop.MainWindow.Width / 2) - (750 / 2)), (int)(desktop.MainWindow.Position.Y + (desktop.MainWindow.Height / 2) - (450 / 2)));
            }
            TimerName.TextChanged += UpdateNameHelpText;
            EffectName.TextChanged += UpdateValueHelpText;
            AbilityName.TextChanged += UpdateValueHelpText;
            _vm.OnNewTimer += CloseWindow;
            CancelButton.Click += Cancel;
            VariableCheck.Click += CheckForForceToVisualsTab;
            Loaded += (sender, args) => { TabContent.Content = new VisualsTabContent(); };
        }

        private void CheckForForceToVisualsTab(object? sender, RoutedEventArgs routedEventArgs)
        {
            if (!VariableCheck.IsChecked.Value)
            {
                EffectsTabStrip.SelectedIndex = 0;
            }
            if (VariableCheck.IsChecked.Value && EffectsTabStrip.SelectedIndex == 0)
            {
                EffectsTabStrip.SelectedIndex = 1;
            }
        }

        private void UpdateNameHelpText(object? sender, Avalonia.Controls.TextChangedEventArgs textChangedEventArgs)
        {
            //if (!string.IsNullOrEmpty(TimerName.Text))
            //    TimerHelpText.Visibility = Visibility.Hidden;
            //else
            //    TimerHelpText.Visibility = Visibility.Visible;
        }
        private void UpdateValueHelpText(object? sender, TextChangedEventArgs textChangedEventArgs)
        {
            //if (!string.IsNullOrEmpty(EffectName.Text) || !string.IsNullOrEmpty(AbilityName.Text))
            //    ValueHelpText.Visibility = Visibility.Hidden;
            //else
            //    ValueHelpText.Visibility = Visibility.Visible;
        }
        private void CloseWindow(Timer throwAway, bool meh, bool megaMeh)
        {
            Close();
        }
        private void Cancel(object? sender, Avalonia.Interactivity.RoutedEventArgs routedEventArgs)
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
        public void StartDrag(object sender, PointerPressedEventArgs args)
        {
            _isDragging = true;
            _startPoint = args.GetPosition(this);
        }
        public void DragWindow(object sender, PointerEventArgs args)
        {
            if (_isDragging)
            {
                // Get the current scaling factor to adjust the movement correctly
                var scalingFactor = this.VisualRoot.RenderScaling;

                var currentPosition = args.GetPosition(this);
                var delta = (currentPosition - _startPoint) / scalingFactor;  // Adjust for DPI scaling

                // Move the window (or element) by the delta
                var currentPositionInScreen = this.Position;
                this.Position = new PixelPoint(
                    currentPositionInScreen.X + (int)delta.X,
                    currentPositionInScreen.Y + (int)delta.Y
                );
            }
        }
        public void StopDrag(object sender, PointerReleasedEventArgs args)
        {
            _isDragging = false;
        }

        private void EffectsTabStrip_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (TabContent == null)
                return;
            TabContent.Content = EffectsTabStrip.SelectedIndex switch
            {
                0 => new VisualsTabContent(),
                1 => new VariablesTabContent(),
                _ => TabContent.Content
            };
        }
    }
}
