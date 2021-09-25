using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SWTORCombatParser
{
    public class PlotMaker
    {

        internal static double[] GetPlotXVals(List<ParsedLogEntry> totalLogsDuringCombat,DateTime startPoint)
        {
            var startTime = startPoint;
            return totalLogsDuringCombat.Select(l => (l.TimeStamp - startTime).TotalSeconds).ToArray();
        }

        internal static double[] GetPlotYVals(List<ParsedLogEntry> totalLogsDuringCombat,bool checkEffective)
        {
            if(!checkEffective)
                return totalLogsDuringCombat.Select(l => l.Value.DblValue).ToArray();
            else
                return totalLogsDuringCombat.Select(l => l.Value.EffectiveDblValue).ToArray();
        }
        internal static double[] GetPlotXValsRates(double[] timeStamps)
        {
            var timeStampsSpread = Enumerable.Range((int)timeStamps.First(), (int)(timeStamps.Last() - timeStamps.First())).ToList();
            return timeStampsSpread.Select(d=>(double)d).ToArray();
        }
        internal static double[] GetPlotYValRates(List<ParsedLogEntry> totalLogsDuringCombat,double[] timeStamps, bool checkEffective)
        {
            //double sum = 0;
            //var sums = new List<double>();
            //if(!checkEffective)
            //    sums = totalLogsDuringCombat.Skip(1).Select((l) => sum += l.Value.DblValue).ToList();
            //else
            //    sums = totalLogsDuringCombat.Skip(1).Select((l) => sum += l.Value.EffectiveDblValue).ToList();
            //return sums.Select((s,i) => s / ((timeStamps.Skip(1).ToArray()[i]) == 0 ? 0.1d : (timeStamps.Skip(1).ToArray()[i]))).ToArray();
            var movingAverageCalc = new MovingAverage(TimeSpan.FromSeconds(10));

            var values = new List<double>();
            if (!checkEffective)
                values = totalLogsDuringCombat.Select((l) => l.Value.DblValue).ToList();
            else
                values = totalLogsDuringCombat.Select((l) => l.Value.EffectiveDblValue).ToList();
            var movingaverage = new List<double>();
            var timeStampsSpread = Enumerable.Range((int)timeStamps.First(), (int)(timeStamps.Last()- timeStamps.First())).ToList();

            foreach(var denseTime in timeStampsSpread)
            {
                var indexes = timeStamps.Select((v, i) => new { v, i })
                    .Where(x => x.v > denseTime && x.v <= denseTime+1)
                    .Select(x => x.i);
                if (indexes.Any())
                {
                    var valSum = 0d;
                    foreach(var ind in indexes)
                    {
                        valSum += values[ind];
                    }
                    movingaverage.Add(movingAverageCalc.ComputeAverage(valSum, denseTime));
                }
                else
                {
                    movingaverage.Add(movingAverageCalc.ComputeAverage(0, denseTime));
                }
                
            }
            return movingaverage.ToArray();
        }

        internal static List<(string,string)> GetAnnotationString(List<ParsedLogEntry> data)
        {
            return data.Select(d => GetAnnotationForAbilitiy(d)).ToList();
        }
        private static (string,string) GetAnnotationForAbilitiy(ParsedLogEntry log)
        {
            var critMark = log.Value.WasCrit ? "*" : "";
            var stringToShow = log.Ability + ": "+ log.Value.DblValue + critMark;
            if(log.Target.IsLocalPlayer && log.Effect.EffectName == "Damage" && log.Value.Modifier != null)
            {
                stringToShow += "\nMitigation: " + log.Value.Modifier.ValueType.ToString() + "-" + log.Value.Modifier.EffectiveDblValue;
            }
            var EffectivestringToShow = log.Ability + ": " + log.Value.EffectiveDblValue + critMark;
            if (log.Target.IsLocalPlayer && log.Effect.EffectName == "Damage" && log.Value.Modifier != null)
            {
                EffectivestringToShow += "\nMitigation: " + log.Value.Modifier.ValueType.ToString() + "-" + log.Value.Modifier.EffectiveDblValue;
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
        private Queue<(double, double)> samples = new Queue<(double,double)>();
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
            samples.Enqueue((newSample,timeStamp));

            while (TimeSpan.FromSeconds(samples.Last().Item2 - samples.First().Item2) > windowDuration)
            {
                sampleAccumulator -= samples.Dequeue().Item1;
            }

            Average = sampleAccumulator / (samples.Count == 1?1:(samples.Last().Item2 - samples.First().Item2));

            return Average;
        }
    }
}
