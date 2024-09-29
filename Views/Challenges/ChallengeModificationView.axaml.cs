using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.ViewModels.Challenges;


namespace SWTORCombatParser.Views.Challenges
{
    /// <summary>
    /// Interaction logic for ChallengeModificationView.xaml
    /// </summary>
    public partial class ChallengeModificationView : Window
    {
        ChallengeModificationViewModel _vm;
        private bool _isDragging;
        private Point _startPoint;

        public ChallengeModificationView(ChallengeModificationViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            _vm = vm;
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                Position = new PixelPoint((int)(desktop.MainWindow.Position.X + (desktop.MainWindow.Width / 2) - (750 / 2)), (int)(desktop.MainWindow.Position.Y + (desktop.MainWindow.Height / 2) - (450 / 2)));
            }
            _vm.OnNewChallenge += CloseWindow;
            CancelButton.Click += Cancel;
        }
        private void CloseWindow(Challenge throwAway, bool meh)
        {
            Close();
        }
        private void Cancel(object sender, RoutedEventArgs e)
        {
            _vm.Cancel();
            Close();
        }

        public void StartDrag(object sender, PointerPressedEventArgs args)
        {
            _isDragging = true;
            _startPoint = args.GetPosition(this);
        }

        private void StopDrag(object? sender, PointerReleasedEventArgs e)
        
        {
            _isDragging = false;
        }

        private void Drag(object? sender, PointerEventArgs args)
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
    }
}
