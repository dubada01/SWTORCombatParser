using SWTORCombatParser.DataStructures.RaidInfos;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;

namespace SWTORCombatParser.ViewModels
{
    public class EncounterCombat
    {
        private List<Combat> combats;
        private object combatAddLock = new object();
        public EncounterInfo Info { get; set; }
        public Combat OverallCombat => GetOverallCombat();
        public List<Combat> Combats { get => combats; set
            { 
                    combats = value; 
            }
        }
        public void AddCombat(Combat combat)
        {
            lock (combatAddLock)
            {
                Combats.Add(combat);
            }
        }
        private Combat GetOverallCombat()
        {
            lock (combatAddLock)
            {
                Trace.WriteLine("Creating Encounter Combat for: "+ Combats.Count + " combats with " + Combats.SelectMany(c => c.AllLogs).Count() + " logs");
                var overallCombat = CombatIdentifier.GenerateNewCombatFromLogs(Combats.SelectMany(c => c.AllLogs).ToList());
                overallCombat.StartTime = overallCombat.StartTime.AddSeconds(-1);
                return overallCombat;
            }

        }
    }
}
