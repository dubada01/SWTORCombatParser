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
        internal static double[] GetPlotYValRates(List<ParsedLogEntry> totalLogsDuringCombat,double[] timeStamps, bool checkEffective)
        {
            double sum = 0;
            var sums = new List<double>();
            if(!checkEffective)
                sums = totalLogsDuringCombat.Skip(1).Select((l) => sum += l.Value.DblValue).ToList();
            else
                sums = totalLogsDuringCombat.Skip(1).Select((l) => sum += l.Value.EffectiveDblValue).ToList();
            return sums.Select((s,i)=>s / (timeStamps.Skip(1).ToArray()[i])).ToArray();
        }

        internal static List<(string,string)> GetAnnotationString(List<ParsedLogEntry> data)
        {
            return data.Select(d => GetAnnotationForAbilitiy(d)).ToList();
        }
        private static (string,string) GetAnnotationForAbilitiy(ParsedLogEntry log)
        {
            var critMark = log.Value.WasCrit ? "*" : "";
            var stringToShow = log.Ability + ": "+ log.Value.DblValue + critMark;
            if(log.Target.IsPlayer && log.Effect.EffectName == "Damage" && log.Value.Modifier != null)
            {
                stringToShow += "\nMitigation: " + log.Value.Modifier.ValueType.ToString() + "-" + log.Value.Modifier.EffectiveDblValue;
            }
            var EffectivestringToShow = log.Ability + ": " + log.Value.EffectiveDblValue + critMark;
            if (log.Target.IsPlayer && log.Effect.EffectName == "Damage" && log.Value.Modifier != null)
            {
                EffectivestringToShow += "\nMitigation: " + log.Value.Modifier.ValueType.ToString() + "-" + log.Value.Modifier.EffectiveDblValue;
            }
            return (stringToShow, EffectivestringToShow);
        }
    }
}
