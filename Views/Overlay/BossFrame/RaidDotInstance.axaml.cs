﻿
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using SWTORCombatParser.ViewModels.Timers;

namespace SWTORCombatParser.Views.Overlay.BossFrame
{
    /// <summary>
    /// Interaction logic for RaidHotInstance.xaml
    /// </summary>
    public partial class RaidDotInstance : UserControl
    {
        public RaidDotInstance()
        {
            InitializeComponent();
            AttachedToVisualTree += TimerBarControl_AttachedToVisualTree;
        }
        private async void TimerBarControl_AttachedToVisualTree(object sender, VisualTreeAttachmentEventArgs e)
        {
            var timerBar = this.FindControl<Border>("TimerBar");
            if (timerBar?.RenderTransform is ScaleTransform barScale)
            {
                // Assuming your DataContext is set and has the properties TimerDuration and CurrentRatio
                if (this.DataContext is TimerInstanceViewModel vm)
                {
                    var duration = vm.TimerDuration; // TimeSpan property
                    var fromValue = vm.CurrentRatio; // double property

                    var animation = new Animation
                    {
                        Duration = duration,
                        Easing = new LinearEasing(), // Use linear easing for smooth animation
                        Children =
                        {
                            new KeyFrame
                            {
                                Cue = new Cue(0d),
                                Setters =
                                {
                                    new Setter(ScaleTransform.ScaleXProperty, fromValue)
                                }
                            },
                            new KeyFrame
                            {
                                Cue = new Cue(1d),
                                Setters =
                                {
                                    new Setter(ScaleTransform.ScaleXProperty, 0d)
                                }
                            }
                        }
                    };

                    await animation.RunAsync(barScale);
                }
            }
        }
    }
}
