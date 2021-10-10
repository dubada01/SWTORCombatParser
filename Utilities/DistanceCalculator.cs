using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser.Utilities
{
    public static class DistanceCalculator
    {
        public static double CalculateDistanceBetweenEntities(PositionData source, PositionData target)
        {
            return Math.Abs(Math.Sqrt(Math.Pow((source.X - target.X), 2) + Math.Pow((source.Y - target.Y), 2)));
        }
    }
}
