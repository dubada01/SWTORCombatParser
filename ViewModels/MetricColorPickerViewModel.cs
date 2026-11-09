using System;
using System.Reactive;
using Avalonia.Media;
using ReactiveUI;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Utilities;

namespace SWTORCombatParser.ViewModels;

public class MetricColorPickerViewModel : ReactiveObject
{
    private Color _metricColor;
    public OverlayType OverlayType { get; }
    public event Action CloseRequested = delegate { };
    public SolidColorBrush MetricBrush => new(_metricColor);
    public Color MetricColor
    {
        get => _metricColor;
        set
        {
            if (_metricColor == value) return;
            this.RaiseAndSetIfChanged(ref _metricColor, value);
            MetricColorLoader.SetColorForMetric(OverlayType, value.ToString());
            this.RaisePropertyChanged(nameof(MetricBrush));
        }
    }
    public MetricColorPickerViewModel(OverlayType type)
    {
        OverlayType = type;
        _metricColor = MetricColorLoader.GetMetricCurrentColor(type);
    }
    public ReactiveCommand<Unit, Unit> SetDefaultCommand => ReactiveCommand.Create(() => { MetricColor = MetricColorLoader.GetDefaultColorForMetric(OverlayType); });
    public ReactiveCommand<Unit, Unit> CloseCommand => ReactiveCommand.Create(() => CloseRequested());
}
