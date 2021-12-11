using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SWTORCombatParser
{
    public class PlotMaker {

        public static double[] GetHPPercentages(List<ParsedLogEntry> logs,Entity sourcePlayer)
        {
            return logs.Where(l=>l.Target == sourcePlayer).Select(l => l.TargetInfo.CurrentHP ).ToArray();
        }
        internal static double[] GetPlotHPXVals(List<ParsedLogEntry> totalLogsDuringCombat, DateTime startPoint, Entity sourcePlayer)
        {
            var startTime = startPoint;
            return totalLogsDuringCombat.Where(l => l.Target == sourcePlayer).Select(l => (l.TimeStamp - startTime).TotalSeconds).ToArray();
        }
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
        internal static double[] GetPlotYValRates(double[] yValues ,double[] timeStamps,double averageWindowDuration = 10)
        {
            var movingAverageCalc = new MovingAverage(TimeSpan.FromSeconds(averageWindowDuration));
            
            var timeStampsSpread = Enumerable.Range((int)timeStamps.First(), (int)(timeStamps.Last()- timeStamps.First())).ToList();

            var movingaverage = new double[timeStampsSpread.Count];

            for(var i=0; i< timeStampsSpread.Count;i++)
            {
                var denseTime = timeStampsSpread[i];
                var timesAndIndex = timeStamps.Select((v, i) => new { v, i });
                var timeAndIndiciesInScope = timesAndIndex.Where(x => x.v > denseTime && x.v <= denseTime + 1);
                var indexes = timeAndIndiciesInScope.Select(x => x.i);


                if (indexes.Any())
                {
                    var valSum = 0d;
                    foreach(var ind in indexes)
                    {
                        valSum += yValues[ind];
                    }
                    movingaverage[i]=(movingAverageCalc.ComputeAverage(valSum, denseTime));
                }
                else
                {
                    movingaverage[i]=(movingAverageCalc.ComputeAverage(0, denseTime));
                }

            }
            
            return movingaverage;
        }
        public static List<(int,double)> GetPeaksOfMean(double[] data, double windowSize)
        {
            var zcores = ZScore.FindPeaks(data, 20).ToList();

            return zcores;
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
