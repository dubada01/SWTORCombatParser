using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser.Model.Challenge
{
    public static class ChallengeMetrics
    {
        public static double GetValueForChallenege(ChallengeType type, Combat combat, Entity participant, DataStructures.Challenge activeChallenge)
        {

            double value = 0;
            switch (type)
            {
                case ChallengeType.DamageIn:
                    value = string.IsNullOrEmpty(activeChallenge.Value) || activeChallenge.Value == "Any" ? 
                        combat.GetDamageFromEntityByPlayer(activeChallenge.ChallengeSource,participant) / (activeChallenge.UseRawValues ? 1 : combat.DurationSeconds) :
                        combat.GetDamageFromEntityByAbilityForPlayer(activeChallenge.Value,activeChallenge.ChallengeSource,participant) / (activeChallenge.UseRawValues ? 1 : combat.DurationSeconds);
                    break;
                case ChallengeType.DamageOut:
                    value = string.IsNullOrEmpty(activeChallenge.Value) || activeChallenge.Value == "Any" ?
                        combat.GetDamageToEntityByPlayer(activeChallenge.ChallengeTarget, participant) / (activeChallenge.UseRawValues ? 1 : combat.DurationSeconds) :
                        combat.GetDamageToEntityByAbilityForPlayer(activeChallenge.Value, activeChallenge.ChallengeTarget, participant) / (activeChallenge.UseRawValues ? 1 : combat.DurationSeconds);
                    break;
                case ChallengeType.InterruptCount:
                    value = combat.TotalInterrupts[participant];
                    break;
                case ChallengeType.AbilityCount:
                    value = combat.GetLogsInvolvingEntity(participant).Where(l => l.Effect.EffectId == _7_0LogParsing.AbilityActivateId && l.Ability == activeChallenge.Value).Count();
                    break;
                case ChallengeType.EffectStacks:
                    value = activeChallenge.UseMaxValue ? combat.GetMaxEffectStacks(activeChallenge.Value,participant) : combat.GetCurrentEffectStacks(activeChallenge.Value,participant);
                    break;
                case ChallengeType.MetricDuringPhase:
                    value = 0; //TO BE IMPLEMENTED AFTER PHASES
                    break;
            }
            return value;
        }

    }
}
