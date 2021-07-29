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
            return totalLogsDuringCombat.Select(l => (l.TimeStamp - startTime).TotalSeconds).ToList();
        }

        internal static List<double> GetPlotYVals(List<ParsedLogEntry> totalLogsDuringCombat,bool checkEffective)
        {
            if(!checkEffective)
                return totalLogsDuringCombat.Select(l => l.Value.DblValue - ((l.Value.Modifier?.DblValue) ?? 0)).ToList();
            else
                return totalLogsDuringCombat.Select(l => l.Threat*2d).ToList();
        }
        internal static List<double> GetPlotYValRates(List<ParsedLogEntry> totalLogsDuringCombat,List<double> timeStamps, bool checkEffective)
        {
            double sum = 0;
            var sums = new List<double>();
            if(checkEffective)
                sums = totalLogsDuringCombat.Select((l) => sum += (l.Value.DblValue-((l.Value.Modifier?.DblValue) ?? 0))).ToList();
            else
                sums = totalLogsDuringCombat.Select((l) => sum += l.Threat*2d).ToList();
            return sums.Select((s,i)=>s/ (timeStamps[i])).ToList();
        }

        internal static List<string> GetAbilitityNames(List<ParsedLogEntry> data)
        {
            return data.Select(d => d.Ability).ToList();
        }
    }
}
