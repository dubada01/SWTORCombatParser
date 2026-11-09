using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace SWTORCombatParser.Views.Progression;

public sealed class PullDurationBar : Control
{
    public static readonly StyledProperty<double> DurationFractionProperty = AvaloniaProperty.Register<PullDurationBar, double>(nameof(DurationFraction));
    public static readonly StyledProperty<double> FirstDeathFractionProperty = AvaloniaProperty.Register<PullDurationBar, double>(nameof(FirstDeathFraction), -1);
    private static readonly IBrush Track = new SolidColorBrush(Color.Parse("#393D3C"));
    private static readonly IBrush Fill = new SolidColorBrush(Color.Parse("#498B76"));
    private static readonly IBrush FirstDeath = new SolidColorBrush(Color.Parse("#FFBB75"));

    static PullDurationBar() => AffectsRender<PullDurationBar>(DurationFractionProperty, FirstDeathFractionProperty);
    public double DurationFraction { get => GetValue(DurationFractionProperty); set => SetValue(DurationFractionProperty, value); }
    public double FirstDeathFraction { get => GetValue(FirstDeathFractionProperty); set => SetValue(FirstDeathFractionProperty, value); }

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        if (width < 2 || Bounds.Height < 12) return;
        var y = (Bounds.Height - 8) / 2;
        context.FillRectangle(Track, new Rect(0, y, width, 8), 2);
        var durationWidth = Math.Clamp(DurationFraction, 0, 1) * width;
        if (durationWidth > 0) context.FillRectangle(Fill, new Rect(0, y, durationWidth, 8), 2);
        if (FirstDeathFraction >= 0)
        {
            var x = Math.Clamp(FirstDeathFraction * width - 1, 0, width - 2);
            context.FillRectangle(FirstDeath, new Rect(x, y - 2, 2, 12), 1);
        }
    }
}
