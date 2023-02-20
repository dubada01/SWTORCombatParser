using SWTORCombatParser.DataStructures.ClassInfos;
using System.Collections.Generic;
using System.Linq;

namespace SWTORCombatParser.Model.CombatParsing
{

    public static class ClassIdentifier
    {
        private static List<SWTORClass> _availableClasses = new List<SWTORClass>();
        public static void InitializeAvailableClasses()
        {
            _availableClasses = ClassLoader.LoadAllClasses();
        }
        public static SWTORClass IdentifyClassById(string diciplineId)
        {
            return _availableClasses.FirstOrDefault(c => c.DisciplineId == diciplineId);
        }
    }
}