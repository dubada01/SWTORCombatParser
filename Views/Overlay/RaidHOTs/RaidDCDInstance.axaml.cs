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
    /// Interaction logic for RaidDCDInstance.xaml
    /// </summary>
    public partial class RaidDCDInstance : UserControl
    {
        public RaidDCDInstance()
        {
            InitializeComponent();
            Loaded += TimerBarControl_AttachedToVisualTree;
        }
        
        private async void TimerBarControl_AttachedToVisualTree(object? sender, RoutedEventArgs routedEventArgs)
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
                                    new Setter(ScaleTransform.ScaleYProperty, fromValue)
                                }
                            },
                            new KeyFrame
                            {
                                Cue = new Cue(1d),
                                Setters =
                                {
                                    new Setter(ScaleTransform.ScaleYProperty, 0d)
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
