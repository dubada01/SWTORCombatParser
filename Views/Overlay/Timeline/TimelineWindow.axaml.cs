using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using SWTORCombatParser.ViewModels.Avalonia_TEMP;
using VerticalAlignment = Avalonia.Layout.VerticalAlignment;

namespace SWTORCombatParser.Views.Overlay.Timeline
{
    public partial class TimelineWindow : Window
    {
      
        private bool _isDragging = false;
        private Point _startPoint;
        // Windows-specific constants for P/Invoke
        const int GWL_EXSTYLE = -20;
        const int WS_EX_LAYERED = 0x00080000;
        const int WS_EX_TRANSPARENT = 0x00000020;

        [DllImport("user32.dll", SetLastError = true)]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        // P/Invoke to interact with Objective-C runtime and Cocoa APIs
        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "sel_registerName")]
        public static extern IntPtr sel_registerName(string name);

        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_getClass")]
        public static extern IntPtr objc_getClass(string name);

        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        public static extern void objc_msgSend(IntPtr receiver, IntPtr selector, bool arg1);

        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        public static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        public static extern void objc_msgSend_float(IntPtr receiver, IntPtr selector, float value);

        private TimelineWindowViewModel viewModel;
        private Canvas timelineCanvas;
        private bool initialized = false;
        public TimeSpan MaxDuration { get; set; }
        public TimeSpan CurrentTime { get; set; }
        public event Action<Point,Point> OnStateChanged = delegate { };
        public event Action OnHideWindow = delegate { };
        private Point _position;
        private Point _size;
        public TimelineWindow()
        {
            InitializeComponent();
            viewModel = DataContext as TimelineWindowViewModel;
            timelineCanvas = this.FindControl<Canvas>("TimelineCanvas");
            timelineCanvas.LayoutUpdated += (a, b) => {
                if (initialized)
                    return;// Subscribe to property change events or set initial positions here
                SetCurrentTimeAndUpdate(viewModel.CurrentTime);
                initialized = true;
            };
            MaxDuration = viewModel.MaxDuration;
        }

        public int GetBossCombatCounts()
        {
            return timelineCanvas.Children.Count;
        }

        public void SetSize(double x, double y)
        {
            ClientSize = new Size(x, y);
        }
        public TimelineWindow(TimelineWindowViewModel vm)
        {
            viewModel = vm;
            DataContext = vm;
            InitializeComponent();

            // Set up references and initial setup
            timelineCanvas = this.FindControl<Canvas>("TimelineCanvas");
            // Subscribe to LayoutUpdated event to ensure the Canvas is fully rendered
            timelineCanvas.LayoutUpdated += (a, b) => {
                if (initialized)
                    return;// Subscribe to property change events or set initial positions here
                SetCurrentTimeAndUpdate(viewModel.CurrentTime);
                initialized = true;
                _size = new Point(ClientSize.Width, ClientSize.Height);
                _position = new Point(Position.X, Position.Y);
            };
            viewModel.OnInit += SetCurrentTimeAndUpdate;
            viewModel.OnUpdateTimeline += SetCurrentTimeAndUpdate;
            viewModel.UpdateClickThrough += ToggleClickThrough;
            viewModel.AreaEntered += SetAreaName;
            this.Opened += OnWindowOpened;
            this.Resized += OnWindowResized;
        }
        private void OnWindowResized(object? sender, WindowResizedEventArgs e)
        {
            OnUpdateTimelinePositions();
            _size = new Point(e.ClientSize.Width, e.ClientSize.Height);
            OnStateChanged(_size,_position);
        }

        private void ToggleClickThrough(bool canClickThrough)
        {
            Dispatcher.UIThread.Invoke(() => {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    MakeWindowClickThroughMac(canClickThrough);
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    MakeWindowClickThroughWindows(canClickThrough);
                WindowBackground.Opacity = canClickThrough ? 0.1 : 0.75;
                CloseButton.IsVisible = !canClickThrough;
            });

        }
        private void OnWindowOpened(object sender, EventArgs e)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                MakeWindowClickThroughMac(true);
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                MakeWindowClickThroughWindows(true);
            }

            CloseButton.IsVisible = false;
            WindowBackground.Opacity = 0.1;
        }
        // Platform-specific method for Windows
        private void MakeWindowClickThroughWindows(bool isClickThrough)
        {
            // Get the native window handle using Avalonia's GetPlatformHandle method
            var platformHandle = this.TryGetPlatformHandle();
            if (platformHandle == null)
            {
                Console.WriteLine("Unable to retrieve platform handle.");
                return;
            }
            var hWnd = platformHandle.Handle;

            // Get the current extended style
            int extendedStyle = GetWindowLong(hWnd, GWL_EXSTYLE);

            if (isClickThrough)
            {
                // Make the window click-through
                SetWindowLong(hWnd, GWL_EXSTYLE, extendedStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT);
            }
            else
            {
                // Make the window clickable again by removing the WS_EX_TRANSPARENT flag
                SetWindowLong(hWnd, GWL_EXSTYLE, extendedStyle & ~WS_EX_TRANSPARENT);
            }
        }
        private void MakeWindowClickThroughMac(bool isClickThrough)
        {
            // Get the native NSWindow handle using Avalonia's GetPlatformHandle method
            var platformHandle = this.TryGetPlatformHandle();
            if (platformHandle == null)
            {
                Console.WriteLine("Unable to retrieve platform handle.");
                return;
            }

            IntPtr nsWindowHandle = platformHandle.Handle;

            // Get the selector for 'setIgnoresMouseEvents:'
            var setIgnoresMouseEventsSelector = sel_registerName("setIgnoresMouseEvents:");

            // Call the 'setIgnoresMouseEvents' method with the boolean argument
            objc_msgSend(nsWindowHandle, setIgnoresMouseEventsSelector, isClickThrough);
        }

        private void SetAreaName(string name, string difficulty, string playerCount)
        {
            EncounterName.Text = name + $" {{{difficulty} {playerCount}}}";
        }
        
        // Example method to update the positions of timeline elements
        private object lockObj = new object();
        private void SetCurrentTimeAndUpdate(TimeSpan currentTime)
        {
            lock (lockObj)
            {
                CurrentTime = currentTime;
                OnUpdateTimelinePositions();
            }
        }
        private void OnUpdateTimelinePositions()
        {
            lock (lockObj)
            {


                if (viewModel == null || timelineCanvas == null)
                    return;
                timelineCanvas.Children.Clear();
                double maxDuration = viewModel.MaxDuration.TotalSeconds;
                double canvasWidth = timelineCanvas.Bounds.Width;
                if(maxDuration == 0)
                    return; 
                foreach (var element in viewModel.AllTimelineElements)
                {
                    // Calculate the position based on element.StartTime and maxDuration
                    double elementStartTime = element.StartTime.TotalSeconds;
                    double positionLeft = (elementStartTime / maxDuration) * canvasWidth;
                    // Create UI elements if needed or update existing ones
                    if (element.IsLeaderboard)
                    {
                        var border = new Border
                        {
                            Background = Brushes.OrangeRed,
                            CornerRadius = new CornerRadius(5),
                            Padding = new Thickness(5),
                            Width = (element.TTK.TotalSeconds / maxDuration) * canvasWidth,
                            Child = new TextBlock
                            {
                                Text = element.BossName,
                                VerticalAlignment = VerticalAlignment.Center,
                                HorizontalAlignment = HorizontalAlignment.Center,
                                TextTrimming = TextTrimming.CharacterEllipsis
                            }
                        };
                        // Set Canvas.Left and Canvas.Top
                        Canvas.SetLeft(border, positionLeft);
                        Canvas.SetTop(border, 53);
                        // Add to the timeline canvas
                        timelineCanvas.Children.Add(border);
                    }
                    if (element.IsFreshKill)
                    {
                        var border = new Border
                        {
                            Background = Brushes.LimeGreen,
                            CornerRadius = new CornerRadius(5),
                            Padding = new Thickness(5),
                            Width = (element.TTK.TotalSeconds / maxDuration) * canvasWidth,
                            Child = new TextBlock
                            {
                                Text = element.BossName,
                                VerticalAlignment = VerticalAlignment.Center,
                                HorizontalAlignment = HorizontalAlignment.Center,
                                TextTrimming = TextTrimming.CharacterEllipsis
                            }
                        };
                        // Set Canvas.Left and Canvas.Top
                        Canvas.SetLeft(border, positionLeft);
                        Canvas.SetTop(border, 20);
                        // Add to the timeline canvas
                        timelineCanvas.Children.Add(border);
                    }
                    if (!element.IsLeaderboard && !element.IsFreshKill)
                    {
                        var border = new Border
                        {
                            Background = Brushes.LightBlue,
                            CornerRadius = new CornerRadius(5),
                            Padding = new Thickness(5),
                            Width = (element.TTK.TotalSeconds / maxDuration) * canvasWidth,
                            Child = new TextBlock
                            {
                                FontSize = 10,
                                Text = element.BossName,
                                VerticalAlignment = VerticalAlignment.Center,
                                HorizontalAlignment = HorizontalAlignment.Center,
                                TextTrimming = TextTrimming.CharacterEllipsis
                            }
                        };
                        // Set Canvas.Left and Canvas.Top
                        Canvas.SetLeft(border, positionLeft);
                        Canvas.SetTop(border, 20);
                        // Add to the timeline canvas
                        timelineCanvas.Children.Add(border);
                    }




                }

                // Update current time indicator
                UpdateCurrentTimeIndicator(maxDuration, canvasWidth);
            }
        }

        private void UpdateCurrentTimeIndicator(double maxDuration, double canvasWidth)
        {
            // Calculate the position for the red current time indicator
            double positionLeft = (CurrentTime.TotalSeconds / maxDuration) * canvasWidth;

            var currentTimeIndicator = new Border()
            {
                CornerRadius = new CornerRadius(5),
                Width = 3,
                Height = 40,
                Background = Brushes.ForestGreen
            };

            Canvas.SetLeft(currentTimeIndicator, positionLeft);
            Canvas.SetTop(currentTimeIndicator, 30);
            DurationInfo.Text = $"{CurrentTime.Minutes}m {CurrentTime.Seconds}s";
            timelineCanvas.Children.Add(currentTimeIndicator);
        }

        private void DragArea_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            _isDragging = true;
            _startPoint = e.GetPosition(this);
        }

        private void DragArea_PointerMoved(object? sender, PointerEventArgs e)
        {
            if (_isDragging)
            {
                // Get the current scaling factor to adjust the movement correctly
                var scalingFactor = this.VisualRoot.RenderScaling;

                var currentPosition = e.GetPosition(this);
                var delta = (currentPosition - _startPoint) / scalingFactor;  // Adjust for DPI scaling

                // Move the window (or element) by the delta
                var currentPositionInScreen = this.Position;
                this.Position = new PixelPoint(
                    currentPositionInScreen.X + (int)delta.X,
                    currentPositionInScreen.Y + (int)delta.Y
                );
                _position = new Point(Position.X, Position.Y);
                OnStateChanged(_size,_position);
            }
        }

        private void DragArea_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            _isDragging = false;
        }

        private void Thumb_OnDragDelta(object? sender, VectorEventArgs e)
        {
            var yadjust = Height + e.Vector.Y;
            var xadjust = Width + e.Vector.X;
            if (xadjust > 0)
                SetValue(WidthProperty, xadjust);
            if (yadjust > 0)
                SetValue(HeightProperty, yadjust);
        }

        private void CloseButton_OnClick(object? sender, RoutedEventArgs e)
        {
            OnHideWindow();
        }
    }
}