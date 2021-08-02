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
                return totalLogsDuringCombat.Select(l => l.Value.DblValue - ((l.Value.Modifier?.DblValue) ?? 0)).ToArray();
            else
                return totalLogsDuringCombat.Select(l => l.Value.EffectiveDblValue).ToArray();
        }
        internal static double[] GetPlotYValRates(List<ParsedLogEntry> totalLogsDuringCombat,double[] timeStamps, bool checkEffective)
        {
            double sum = 0;
            var sums = new List<double>();
            if(checkEffective)
                sums = totalLogsDuringCombat.Select((l) => sum += (l.Value.DblValue-((l.Value.Modifier?.DblValue) ?? 0))).ToList();
            else
                sums = totalLogsDuringCombat.Select((l) => sum += l.Value.EffectiveDblValue).ToList();
            return sums.Select((s,i)=>s/ (timeStamps[i])).ToArray();
        }

        internal static List<string> GetAbilitityNames(List<ParsedLogEntry> data)
        {
            return data.Select(d => d.Ability).ToList();
        }
    }
}
