using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using System.Collections.Generic;
using System.Linq;

namespace SWTORCombatParser.Model.Challenge
{
    public static class ChallengeMetrics
    {
        public static double GetValueForChallenege(ChallengeType type, Combat combat, Entity participant, DataStructures.Challenge activeChallenge, Combat phaseCombat)
        {

            double value = 0;
            switch (type)
            {
                case ChallengeType.DamageIn:
                    value = string.IsNullOrEmpty(activeChallenge.Value) || activeChallenge.Value.ToLower() == "any" ?

                         combat.GetDamageFromEntityByPlayer(activeChallenge.ChallengeSource, participant) / (activeChallenge.UseRawValues ? 1 : combat.DurationSeconds)
                         :
                        string.IsNullOrEmpty(activeChallenge.ChallengeSource) || activeChallenge.ChallengeSource.ToLower() == "any" ? combat.GetDamageIncomingByAbilityForPlayer(activeChallenge.Value, participant) / (activeChallenge.UseRawValues ? 1 : combat.DurationSeconds) :
                        combat.GetDamageFromEntityByAbilityForPlayer(activeChallenge.Value, activeChallenge.ChallengeSource, participant) / (activeChallenge.UseRawValues ? 1 : combat.DurationSeconds);
                    break;
                case ChallengeType.DamageOut:
                    value = string.IsNullOrEmpty(activeChallenge.Value) || activeChallenge.Value.ToLower() == "any" ?
                        combat.GetDamageToEntityByPlayer(activeChallenge.ChallengeTarget, participant) / (activeChallenge.UseRawValues ? 1 : combat.DurationSeconds) :
                        string.IsNullOrEmpty(activeChallenge.ChallengeTarget) || activeChallenge.ChallengeTarget.ToLower() == "any" ?
                        combat.GetDamageOutgoingByAbilityForPlayer(activeChallenge.Value, participant) / (activeChallenge.UseRawValues ? 1 : combat.DurationSeconds) :
                        combat.GetDamageToEntityByAbilityForPlayer(activeChallenge.Value, activeChallenge.ChallengeTarget, participant) / (activeChallenge.UseRawValues ? 1 : combat.DurationSeconds);
                    break;
                case ChallengeType.InterruptCount:
                    value = combat.TotalInterrupts[participant];
                    break;
                case ChallengeType.AbilityCount:
                    value = combat.GetLogsInvolvingEntity(participant).Count(l => l.Effect.EffectId == _7_0LogParsing.AbilityActivateId && (l.Ability == activeChallenge.Value || (long.TryParse(activeChallenge.Value, out var _) && l.AbilityId == ulong.Parse(activeChallenge.Value))));
                    break;
                case ChallengeType.EffectStacks:
                    value = activeChallenge.UseMaxValue ? combat.GetMaxEffectStacks(ulong.TryParse(activeChallenge.Value, out var challenge) ? challenge : 0, participant) : combat.GetCurrentEffectStacks(ulong.TryParse(activeChallenge.Value, out var _challenge) ? _challenge : 0, participant);
                    break;
                case ChallengeType.MetricDuringPhase:
                    if (combat.DurationMS > 0 && phaseCombat.AllEntities.Contains(participant))
                        value = MetricGetter.GetValueForMetric(activeChallenge.PhaseMetric,  phaseCombat, participant);
                    break;
            }
            return value;
        }

    }
}
