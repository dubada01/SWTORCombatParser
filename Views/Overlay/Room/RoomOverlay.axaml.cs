using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
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
        private Dictionary<string, Ellipse> _currentHazards = new Dictionary<string, Ellipse>();
        public RoomOverlay(BaseOverlayViewModel viewmodel)
        {
            DataContext = viewmodel;
            InitializeComponent();
        }

        
        internal void DrawCharacter(double xFraction, double yFraction, double facing)
        {
            Dispatcher.UIThread.Invoke(() =>
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
        private static Rect GetBoundingBox(Control child, Control parent)
        {
            var transform = child.TransformToVisual(parent);
            var topLeft = transform.Value.Transform(new Point(0, 0));
            var bottomRight = transform.Value.Transform(new Point(child.Bounds.Width, child.Bounds.Height));
            return new Rect(topLeft, bottomRight);
        }
        internal void DrawHazard(double xFraction, double yFraction, double widthFraction, string hazardId)
        {
            Dispatcher.UIThread.Invoke(() =>
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
            Dispatcher.UIThread.Invoke(() =>
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
