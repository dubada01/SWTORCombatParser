using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.ClassInfos;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        public static SWTORClass IdentifyClass(string dicipline)
        {
            return _availableClasses.FirstOrDefault(c => c.Discipline == dicipline);
        }
    }
}