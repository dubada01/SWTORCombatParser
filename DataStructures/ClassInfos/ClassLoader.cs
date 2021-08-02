using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SWTORCombatParser.DataStructures.ClassInfos
{
    public static class ClassLoader
    {
        public static List<SWTORClass> LoadAllClasses()
        {
            List<SWTORClass> loadedClasses = new List<SWTORClass>();
            var classString = File.ReadAllLines("DataStructures/ClassInfos/Classes.json");
            foreach (var row in classString)
            {
                //todo parse this from the string using JSON
                var swtorClass = new SWTORClass() {UniqueAbilities = new List<string>()};
                loadedClasses.Add(swtorClass);
            }
            return loadedClasses;
        }
    }
}
