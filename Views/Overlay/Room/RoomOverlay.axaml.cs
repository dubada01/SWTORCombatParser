using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.ViewModels.Overlays.Room;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SWTORCombatParser.Views.Overlay.Room
{
    /// <summary>
    /// Interaction logic for RoomOverlay.xaml
    /// </summary>
    public partial class RoomOverlay : Window
    {
        private RoomOverlayViewModel viewModel;
        private bool _loaded;
        private Dictionary<string, Ellipse> _currentHazards = new Dictionary<string, Ellipse>();
        public RoomOverlay(RoomOverlayViewModel viewmodel)
        {
            viewModel = viewmodel;
            DataContext = viewmodel;
            InitializeComponent();
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Close,
    new ExecutedRoutedEventHandler(delegate (object sender, ExecutedRoutedEventArgs args) { this.Close(); })));
            MainWindowClosing.Closing += CloseOverlay;
            viewmodel.OnLocking += makeTransparent;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _loaded = true;
            var imageLocation = GetBoundingBox(RoomImage, ReferenceInfo);
            CharImage.Height = imageLocation.Width * 0.1;
            CharImage.Width = imageLocation.Width * 0.1;
            RemoveFromAppWindow();
        }

        private void RemoveFromAppWindow()
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, (extendedStyle | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW);
        }
        public const int WS_EX_TRANSPARENT = 0x00000020;
        public const int GWL_EXSTYLE = (-20);
        private const int WS_EX_APPWINDOW = 0x00040000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        public void makeTransparent(bool shouldLock)
        {
            if (!_loaded)
                return;
            Dispatcher.Invoke(() =>
            {
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                if (shouldLock)
                {
                    BackgroundArea.Opacity = 0.75f;
                    int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                    SetWindowLong(hwnd, GWL_EXSTYLE, (extendedStyle | WS_EX_TRANSPARENT) & ~WS_EX_APPWINDOW);
                }
                else
                {
                    BackgroundArea.Opacity = 0.45f;
                    //Remove the WS_EX_TRANSPARENT flag from the extended window style
                    int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                    SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle & ~WS_EX_TRANSPARENT);

                }
            });

        }
        private void CloseOverlay()
        {
            Dispatcher.Invoke(() =>
            {
                Hide();
            });

        }
        public void DragWindow(object sender, MouseButtonEventArgs args)
        {
            DragMove();
        }
        public void UpdateDefaults(object sender, MouseButtonEventArgs args)
        {
            DefaultRoomOverlayManager.SetDefaults(new Point() { X = Left, Y = Top }, new Point() { X = Width, Y = Height });
        }

        private void Thumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            var yadjust = Height + e.VerticalChange;
            var xadjust = Width + e.HorizontalChange;
            if (xadjust > 0)
                SetValue(WidthProperty, xadjust);
            if (yadjust > 0)
                SetValue(HeightProperty, yadjust);
        }

        private void Window_MouseLeave(object sender, MouseEventArgs e)
        {
            DefaultRoomOverlayManager.SetDefaults(new Point() { X = Left, Y = Top }, new Point() { X = Width, Y = Height });
            Mouse.OverrideCursor = Cursors.Arrow;
        }

        private void Thumb_MouseEnter(object sender, MouseEventArgs e)
        {
            if (viewModel.OverlaysMoveable)
            {
                Mouse.OverrideCursor = Cursors.SizeNWSE;
            }
        }

        private void Border_MouseEnter(object sender, MouseEventArgs e)
        {
            if (viewModel.OverlaysMoveable)
            {
                Mouse.OverrideCursor = Cursors.Hand;
            }
        }

        private void Grid_MouseLeave(object sender, MouseEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Arrow;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            CloseOverlay();
        }

        internal void DrawCharacter(double xFraction, double yFraction, double facing)
        {
            Dispatcher.Invoke(() =>
            {
                var imageLocation = GetBoundingBox(RoomImage, ReferenceInfo);
                Point characterLocation = new Point((imageLocation.Width * xFraction) + imageLocation.X, (imageLocation.Height * yFraction) + imageLocation.Y);
                CharImage.Height = imageLocation.Width * 0.066;
                CharImage.Width = imageLocation.Width * 0.066;
                Canvas.SetLeft(CharImage, characterLocation.X - (CharImage.Width / 2));
                Canvas.SetTop(CharImage, characterLocation.Y - (CharImage.Height / 2));

                var Rotation = new RotateTransform(90, imageLocation.Width / 2, imageLocation.Height / 2);
                ReferenceInfo.RenderTransform = Rotation;

                var rotationTransform = new RotateTransform(facing * -1, CharImage.Width / 2, CharImage.Height / 2);
                CharImage.RenderTransform = rotationTransform;
            });


        }
        private static Rect GetBoundingBox(FrameworkElement child, FrameworkElement parent)
        {
            GeneralTransform transform = child.TransformToAncestor(parent);
            Point topLeft = transform.Transform(new Point(0, 0));
            Point bottomRight = transform.Transform(new Point(child.ActualWidth, child.ActualHeight));
            return new Rect(topLeft, bottomRight);
        }

        internal void DrawHazard(double xFraction, double yFraction, double widthFraction, string hazardId)
        {
            Dispatcher.Invoke(() =>
            {

                var imageLocation = GetBoundingBox(RoomImage, ReferenceInfo);
                Point characterLocation = new Point((imageLocation.Width * xFraction) + imageLocation.X, (imageLocation.Height * yFraction) + imageLocation.Y);
                var newHazard = new Ellipse();
                newHazard.Fill = widthFraction > 0.06 ? Brushes.Pink : Brushes.CornflowerBlue;
                newHazard.Height = imageLocation.Width * widthFraction;
                newHazard.Width = imageLocation.Width * widthFraction;
                Canvas.SetLeft(newHazard, characterLocation.X - (newHazard.Width / 2));
                Canvas.SetTop(newHazard, characterLocation.Y - (newHazard.Height / 2));
                // Add the Ellipse directly to the Canvas
                ImageCanvas.Children.Add(newHazard);
                _currentHazards[hazardId] = newHazard;
            });
        }
        internal void ClearAllHazards()
        {
            Dispatcher.Invoke(() =>
            {
                foreach (var hazard in _currentHazards)
                {
                    ImageCanvas.Children.Remove(hazard.Value);
                }
            });
            _currentHazards.Clear();
        }
        internal void ClearSpecificHazard(string hazardId)
        {
            Dispatcher.Invoke(() => {
                Ellipse hazard;
                if(_currentHazards.TryGetValue(hazardId, out hazard))
                {
                    ImageCanvas.Children.Remove(hazard);
                }
            });
            _currentHazards.Remove(hazardId);
        }
    }
}
