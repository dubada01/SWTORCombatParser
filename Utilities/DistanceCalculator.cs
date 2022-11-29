using System;
using SWTORCombatParser.DataStructures;

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
