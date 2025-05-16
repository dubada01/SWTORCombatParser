using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SWTORCombatParser.ViewModels.Avalonia_TEMP;
using VerticalAlignment = Avalonia.Layout.VerticalAlignment;

namespace SWTORCombatParser.Views.Overlay.Timeline
{
    public partial class TimelineWindow : UserControl
    {
        private TimelineWindowViewModel viewModel;
        private Canvas timelineCanvas;
        private bool initialized = false;
        public TimeSpan MaxDuration { get; set; }
        public TimeSpan CurrentTime { get; set; }
        private Point _position;
        private Point _size;
        private bool _isOpen = false;

        public int GetBossCombatCounts()
        {
            return timelineCanvas.Children.Count;
        }

        public TimelineWindow(TimelineWindowViewModel vm)
        {
            viewModel = vm;
            DataContext = vm;
            InitializeComponent();
            
            // Set up references and initial setup
            timelineCanvas = this.FindControl<Canvas>("TimelineCanvas");
            viewModel.OnInit += SetCurrentTimeAndUpdate;
            viewModel.OnUpdateTimeline += SetCurrentTimeAndUpdate;
            viewModel.AreaEntered += SetAreaName;
            
            timelineCanvas.GetObservable(BoundsProperty).Subscribe(_ =>
            {
                if (viewModel?.AllTimelineElements?.Count > 0)
                    OnUpdateTimelinePositions();
            });
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
    }
}