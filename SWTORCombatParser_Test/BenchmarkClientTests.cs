using System;
using System.Linq;
using NUnit.Framework;
using SWTORCombatParser.Model.CloudRaiding;

namespace SWTORCombatParser_Test;

public class BenchmarkClientTests
{
    [Test]
    public void TiedScoresKeepTheirFrequencyAndMissingDataHasNoRank()
    {
        var metric=new BenchmarkMetric {AlgorithmVersion=2,Available=true,SampleCount=100,
            Thresholds=Enumerable.Repeat(10d,90).Concat(Enumerable.Repeat(100d,10)).ToArray()};
        Assert.That(metric.Rank(10),Is.EqualTo(90));
        Assert.That(metric.Rank(50),Is.EqualTo(90));
        Assert.That(metric.Rank(100),Is.EqualTo(100));
        Assert.That(metric.Rank(1),Is.Zero);
        metric.Available=false;
        Assert.That(metric.Rank(100),Is.Null);
        metric.Available=true; metric.SampleCount=0;
        Assert.That(metric.Rank(100),Is.Null);
    }

    [Test]
    public void InvalidOrUnknownBenchmarksNeverProduceAFalsePerfectRank()
    {
        var metric=new BenchmarkMetric {AlgorithmVersion=2,Available=true,SampleCount=10,Thresholds=new double[99]};
        Assert.That(metric.Rank(100),Is.Null);
        metric.Thresholds=Enumerable.Repeat(10d,100).ToArray(); metric.Thresholds[5]=double.NaN;
        Assert.That(metric.Rank(100),Is.Null);
        metric.Thresholds=Enumerable.Range(1,100).Reverse().Select(x=>(double)x).ToArray();
        Assert.That(metric.Rank(100),Is.Null);
        metric.Thresholds=Enumerable.Repeat(10d,100).ToArray(); metric.AlgorithmVersion=1;
        Assert.That(metric.Rank(100),Is.Null);
    }
}
