using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser.DataStructures.AbilityInfo
{
    public class AbilityInfo
    {
        public string name { get; set; }
    }
    public static class AbilityLoader
    {
        public static Dictionary<double,AbilityInfo> GetAbosrbAbilities()
        {
            return JsonConvert.DeserializeObject<Dictionary<double, AbilityInfo>>(File.ReadAllText(@"DataStructures/AbilityInfo/absorbs.json"));
        }
    }
}
