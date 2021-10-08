using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.ClassInfos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SWTORCombatParser.Model.CombatParsing
{

    public static class ClassIdentifier
    {
        private static List<SWTORClass> _availableClasses = new List<SWTORClass>();
        public static void InitializeAvailableClasses()
        {
            _availableClasses = ClassLoader.LoadAllClasses();
        }
        public static SWTORClass IdentifyClass(ParsedLogEntry combatLog)
        {
            foreach(var swtorClass in _availableClasses)
            {
                if (swtorClass.UniqueAbilities.Any(a => combatLog.Ability == a && combatLog.Effect.EffectType == EffectType.Apply))
                    return swtorClass;
            }
            return null;
        }
    }
}
