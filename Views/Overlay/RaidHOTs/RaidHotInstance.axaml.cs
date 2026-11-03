
using System;
using System.Diagnostics;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using SWTORCombatParser.ViewModels.Timers;

namespace SWTORCombatParser.Views.Overlay.RaidHOTs
{
    /// <summary>
    /// Interaction logic for RaidHotInstance.xaml
    /// </summary>
    public partial class RaidHotInstance : UserControl
    {
        public RaidHotInstance()
        {
            InitializeComponent();
            Loaded += RestartAnimation;
        }

        private async void RestartAnimation(object? sender, RoutedEventArgs routedEventArgs)
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
                    await animation.RunAsync(timerBar);
                }
            }
        }
    }
}
