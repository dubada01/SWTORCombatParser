using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.Model.LogParsing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Windows.Forms.AxHost;

namespace SWTORCombatParser.Model.CloudRaiding
{
    public static class Stats
    {
        public static async Task RecordCombatState(Combat combat)
        {
            if(!combat.WasBossKilled)
            { return; }
            await API_Connection.TryAddBossEncounter(new GameEncounter
            {
                BossName = combat.EncounterBossDifficultyParts.Item1,
                Difficulty = combat.EncounterBossDifficultyParts.Item3,
                NumberOfPlayers =  int.Parse(combat.EncounterBossDifficultyParts.Item2),
                TimeToKill = combat.DurationSeconds,
                PlayerClasses = combat.CharacterParticipants.Select(c=>GetClass(c,combat.StartTime)).ToList(),
                PlayerNames = combat.CharacterParticipants.Select(c=>c.Name).ToList(),
                EncounterName = combat.ParentEncounter.Name,
                EncounterTimestamp = combat.StartTime.ToUniversalTime()
            });
        }

        private static string GetClass(Entity c, DateTime startTime)
        {
            var state = CombatLogStateBuilder.CurrentState;
            SWTORClass playerClass;
            if (!CombatLogStateBuilder.CurrentState.PlayerClassChangeInfo.ContainsKey(c))
            {
                playerClass = null;
            }
            else
            {
                playerClass = state.GetCharacterClassAtTime(c, startTime);
            }
            return playerClass == null ? "Unknown" : playerClass.Name + "/" + playerClass.Discipline;
        }
    }
}
