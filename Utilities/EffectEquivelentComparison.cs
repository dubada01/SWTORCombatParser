using SWTORCombatParser.Model.LogParsing;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace SWTORCombatParser.Utilities
{
    public class EffectEquivelentComparison : IEqualityComparer<CombatModifier>
    {
        public bool Equals([AllowNull] CombatModifier x, [AllowNull] CombatModifier y)
        {
            return x.Target == y.Target && x.Source == y.Source && x.StartTime == y.StartTime && x.Name == y.Name;
        }

        public int GetHashCode([DisallowNull] CombatModifier obj)
        {
            return obj.ToString().ToLower().GetHashCode();
        }
    }
}
