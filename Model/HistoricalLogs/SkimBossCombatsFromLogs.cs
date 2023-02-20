using SWTORCombatParser.Utilities;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.CombatParsing;

namespace SWTORCombatParser.Model.HistoricalLogs
{
    public static class SkimBossCombatsFromLogs
    {
        public static List<Combat> GetBossCombats(List<ParsedLogEntry> allLogs)
        {
            List<List<ParsedLogEntry>> concurrentLogsForCombat = new List<List<ParsedLogEntry>>();
            List<ParsedLogEntry> currentCombatLogs = new List<ParsedLogEntry>();

            CombatDetector.Reset();

            foreach (var line in allLogs)
            {
                var combatState = CombatDetector.CheckForCombatState(line);

                if (combatState == CombatState.ExitedByEntering)
                {
                    if (currentCombatLogs.Count == 0)
                        continue;
                    currentCombatLogs.Add(line);
                    concurrentLogsForCombat.Add(currentCombatLogs.ToList());
                    currentCombatLogs.Clear();
                }
                if (combatState == CombatState.EnteredCombat)
                {
                    currentCombatLogs.Clear();
                }
                if (combatState == CombatState.ExitedCombat)
                {
                    if (currentCombatLogs.Count == 0)
                        continue;
                    currentCombatLogs.Add(line);
                    concurrentLogsForCombat.Add(currentCombatLogs.ToList());
                    currentCombatLogs.Clear();
                }
                if (combatState == CombatState.InCombat)
                    currentCombatLogs.Add(line);

            }
            return GetCombats(concurrentLogsForCombat);
        }
        private static List<Combat> GetCombats(List<List<ParsedLogEntry>> logsSplitIntoCombats)
        {
            ConcurrentBag<Combat> concurrentCombats = new ConcurrentBag<Combat>();
            Parallel.ForEach(logsSplitIntoCombats, logs => {
                var combatCreated = CombatIdentifier.GenerateNewCombatFromLogs(logs,false,true);
                if (!string.IsNullOrEmpty(combatCreated.EncounterBossInfo))
                    concurrentCombats.Add(combatCreated);
            });
            return concurrentCombats.ToList();
        }
    }
}
