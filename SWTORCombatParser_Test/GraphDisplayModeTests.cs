using System;
using System.Linq;
using NUnit.Framework;
using ScottPlot;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.ViewModels.Home_View_Models;

namespace SWTORCombatParser_Test;

public class GraphDisplayModeTests
{
    [Test, Combinatorial]
    public void DisplayLayersComposeWithEffectiveAndRawMetricSelections(
        [Values] bool points, [Values] bool trends, [Values] bool effective, [Values] bool raw)
    {
        var plot = new Plot();
        var date = DateTime.Today;
        var series = new CombatMetaDataSeries { Legend = new LegendItemViewModel { HasEffective = true } };
        var xs = new[] { 0d, 1d }; var ys = new[] { 10d, 20d };
        series.Points[date] = plot.Add.ScatterPoints(xs, ys);
        series.Line[date] = plot.Add.ScatterLine(xs, ys);
        series.EffectivePoints[date] = plot.Add.ScatterPoints(xs, ys);
        series.EffectiveLine[date] = plot.Add.ScatterLine(xs, ys);
        series.Legend.LegenedToggled += (normal, unadjusted) => typeof(CombatMetaDataSeries)
            .GetMethod("LegenedToggled", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(series, new object[] { normal, unadjusted });
        series.SetDisplayMode(points, trends);
        series.Legend.Checked = effective; series.Legend.EffectiveChecked = raw;
        Assert.Multiple(() =>
        {
            Assert.That(series.Points[date].IsVisible, Is.EqualTo(points && effective));
            Assert.That(series.Line[date].IsVisible, Is.EqualTo(trends && effective));
            Assert.That(series.EffectivePoints[date].IsVisible, Is.EqualTo(points && raw));
            Assert.That(series.EffectiveLine[date].IsVisible, Is.EqualTo(trends && raw));
            Assert.That(series.Points[date].Data.GetScatterPoints().Count(), Is.EqualTo(2), "Toggling must preserve the underlying event data.");
        });
    }
}
