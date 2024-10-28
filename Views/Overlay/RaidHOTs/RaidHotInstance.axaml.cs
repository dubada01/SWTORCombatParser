
using System;
using System.Diagnostics;
using Avalonia;
using System.Reactive.Linq;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using ReactiveUI;
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
            DataContextChanged += ConfigureSubscritoin;
        }

        private void ConfigureSubscritoin(object? sender, EventArgs e)
        {
            if(DataContext is TimerInstanceViewModel vm)
            {
                vm.TimerStarted += RestartAnimation;
                Debug.WriteLine("Subscription Made");
            }
        }

        private async void RestartAnimation()
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
                    Debug.WriteLine($"{DateTime.Now}: Starting HOT animation: "+vm.TimerName);
                    await animation.RunAsync(timerBar);
                }
            }
        }
    }
}
