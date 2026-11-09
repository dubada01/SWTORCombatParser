using Newtonsoft.Json;
using SWTORCombatParser.Model.Phases;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
namespace SWTORCombatParser.DataStructures.Phases
{
    public static class DefaultPhaseLoader
    {
        public static void LoadBuiltinPhases()
        {
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, "DataStructures", "Phases", "phase_info.json");
                var phases = JsonConvert.DeserializeObject<Phase[]>(File.ReadAllText(path))
                    ?? throw new JsonSerializationException("The built-in phase list is null.");
                // Merge in one transaction, preserving custom definitions and
                // avoiding a partially rewritten file for every built-in phase.
                DefaultPhaseManager.AddOrUpdatePhases(phases);
            }
            catch (Exception e)
            {
                Logging.LogError("Failed to load phase infos:" + e.Message, tryCloudLogs: false);
            }
        }
    }
}
