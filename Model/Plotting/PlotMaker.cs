using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SWTORCombatParser.Model.Plotting
{
    public class PlotMaker
    {

        public static double[] GetHPPercentages(List<ParsedLogEntry> logs, Entity sourcePlayer)
        {
            return logs.Where(l => l.Target == sourcePlayer && l.Effect.EffectType != EffectType.AbsorbShield).Select(l => ((double)l.TargetInfo.CurrentHP / (double)l.TargetInfo.MaxHP)).ToArray();
        }
        internal static double[] GetPlotHPXVals(List<ParsedLogEntry> totalLogsDuringCombat, DateTime startPoint, Entity sourcePlayer)
        {
            var startTime = startPoint;
            var logsToUse = totalLogsDuringCombat.Where(l => l.Target == sourcePlayer && l.Effect.EffectType != EffectType.AbsorbShield);
            return logsToUse.Select(l => (l.TimeStamp - startTime).TotalSeconds).ToArray();
        }
        internal static double[] GetPlotXVals(IEnumerable<ParsedLogEntry> totalLogsDuringCombat, DateTime startPoint)
        {
            var startTime = startPoint;
            return totalLogsDuringCombat.Select(l => (l.TimeStamp - startTime).TotalSeconds).ToArray();
        }

        internal static double[] GetPlotYVals(IEnumerable<ParsedLogEntry> totalLogsDuringCombat, bool checkEffective)
        {
            if (!checkEffective)
                return totalLogsDuringCombat.Select(l => l.Value.DblValue).ToArray();
            else
            {
                return totalLogsDuringCombat.Select(l => l.Value.EffectiveDblValue).ToArray();
            }

        }
        internal static double[] GetPlotXValsRates(double[] timeStamps)
        {
            var timeStampsSpread = Enumerable.Range((int)timeStamps.First(), (int)(timeStamps.Last() - timeStamps.First())).ToList();
            return timeStampsSpread.Select(d => (double)d).ToArray();
        }
        internal static double[] GetPlotYValRates(double[] yValues, double[] timeStamps, double averageWindowDuration = 10)
        {
            var movingAverageCalc = new MovingAverage(TimeSpan.FromSeconds(averageWindowDuration));
            var timeStampsSpread = Enumerable.Range((int)timeStamps.First(), (int)(timeStamps.Last() - timeStamps.First())).ToList();


            Dictionary<int, double> perSecondSums = new Dictionary<int, double>();
            for (var t = 0; t < timeStamps.Length; t++)
            {
                var second = (int)timeStamps[t];
                if (!perSecondSums.ContainsKey(second))
                {
                    perSecondSums[second] = 0;
                }
                perSecondSums[second] += yValues[t];
            }
            var movingaverage = new double[timeStampsSpread.Count];
            for (int i = 0; i < timeStampsSpread.Count; i++)
            {
                if (!perSecondSums.TryGetValue(timeStampsSpread[i], out var spread))
                {
                    movingaverage[i] = movingAverageCalc.ComputeAverage(0, timeStampsSpread[i]);
                }
                else
                {
                    movingaverage[i] = movingAverageCalc.ComputeAverage(spread, timeStampsSpread[i]);
                }
            }


            return movingaverage;
        }
        public static List<(int, double)> GetPeaksOfMean(double[] data, double windowSize)
        {
            var zcores = ZScore.FindPeaks(data, 20).ToList();

            return zcores;
        }

        internal static List<(string, string)> GetAnnotationString(List<ParsedLogEntry> data, bool isIncoming, bool isShield = false)
        {
            return data.Select(d => GetAnnotationForAbilitiy(d, isIncoming, isShield)).ToList();
        }
        private static (string, string) GetAnnotationForAbilitiy(ParsedLogEntry log, bool isIncoming, bool isShield)
        {
            var critMark = log.Value.WasCrit ? "*" : "";
            var stringToShow = log.Ability + ": " + log.Value.DblValue + critMark;
            if (log.Target.IsCharacter && log.Effect.EffectId == _7_0LogParsing._damageEffectId && log.Value.Modifier != null)
            {
                stringToShow += "\nMitigation: " + log.Value.Modifier.ValueType.ToString() + "-" + log.Value.Modifier.EffectiveDblValue;
            }
            var EffectivestringToShow = log.Ability + ": " + log.Value.EffectiveDblValue + critMark;
            if (log.Target.IsCharacter && log.Effect.EffectId == _7_0LogParsing._damageEffectId && log.Value.Modifier != null)
            {
                EffectivestringToShow += "\nMitigation: " + log.Value.Modifier.ValueType.ToString() + "-" + log.Value.Modifier.EffectiveDblValue;
            }
            if (isIncoming)
            {
                stringToShow += "\n(" + log.Source.Name + ")";
                EffectivestringToShow += "\n(" + log.Source.Name + ")";
            }
            if (isShield)
            {
                stringToShow += "\n(" + log.Target.Name + ")";
                EffectivestringToShow += "\n(" + log.Target.Name + ")";
            }
            return (stringToShow, EffectivestringToShow);
        }

    }
    public class MovingAverage
    {

        public MovingAverage(TimeSpan duration)
        {
            windowDuration = duration;
        }
        private Queue<(double, double)> samples = new Queue<(double, double)>();
        private TimeSpan windowDuration;
        private double sampleAccumulator;
        public double Average { get; private set; }
        /// <summary>
        /// Computes a new windowed average each time a new sample arrives
        /// </summary>
        /// <param name="newSample"></param>
        public double ComputeAverage(double newSample, double timeStamp)
        {
            sampleAccumulator += newSample;
            samples.Enqueue((newSample, timeStamp));

            while (TimeSpan.FromSeconds(samples.Last().Item2 - samples.First().Item2) > windowDuration)
            {
                sampleAccumulator -= samples.Dequeue().Item1;
            }

            Average = sampleAccumulator / (samples.Count == 1 ? 1 : (samples.Last().Item2 - samples.First().Item2));

            return Average;
        }
    }
}
