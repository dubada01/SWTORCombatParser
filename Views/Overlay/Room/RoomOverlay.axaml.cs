using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using SWTORCombatParser.ViewModels;

namespace SWTORCombatParser.Views.Overlay.Room
{
    /// <summary>
    /// Interaction logic for RoomOverlay.xaml
    /// </summary>
    public partial class RoomOverlay : UserControl
    {
        private bool _loaded;
        private Dictionary<long, Ellipse> _currentHazards = new Dictionary<long, Ellipse>();
        public RoomOverlay(BaseOverlayViewModel viewmodel)
        {
            DataContext = viewmodel;
            InitializeComponent();
            Loaded += SetCharSize;
        }

        private void SetCharSize(object? sender, RoutedEventArgs e)
        {
            CharImage.Height = 50;
            CharImage.Width = 50;
        }

        internal void DrawCharacter(double xFraction, double yFraction, double facing)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                var imageBounds = RoomImage.Bounds;
                var transformToVisual = RoomImage.TransformToVisual(ImageCanvas);
                var visualOffset = transformToVisual?.Transform(new Point(0,0)) ?? default;

                // Get DPI scale (1.0 = 96 DPI)
                var scale = VisualRoot?.RenderScaling ?? 1.0;

                // Apply scale to layout-related dimensions if necessary
                var scaledWidth = imageBounds.Width * scale;
                var scaledHeight = imageBounds.Height * scale;
                var scaledOffsetX = visualOffset.X * scale;
                var scaledOffsetY = visualOffset.Y * scale;

                var characterX = (scaledWidth * xFraction) + scaledOffsetX;
                var characterY = (scaledHeight * yFraction) + scaledOffsetY;

                var characterSize = scaledWidth * 0.066;
                CharImage.Width = characterSize / scale;  // Convert back to layout units
                CharImage.Height = characterSize / scale;

                Canvas.SetLeft(CharImage, (characterX - (characterSize / 2)) / scale);
                Canvas.SetTop(CharImage, (characterY - (characterSize / 2)) / scale);

                CharImage.RenderTransform = new RotateTransform(-facing, CharImage.Width / 2, CharImage.Height / 2);
                CharImage.RenderTransformOrigin = RelativePoint.TopLeft;
            });
        }
        private static Rect GetBoundingBox(Control child, Control parent)
        {
            var transform = child.TransformToVisual(parent);
            var topLeft = transform.Value.Transform(new Point(0, 0));
            var bottomRight = transform.Value.Transform(new Point(child.Bounds.Width, child.Bounds.Height));
            return new Rect(topLeft, bottomRight);
        }
        internal void DrawHazard(double xFraction, double yFraction, double widthFraction, long hazardId)
        {
            Dispatcher.UIThread.Invoke(() =>
            {

                var imageLocation = GetBoundingBox(RoomImage, ImageCanvas);
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
            Dispatcher.UIThread.Invoke(() =>
            {
                foreach (var hazard in _currentHazards)
                {
                    ImageCanvas.Children.Remove(hazard.Value);
                }
            });
            _currentHazards.Clear();
        }
        internal void ClearSpecificHazard(long hazardId)
        {
            Dispatcher.UIThread.Invoke(() => {
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
