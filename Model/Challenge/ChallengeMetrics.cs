using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Model.Phases;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Documents;

namespace SWTORCombatParser.Model.Challenge
{
    public static class ChallengeMetrics
    {
        public static double GetValueForChallenege(ChallengeType type, Combat combat, Entity participant, DataStructures.Challenge activeChallenge, PhaseInstance phaseOfInterest)
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
                    value = combat.GetLogsInvolvingEntity(participant).Where(l => l.Effect.EffectId == _7_0LogParsing.AbilityActivateId && (l.Ability == activeChallenge.Value || l.AbilityId == activeChallenge.Value)).Count();
                    break;
                case ChallengeType.EffectStacks:
                    value = activeChallenge.UseMaxValue ? combat.GetMaxEffectStacks(activeChallenge.Value, participant) : combat.GetCurrentEffectStacks(activeChallenge.Value, participant);
                    break;
                case ChallengeType.MetricDuringPhase:
                    if(phaseOfInterest == null)
                    {
                        value = 0;
                        break;
                    }
                    var phaseCombat = combat.GetPhaseCopy(phaseOfInterest);
                    if(phaseCombat.AllEntities.Contains(participant))
                        value = MetricGetter.GetValueForMetric(activeChallenge.PhaseMetric, new List<Combat> { phaseCombat } , participant);
                    break;
            }
            return value;
        }

    }
}
