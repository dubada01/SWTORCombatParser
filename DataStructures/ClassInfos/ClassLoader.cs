using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SWTORCombatParser.DataStructures.ClassInfos
{
    public class AllSWTORClasses
    {
        public List<SWTORClass> AllClasses { get; set; }
    }
    public static class ClassLoader
    {
        public static List<SWTORClass> LoadAllClasses()
        {
            var allClasses = File.ReadAllText("DataStructures/ClassInfos/Classes.json");
            return JsonConvert.DeserializeObject<AllSWTORClasses>(allClasses).AllClasses;
        }
    }
}
