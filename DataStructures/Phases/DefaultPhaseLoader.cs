using Newtonsoft.Json;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.Phases;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser.DataStructures.Phases
{
    public static class DefaultPhaseLoader
    {
        public static void LoadBuiltinPhases()
        {
            try
            {
                var phases = JsonConvert.DeserializeObject<IEnumerable<Phase>>(File.ReadAllText(@"DataStructures/Phases/phase_info.json"));
                foreach(var phase in phases)
                {
                    DefaultPhaseManager.AddOrUpdatePhase(phase);
                }
            }
            catch (Exception e)
            {
                Logging.LogError("Failed to load phase infos:" + e.Message);
            }
        }
    }
}
