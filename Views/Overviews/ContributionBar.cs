using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace SWTORCombatParser.Views.Overviews;

// Size from the arranged cell so the fill follows column resizing.
public sealed class ContributionBar : Control
{
    public static readonly StyledProperty<double> PercentageProperty = AvaloniaProperty.Register<ContributionBar, double>(nameof(Percentage));
    private static readonly IBrush Fill = new SolidColorBrush(Color.Parse("#285C43"));
    static ContributionBar() => AffectsRender<ContributionBar>(PercentageProperty);
    public double Percentage { get => GetValue(PercentageProperty); set => SetValue(PercentageProperty, value); }
    public override void Render(DrawingContext context)
    {
        var fraction = double.IsFinite(Percentage) ? Math.Clamp(Percentage / 100, 0, 1) : 0;
        context.FillRectangle(Fill, new Rect(0, 0, Bounds.Width * fraction, Bounds.Height), 2);
    }
}
