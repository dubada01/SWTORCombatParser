using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.ViewModels.Overlays.PvP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DateTime = System.DateTime;

namespace SWTORCombatParser.Views.Overlay.PvP
{
    /// <summary>
    /// Interaction logic for MiniMapView.xaml
    /// </summary>
    public partial class MiniMapView : Window
    {
        private MapInfo _currentMapInfo;
        private List<OpponentMapIcon> opponentImages => new List<OpponentMapIcon> { Op1, Op2, Op3, Op4, Op5, Op6, Op7, Op8, Op9, Op10, Op11, Op12, Op13, Op14, Op15, Op16 };
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
            HideAllOpponents();
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
            Dispatcher.Invoke(() =>
            {
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                if (shouldLock)
                {
                    BackgroundArea.Opacity = 0.05f;
                    int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                    SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT);
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
            viewModel.OverlayEnabled = false;
        }

        private void UpdateIconPosition(double xFraction, double yFraction, double facing, OpponentMapInfo opponent, int opponentIndex)
        {
            Dispatcher.Invoke(() =>
            {
                var icon = opponent.IsLocalPlayer ? CharImage : opponentImages[opponentIndex];
                icon.Icon.Source = new BitmapImage(GetUriFromMenaceType(opponent.IsEnemy, opponent.IsTarget, opponent.IsLocalPlayer));
                icon.SelectionAdornment.Visibility = opponent.IsTarget ? Visibility.Visible : Visibility.Hidden;

                //icon.PlayerName.Text = opponent.Name;
                icon.Visibility = Visibility.Visible;

                var imageLocation = GetBoundingBox(Arena, ImageCanvas);

                var characterXposOverlay = imageLocation.Width * xFraction;
                var characterYposOverlay = imageLocation.Height * yFraction;

                icon.Opacity = opponent.IsCurrentInfo ? 1 : 0.25;
                Canvas.SetLeft(icon, characterXposOverlay - (icon.Width / 2));
                Canvas.SetTop(icon, characterYposOverlay - (icon.Height / 2));

                var rotationTransform = new RotateTransform(facing * -1, 0, 0);
                icon.Icon.RenderTransform = rotationTransform;
            });
        }
        internal void AddOpponents(List<OpponentMapInfo> opponentInfos, DateTime startTime)
        {
            if (CombatIdentifier.CurrentCombat == null)
                return;
            var currentMap = CombatLogStateBuilder.CurrentState.GetEncounterActiveAtTime(startTime);
            _currentMapInfo = currentMap.MapInfo;
            var roomTop = _currentMapInfo.MinY;
            var roomLeft = _currentMapInfo.MinX;
            var roomWidth = _currentMapInfo.MaxX - _currentMapInfo.MinX;
            var roomHeight = _currentMapInfo.MaxY - _currentMapInfo.MinY;

            var opponentIndex = 0;
            HideAllOpponents();
            foreach (var opponent in opponentInfos.Where(o => o.IsEnemy != EnemyState.Friend && !o.IsLocalPlayer))
            {
                var xFraction = (opponent.Position.X - roomLeft) / roomWidth;
                var yFraction = (opponent.Position.Y - roomTop) / roomHeight;
                UpdateIconPosition(xFraction, yFraction, opponent.Position.Facing, opponent, opponentIndex);
                opponentIndex++;
            }
        }

        private Uri GetUriFromMenaceType(EnemyState isEnemy, bool isTaget, bool isLocalPlayer)
        {
            if (isLocalPlayer)
                return new Uri(System.IO.Path.Combine(Environment.CurrentDirectory, "resources/RoomOverlays/PlayerLocation.png"));
            if (isEnemy == EnemyState.Enemy)
            {
                return isTaget ?
                    new Uri(System.IO.Path.Combine(Environment.CurrentDirectory, "resources/RoomOverlays/TargetedEnemyLocation.png")) :
                    new Uri(System.IO.Path.Combine(Environment.CurrentDirectory, "resources/RoomOverlays/EnemyLocation.png"));
            }

            return new Uri(System.IO.Path.Combine(Environment.CurrentDirectory, "resources/RoomOverlays/UnknownPlayerLocation.png"));
        }

        private void HideAllOpponents()
        {
            CharImage.Visibility = Visibility.Hidden;
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
