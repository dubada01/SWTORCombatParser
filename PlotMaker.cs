using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SWTORCombatParser
{
    public class PlotMaker
    {
        internal static List<double> GetPlotXVals(List<ParsedLogEntry> totalLogsDuringCombat,DateTime startPoint)
        {
            var startTime = startPoint;
            return totalLogsDuringCombat.Select(l => (l.TimeStamp - startTime).TotalMilliseconds).ToList();
        }

        internal static List<double> GetPlotYVals(List<ParsedLogEntry> totalLogsDuringCombat)
        {
            return totalLogsDuringCombat.Select(l => l.Value.DblValue).ToList();
        }
        internal static List<double> GetPlotYValRates(List<ParsedLogEntry> totalLogsDuringCombat,List<double> timeStamps)
        {
            double sum = 0;
            
                var sums = totalLogsDuringCombat.Select((l) => sum += l.Value.DblValue);
            return sums.Select((s,i)=>s/ (timeStamps[i] / 1000)).ToList();
        }
    }
}
