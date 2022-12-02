using Google.Type;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.ViewModels.Overlays.PvP;
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
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SWTORCombatParser.Views.Overlay.PvP
{
    /// <summary>
    /// Interaction logic for MiniMapView.xaml
    /// </summary>
    public partial class MiniMapView : Window
    {
        private List<OpponentMapIcon> opponentImages => new List<OpponentMapIcon> { Op1, Op2, Op3, Op4, Op5, Op6, Op7, Op8 };
        private MiniMapViewModel viewModel;
        public MiniMapView(MiniMapViewModel vm)
        {
            viewModel = vm;
            DataContext = vm;
            InitializeComponent();
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Close,
    new ExecutedRoutedEventHandler(delegate (object sender, ExecutedRoutedEventArgs args) { this.Close(); })));
            MainWindowClosing.Closing += CloseOverlay;
            vm.OnLocking += makeTransparent;

            Loaded += OnLoaded;
        }
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
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
        private const int WS_EX_APPWINDOW = 0x00040000, WS_EX_TOOLWINDOW = 0x00000080;

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        public void makeTransparent(bool shouldLock)
        {
            Dispatcher.Invoke(() => {
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                if (shouldLock)
                {
                    Background.Opacity = 0.1f;
                    int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                    SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT);
                }
                else
                {
                    Background.Opacity = 0.45f;
                    //Remove the WS_EX_TRANSPARENT flag from the extended window style
                    int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                    SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle & ~WS_EX_TRANSPARENT);

                }
            });

        }
        private void CloseOverlay()
        {
            Dispatcher.Invoke(() => {
                Close();
            });

        }

        public void DragWindow(object sender, MouseButtonEventArgs args)
        {
            DragMove();
        }
        public void UpdateDefaults(object sender, MouseButtonEventArgs args)
        {
            DefaultGlobalOverlays.SetDefault("PvP_MiniMap", new Point() { X = Left, Y = Top }, new Point() { X = Width, Y = Height });
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
            DefaultGlobalOverlays.SetDefault("PvP_MiniMap", new Point() { X = Left, Y = Top }, new Point() { X = Width, Y = Height });
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
            DefaultGlobalOverlays.SetActive("PvP_MiniMap", false);
            CloseOverlay();
        }
        internal void UpdateCharacter(double facing)
        {
            Dispatcher.Invoke(() => {
                var imageLocation = GetBoundingBox(Arena, ImageCanvas);
                Point characterLocation = new Point((imageLocation.Width * .5) + imageLocation.X, (imageLocation.Height * .5) + imageLocation.Y);
                CharImage.Height = imageLocation.Width * 0.1;
                CharImage.Width = imageLocation.Width * 0.1;
                Canvas.SetLeft(CharImage, characterLocation.X - (CharImage.Width / 2));
                Canvas.SetTop(CharImage, characterLocation.Y - (CharImage.Height / 2));

                //var Rotation = new RotateTransform(90, imageLocation.Width / 2, imageLocation.Height / 2);
                //ImageCanvas.RenderTransform = Rotation;

                var rotationTransform = new RotateTransform(facing * -1, CharImage.Width / 2, CharImage.Height / 2);
                CharImage.RenderTransform = rotationTransform;
            });
        }
        
        internal void AddOpponents(List<OpponentMapInfo> opponentInfos, PositionData localPosition, double range, int rangeBuffer)
        {
            var opponentIndex = 0;
            HideAllOpponents();
            Dispatcher.Invoke(() => {
                var imageLocation = GetBoundingBox(Arena, ImageCanvas);

                var doubleRange = range * 2;

                var imageWidthGameUnits = Math.Max(20, doubleRange + rangeBuffer);
                var imageHeightGameUnits = Math.Max(20, doubleRange + rangeBuffer);
                RangeIndicator.Width =imageLocation.Width * (doubleRange / imageWidthGameUnits);
                RangeIndicator.Height =imageLocation.Height * (doubleRange / imageHeightGameUnits);
                Canvas.SetLeft(RangeIndicator, imageLocation.Width / 2 - (RangeIndicator.Width / 2));
                Canvas.SetTop(RangeIndicator, imageLocation.Height / 2 - (RangeIndicator.Height / 2));
                foreach (var opponent in opponentInfos) {
                    var img = opponentImages[opponentIndex];
                    img.Icon.Source = opponent.IsTarget ?
                    new BitmapImage(new Uri(System.IO.Path.Combine(Environment.CurrentDirectory, "resources/RoomOverlays/TargetedOpponentLocation.png"))) :
                    new BitmapImage(new Uri(System.IO.Path.Combine(Environment.CurrentDirectory, "resources/RoomOverlays/OpponentLocation.png")));
                    if (opponent.Menace != MenaceTypes.None)
                    { 
                        img.MenaceIcon.Visibility = Visibility.Visible;
                        img.MenaceIcon.Source = new BitmapImage(GetUriFromMenaceType(opponent.Menace));
                    }
                    else
                        img.MenaceIcon.Visibility = Visibility.Hidden;

                    img.PlayerName.Text = opponent.Name;
                    img.Visibility = Visibility.Visible;
                    

                    var trueXDistance = opponent.Position.X- localPosition.X;
                    var trueYDistance = opponent.Position.Y - localPosition.Y;

                    var xFraction = trueXDistance / imageWidthGameUnits;
                    var yFraction = trueYDistance / imageHeightGameUnits;

                    
                    Point characterLocation = new Point((imageLocation.Width * xFraction) + imageLocation.Width/2, (imageLocation.Height * yFraction) + imageLocation.Height/2);
                    img.Height = imageLocation.Height * 0.1;
                    img.Width = img.Height;
                    Canvas.SetLeft(img, characterLocation.X - (img.Width / 2));
                    Canvas.SetTop(img, characterLocation.Y - (img.Width / 2));

                    //var Rotation = new RotateTransform(90, imageLocation.Width / 2, imageLocation.Height / 2);
                    //ImageCanvas.RenderTransform = Rotation;

                    var rotationTransform = new RotateTransform(opponent.Position.Facing * -1, 0, 0);
                    img.Icon.RenderTransform = rotationTransform;
                    opponentIndex++;
                }
            });
        }

        private Uri GetUriFromMenaceType(MenaceTypes menace)
        {
            if (menace == MenaceTypes.None)
                return new Uri(System.IO.Path.Combine(Environment.CurrentDirectory, "resources/RoomOverlays/OpponentLocation.png"));
            if (menace == MenaceTypes.Dps)
                return new Uri(System.IO.Path.Combine(Environment.CurrentDirectory, "resources/dpsIcon.png"));
            if (menace == MenaceTypes.Healer)
                return new Uri(System.IO.Path.Combine(Environment.CurrentDirectory, "resources/healingIcon.png"));
            return new Uri(System.IO.Path.Combine(Environment.CurrentDirectory, "resources/question-mark.png"));
        }

        private void HideAllOpponents()
        {
            opponentImages.ForEach(o => o.Visibility = Visibility.Hidden);
        }
        private static Rect GetBoundingBox(FrameworkElement child, FrameworkElement parent)
        {
            GeneralTransform transform = child.TransformToAncestor(parent);
            Point topLeft = transform.Transform(new Point(0, 0));
            Point bottomRight = transform.Transform(new Point(child.ActualWidth, child.ActualHeight));
            return new Rect(topLeft, bottomRight);
        }
    }
}
