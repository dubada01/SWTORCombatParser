using System;
using SWTORCombatParser.DataStructures;
using System.Collections.Generic;
using System.Linq;

namespace SWTORCombatParser.Model.Overlays;

public static class MetricGetter
{
    public static double GetValueForMetric(OverlayType type, List<Combat> combats, Entity participant)
    {
        double value = 0;
        if (!combats.Any(c => c.CharacterParticipants.ContainsKey(participant.LogId)))
            return value;
        switch (type)
        {
            case OverlayType.APM:
                value = combats.SelectMany(c => c.APM).Where(v => v.Key == participant).Select(v => v.Value).Average();
                break;
            case OverlayType.DPS:
                value = combats.SelectMany(c => c.EDPS).Where(v => v.Key == participant).Select(v => v.Value).Average();
                break;
            case OverlayType.InstantaneousDPS:
                value = combats.SelectMany(c => c.InstantaneousEffectiveDPS).Where(v => v.Key == participant).Select(v => v.Value).Average();
                break;
            case OverlayType.Damage:
                value = combats.SelectMany(c => c.TotalEffectiveDamage).Where(v => v.Key == participant)
                    .Select(v => v.Value).Average();
                break;
            case OverlayType.RawDamage:
                value = combats.SelectMany(c => c.TotalDamage).Where(v => v.Key == participant).Select(v => v.Value)
                    .Average();
                break;
            case OverlayType.NonEDPS:
                value = combats.SelectMany(c => c.DPS).Where(v => v.Key == participant).Select(v => v.Value).Average();
                break;
            case OverlayType.EHPS:
                value = combats.SelectMany(c => c.EHPS).Where(v => v.Key == participant).Select(v => v.Value).Average();
                value += combats.SelectMany(c => c.PSPS).Where(v => v.Key == participant).Select(v => v.Value)
                    .Average();
                break;
            case OverlayType.InstantaneousEHPS:
                value = combats.SelectMany(c => c.InstantaneousEffectiveHPS).Where(v => v.Key == participant).Select(v => v.Value).Average();
                break;
            case OverlayType.EffectiveHealing:
                value = combats.SelectMany(c => c.TotalEffectiveHealing).Where(v => v.Key == participant)
                    .Select(v => v.Value).Average();
                value += combats.SelectMany(c => c.TotalProvidedSheilding).Where(v => v.Key == participant).Select(v => v.Value)
    .Average();
                break;
            case OverlayType.RawHealing:
                value = combats.SelectMany(c => c.TotalHealing).Where(v => v.Key == participant).Select(v => v.Value)
                    .Average();
                break;
            case OverlayType.HPS:
                value = combats.SelectMany(c => c.HPS).Where(v => v.Key == participant).Select(v => v.Value)
                    .Average();
                break;
            case OverlayType.ProvidedAbsorb:
                value = combats.SelectMany(c => c.PSPS).Where(v => v.Key == participant).Select(v => v.Value).Average();
                break;
            case OverlayType.FocusDPS:
                value = combats.SelectMany(c => c.EFocusDPS).Where(v => v.Key == participant).Select(v => v.Value)
                    .Average();
                break;
            case OverlayType.ThreatPerSecond:
                value = combats.SelectMany(c => c.TPS).Where(v => v.Key == participant).Select(v => v.Value).Average();
                break;
            case OverlayType.Threat:
                value = combats.SelectMany(c => c.TotalThreat).Where(v => v.Key == participant).Select(v => v.Value)
                    .Average();
                break;
            case OverlayType.DamageTaken:
                value = combats.SelectMany(c => c.EDTPS).Where(v => v.Key == participant).Select(v => v.Value)
                    .Average();
                break;
            case OverlayType.Mitigation:
                value = combats.SelectMany(c => c.MPS).Where(v => v.Key == participant).Select(v => v.Value).Average();
                break;
            case OverlayType.DamageSavedDuringCD:
                value = combats.SelectMany(c => c.DamageSavedFromCDPerSecond).Where(v => v.Key == participant)
                    .Select(v => v.Value).Average();
                break;
            case OverlayType.DamageAvoided:
                value = combats.SelectMany(c => c.DAPS).Where(v => v.Key == participant).Select(v => v.Value).Average();
                break;
            case OverlayType.ShieldAbsorb:
                value = combats.SelectMany(c => c.SAPS).Where(v => v.Key == participant).Select(v => v.Value).Average();
                break;
            case OverlayType.BurstDPS:
                value = combats.SelectMany(c => c.MaxBurstDamage).Where(v => v.Key == participant).Select(v => v.Value)
                    .Average();
                break;
            case OverlayType.BurstEHPS:
                value = combats.SelectMany(c => c.MaxBurstHeal).Where(v => v.Key == participant).Select(v => v.Value)
                    .Average();
                break;
            case OverlayType.BurstDamageTaken:
                value = combats.SelectMany(c => c.MaxBurstDamageTaken).Where(v => v.Key == participant)
                    .Select(v => v.Value).Average();
                break;
            case OverlayType.HealReactionTime:
                value = combats.SelectMany(c => c.NumberOfHighSpeedReactions).Where(v => v.Key == participant)
                    .Select(v => v.Value).Average();
                break;
            case OverlayType.TankHealReactionTime:
                value = combats.SelectMany(c => c.AverageTankDamageRecoveryTimeTotal).Where(v => v.Key == participant)
                    .Select(v => v.Value).Average();
                break;
            case OverlayType.InterruptCount:
                value = combats.SelectMany(c => c.TotalInterrupts).Where(v => v.Key == participant).Select(v => v.Value)
                    .Average();
                break;
            case OverlayType.CritPercent:
                value = combats.SelectMany(c => c.CritPercent).Where(c => c.Key == participant).Select(v => v.Value).Average() * 100;
                break;
            case OverlayType.SingleTargetDPS:
                value = combats.SelectMany(c => c.STDPS).Where(v => v.Key == participant).Select(v => v.Value).Average();
                break;
            case OverlayType.SingleTargetEHPS:
                value = combats.SelectMany(c => c.STEHPS).Where(v => v.Key == participant).Select(v => v.Value).Average();
                break;
            case OverlayType.CleanseCount:
                value = combats.SelectMany(c => c.TotalCleanses).Where(v => v.Key == participant).Select(v => v.Value)
                    .Average();
                break;
            case OverlayType.CleanseSpeed:
                value = combats.SelectMany(c => c.AverageCleanseSpeed).Where(v => v.Key == participant).Select(v => v.Value)
                    .Average();
                break;
        }

        return value;
    }
    public static double GetTotalforMetric(OverlayType type, Combat? combat)
    {
        try
        {
            double value = 0;
            if (combat == null)
                return value;
            switch (type)
            {
                case OverlayType.APM:
                    value = combat.APM.Where(kvp => kvp.Key.IsCharacter).Select(v => v.Value)
                        .Average();
                    break;
                case OverlayType.DPS:
                    value = combat.EDPS.Where(kvp => kvp.Key.IsCharacter).Select(v => v.Value)
                        .Sum();
                    break;
                case OverlayType.InstantaneousDPS:
                    value = combat.InstantaneousEffectiveDPS.Where(kvp => kvp.Key.IsCharacter)
                        .Select(v => v.Value).Sum();
                    break;
                case OverlayType.Damage:
                    value = combat.TotalEffectiveDamage.Where(kvp => kvp.Key.IsCharacter)
                        .Select(v => v.Value).Sum();
                    break;
                case OverlayType.RawDamage:
                    value = combat.TotalDamage.Where(kvp => kvp.Key.IsCharacter)
                        .Select(v => v.Value)
                        .Sum();
                    break;
                case OverlayType.NonEDPS:
                    value = combat.DPS.Where(kvp => kvp.Key.IsCharacter).Select(v => v.Value).Sum();
                    break;
                case OverlayType.EHPS:
                    value = combat.EHPS.Where(kvp => kvp.Key.IsCharacter).Select(v => v.Value)
                        .Sum();
                    value += combat.PSPS.Where(kvp => kvp.Key.IsCharacter).Select(v => v.Value)
                        .Sum();
                    break;
                case OverlayType.InstantaneousEHPS:
                    value = combat.InstantaneousEffectiveHPS.Where(kvp => kvp.Key.IsCharacter)
                        .Select(v => v.Value).Sum();
                    break;
                case OverlayType.EffectiveHealing:
                    value = combat.TotalEffectiveHealing.Where(kvp => kvp.Key.IsCharacter)
                        .Select(v => v.Value).Sum();
                    break;
                case OverlayType.RawHealing:
                    value = combat.TotalHealing.Where(kvp => kvp.Key.IsCharacter)
                        .Select(v => v.Value)
                        .Sum();
                    break;
                case OverlayType.HPS:
                    value = combat.HPS.Where(kvp => kvp.Key.IsCharacter).Select(v => v.Value)
                        .Sum();
                    break;
                case OverlayType.ProvidedAbsorb:
                    value = combat.PSPS.Where(kvp => kvp.Key.IsCharacter).Select(v => v.Value)
                        .Sum();
                    break;
                case OverlayType.FocusDPS:
                    value = combat.EFocusDPS.Where(kvp => kvp.Key.IsCharacter).Select(v => v.Value)
                        .Sum();
                    break;
                case OverlayType.ThreatPerSecond:
                    value = combat.TPS.Where(kvp => kvp.Key.IsCharacter).Select(v => v.Value).Sum();
                    break;
                case OverlayType.Threat:
                    value = combat.TotalThreat.Where(kvp => kvp.Key.IsCharacter)
                        .Select(v => v.Value)
                        .Sum();
                    break;
                case OverlayType.DamageTaken:
                    value = combat.EDTPS.Where(kvp => kvp.Key.IsCharacter).Select(v => v.Value)
                        .Sum();
                    break;
                case OverlayType.Mitigation:
                    value = combat.MPS.Where(kvp => kvp.Key.IsCharacter).Select(v => v.Value).Sum();
                    break;
                case OverlayType.DamageSavedDuringCD:
                    value = combat.DamageSavedFromCDPerSecond.Where(kvp => kvp.Key.IsCharacter)
                        .Select(v => v.Value).Sum();
                    break;
                case OverlayType.DamageAvoided:
                    value = combat.DAPS.Where(kvp => kvp.Key.IsCharacter).Select(v => v.Value)
                        .Sum();
                    break;
                case OverlayType.ShieldAbsorb:
                    value = combat.SAPS.Where(kvp => kvp.Key.IsCharacter).Select(v => v.Value)
                        .Sum();
                    break;
                case OverlayType.BurstDPS:
                    value = combat.MaxBurstDamage.Where(kvp => kvp.Key.IsCharacter)
                        .Select(v => v.Value)
                        .Average();
                    break;
                case OverlayType.BurstEHPS:
                    value = combat.MaxBurstHeal.Where(kvp => kvp.Key.IsCharacter)
                        .Select(v => v.Value)
                        .Average();
                    break;
                case OverlayType.BurstDamageTaken:
                    value = combat.MaxBurstDamageTaken.Where(kvp => kvp.Key.IsCharacter)
                        .Select(v => v.Value).Average();
                    break;
                case OverlayType.HealReactionTime:
                    value = combat.NumberOfHighSpeedReactions.Where(kvp => kvp.Key.IsCharacter)
                        .Select(v => v.Value).Average();
                    break;
                case OverlayType.TankHealReactionTime:
                    value = combat.AverageTankDamageRecoveryTimeTotal
                        .Where(kvp => kvp.Key.IsCharacter)
                        .Select(v => v.Value).Average();
                    break;
                case OverlayType.InterruptCount:
                    value = combat.TotalInterrupts.Where(kvp => kvp.Key.IsCharacter)
                        .Select(v => v.Value)
                        .Sum();
                    break;
                case OverlayType.CritPercent:
                    value = combat.CritPercent.Where(kvp => kvp.Key.IsCharacter)
                        .Select(v => v.Value).Average() * 100;
                    break;
                case OverlayType.SingleTargetDPS:
                    value = combat.STDPS.Where(kvp => kvp.Key.IsCharacter).Select(v => v.Value)
                        .Average();
                    break;
                case OverlayType.SingleTargetEHPS:
                    value = combat.STEHPS.Where(kvp => kvp.Key.IsCharacter).Select(v => v.Value)
                        .Average();
                    break;
                case OverlayType.CleanseCount:
                    value = combat.TotalCleanses.Where(kvp => kvp.Key.IsCharacter)
                        .Select(v => v.Value)
                        .Sum();
                    break;
                case OverlayType.CleanseSpeed:
                    var averageCleanseSpeed = combat
                        .AverageCleanseSpeed.Where(kvp => kvp.Key.IsCharacter)
                        .Select(v => v.Value)
                        .Where(v => v != 0);

                    value = averageCleanseSpeed.Any()
                        ? averageCleanseSpeed.Average()
                        : 0.0;
                    break;
            }

            return value;
        }
        catch (Exception e)
        {
            return 0;
        }
    }
    public static double GetValueForMetric(OverlayType type, Combat combat, Entity participant)
    {
        double value = 0;
        participant = ResolveCombatParticipant(combat, participant);
        if (participant == null)
            return value;
        switch (type)
        {
            case OverlayType.APM:
                value = GetMetricValue(combat.APM, participant);
                break;
            case OverlayType.DPS:
                value = GetMetricValue(combat.EDPS, participant);
                break;
            case OverlayType.InstantaneousDPS:
                value = GetMetricValue(combat.InstantaneousEffectiveDPS, participant);
                break;
            case OverlayType.Damage:
                value = GetMetricValue(combat.TotalEffectiveDamage, participant);
                break;
            case OverlayType.NonEDPS:
                value = GetMetricValue(combat.DPS, participant);
                break;
            case OverlayType.RawDamage:
                value = GetMetricValue(combat.TotalDamage, participant);
                break;
            case OverlayType.EHPS:
                value = GetMetricValue(combat.EHPS, participant) + GetMetricValue(combat.PSPS, participant);
                break;
            case OverlayType.InstantaneousEHPS:
                value = GetMetricValue(combat.InstantaneousEffectiveHPS, participant);
                break;
            case OverlayType.EffectiveHealing:
                value = GetMetricValue(combat.TotalEffectiveHealing, participant);
                break;
            case OverlayType.HPS:
                value = GetMetricValue(combat.HPS, participant);
                break;
            case OverlayType.RawHealing:
                value = GetMetricValue(combat.TotalHealing, participant);
                break;
            case OverlayType.ProvidedAbsorb:
                value = GetMetricValue(combat.PSPS, participant);
                break;
            case OverlayType.FocusDPS:
                value = GetMetricValue(combat.EFocusDPS, participant);
                break;
            case OverlayType.ThreatPerSecond:
                value = GetMetricValue(combat.TPS, participant);
                break;
            case OverlayType.Threat:
                value = GetMetricValue(combat.TotalThreat, participant);
                break;
            case OverlayType.DamageTaken:
                value = GetMetricValue(combat.EDTPS, participant);
                break;
            case OverlayType.Mitigation:
                value = GetMetricValue(combat.MPS, participant);
                break;
            case OverlayType.DamageSavedDuringCD:
                value = GetMetricValue(combat.DamageSavedFromCDPerSecond, participant);
                break;
            case OverlayType.DamageAvoided:
                value = GetMetricValue(combat.DAPS, participant);
                break;
            case OverlayType.ShieldAbsorb:
                value = GetMetricValue(combat.SAPS, participant);
                break;
            case OverlayType.BurstDPS:
                value = GetMetricValue(combat.MaxBurstDamage, participant);
                break;
            case OverlayType.BurstEHPS:
                value = GetMetricValue(combat.MaxBurstHeal, participant);
                break;
            case OverlayType.BurstDamageTaken:
                value = GetMetricValue(combat.MaxBurstDamageTaken, participant);
                break;
            case OverlayType.HealReactionTime:
                value = GetMetricValue(combat.NumberOfHighSpeedReactions, participant);
                break;
            case OverlayType.HealReactionTimeRatio:
                var abilitiesActivated = combat.AbilitiesActivated.TryGetValue(participant, out var activatedAbilities)
                    ? activatedAbilities.Count
                    : 0;
                value = abilitiesActivated == 0
                    ? 0
                    : GetMetricValue(combat.NumberOfHighSpeedReactions, participant) / abilitiesActivated;
                break;
            case OverlayType.TankHealReactionTime:
                value = GetMetricValue(combat.AverageTankDamageRecoveryTimeTotal, participant);
                break;
            case OverlayType.InterruptCount:
                value = GetMetricValue(combat.TotalInterrupts, participant);
                break;
            case OverlayType.SingleTargetDPS:
                value = GetMetricValue(combat.STDPS, participant);
                break;
            case OverlayType.SingleTargetEHPS:
                value = GetMetricValue(combat.STEHPS, participant);
                break;
            case OverlayType.CleanseCount:
                value = GetMetricValue(combat.TotalCleanses, participant);
                break;
            case OverlayType.CleanseSpeed:
                value = GetMetricValue(combat.AverageCleanseSpeed, participant);
                break;
            case OverlayType.FluffDPS:
                value = GetMetricValue(combat.ERegDPS, participant);
                break;
            case OverlayType.EHPSNoShielding:
                value = GetMetricValue(combat.EHPS, participant);
                break;
            
        }

        return value;
    }

