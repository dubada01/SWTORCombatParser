using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SWTORCombatParser.Utilities;

public static class ObservableCollectionExtensions
{
    public static void Sort<T>(this ObservableCollection<T> collection, Comparison<T> comparison)
    {
        var sorted = collection.OrderBy(x => x, Comparer<T>.Create(comparison)).ToList();
        for (int i = 0; i < sorted.Count; i++)
            collection.Move(collection.IndexOf(sorted[i]), i);
    }
}