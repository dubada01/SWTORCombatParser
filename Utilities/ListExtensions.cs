using System;
using System.Collections.Generic;

namespace SWTORCombatParser.Utilities
{
    public static class ListExtensions
    {
        public static void MoveIndex<T>(this List<T> list, int srcIdx, int destIdx)
        {
            if (srcIdx != destIdx)
            {
                list.Insert(destIdx, list[srcIdx]);
                list.RemoveAt(destIdx < srcIdx ? srcIdx + 1 : srcIdx);
            }
        }
        public static void SwapItems<T>(this List<T> list, int idxX, int idxY)
        {
            if (idxX != idxY)
            {
                T tmp = list[idxX];
                list[idxX] = list[idxY];
                list[idxY] = tmp;
            }
        }
        public static int IndexOfMin(this IList<double> self)
        {
            if (self == null)
            {
                throw new ArgumentNullException("self");
            }

            if (self.Count == 0)
            {
                throw new ArgumentException("List is empty.", "self");
            }

            double min = self[0];
            int minIndex = 0;

            for (int i = 1; i < self.Count; ++i)
            {
                if (self[i] < min)
                {
                    min = self[i];
                    minIndex = i;
                }
            }

            return minIndex;
        }
    }
}