    private static Entity ResolveCombatParticipant(Combat combat, Entity participant)
    {
        if (combat == null || participant == null)
            return null;

        if (participant.LogId != 0 && combat.CharacterParticipants.TryGetValue(participant.LogId, out var matchingParticipant))
            return matchingParticipant;

        if (participant.Id != 0)
        {
            matchingParticipant = combat.CharacterParticipants.Values.FirstOrDefault(p => p.Id == participant.Id);
            if (matchingParticipant != null)
                return matchingParticipant;
        }

        if (participant.IsLocalPlayer && combat.LocalPlayer != null)
            return combat.LocalPlayer;

        if (!string.IsNullOrEmpty(participant.Name))
        {
            matchingParticipant = combat.CharacterParticipants.Values.FirstOrDefault(p =>
                p.Name == participant.Name && (participant.IsLocalPlayer == p.IsLocalPlayer || participant.IsLocalPlayer));
            if (matchingParticipant != null)
                return matchingParticipant;
        }

        return combat.CharacterParticipants.Values.FirstOrDefault(p => ReferenceEquals(p, participant));
    }

    private static double GetMetricValue(IReadOnlyDictionary<Entity, double> metrics, Entity participant)
    {
        return metrics != null && participant != null && metrics.TryGetValue(participant, out var value) ? value : 0;
    }
}
