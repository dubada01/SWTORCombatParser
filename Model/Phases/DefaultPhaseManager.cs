using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SWTORCombatParser.Model.Phases
{
    public static class DefaultPhaseManager
    {
        private static string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DubaTech", "SWTORCombatParser");

        private static string _phaseFile = Path.Combine(appDataPath, "phase_info.json");

        public static IEnumerable<Phase> GetExisitingPhases()
        {
            TryInit();
            var phaseText = File.ReadAllText(_phaseFile);
            var phases = Newtonsoft.Json.JsonConvert.DeserializeObject<IEnumerable<Phase>>(phaseText);
            foreach (var phase in phases)
            {
                phase.SetSource();
            }
            return phases;
        }
        public static void RemovePhase(Phase phase)
        {
            var phases = GetExisitingPhases().ToList();
            phases.RemoveAll(p => p.Id == phase.Id);
            var phaseText = Newtonsoft.Json.JsonConvert.SerializeObject(phases);
            File.WriteAllText(_phaseFile, phaseText);
        }
        public static void AddOrUpdatePhase(Phase phase)
        {
            var phases = GetExisitingPhases().ToList();
            var existingPhase = phases.FirstOrDefault(x => x.Id == phase.Id);
            if (existingPhase != null)
            {
                phases.Remove(existingPhase);
            }
            phases.Add(phase);
            var phaseText = Newtonsoft.Json.JsonConvert.SerializeObject(phases);
            File.WriteAllText(_phaseFile, phaseText);
        }
        private static void TryInit()
        {
            if (!File.Exists(_phaseFile))
            {
                Directory.CreateDirectory(appDataPath);
                File.WriteAllText(_phaseFile, "[]");
            }
        }
    }
}
