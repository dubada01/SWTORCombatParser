using MoreLinq;
using SWTORCombatParser.DataStructures.AbilityInfo;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Model.LogParsing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace SWTORCombatParser.Model.CombatParsing
{
    public class ShieldingEvent
    {
        public Entity Source;
        public Entity Target;
        public string SheildName;
        public double ShieldValue;
        public DateTime ShieldingTime;
    }
    public static class AddSheildingToLogs
    {
        //public static void AddSheildLogs(Dictionary<Entity,List<ParsedLogEntry>> allPriticipantSheildingLogs, Combat combat)
        //{
        //    var currentAbsorbAbilities = AbilityLoader.GetAbosrbAbilities().Values.Select(v=>v.name).ToList();
        //    var state = CombatLogStateBuilder.CurrentState;
        //    var modifiers = state.Modifiers;

        //    foreach (var source in combat.CharacterParticipants)
        //    {
        //        var logs = combat.GetLogsInvolvingEntity(source);
        //        logs.RemoveAll(l => l.Ability == "Healer Bubble");
        //        combat.ShieldingProvidedLogs[source] = new List<ParsedLogEntry>();
        //        combat.TotalProvidedSheilding[source] = 0;

        //        var healingShieldModifiers = modifiers.Where(m => currentAbsorbAbilities.Contains(m.Key)).SelectMany(kvp=>kvp.Value).Where(mod=>mod.Source == source).ToList();
        //        if (!healingShieldModifiers.Any())
        //            continue;
        //        foreach (var recipient in combat.CharacterParticipants)
        //        {
        //            if (!allPriticipantSheildingLogs.ContainsKey(recipient))
        //                continue;
        //            var sheildingLogs = allPriticipantSheildingLogs[recipient].OrderBy(l=>l.TimeStamp).ToList();
        //            if (sheildingLogs.Count == 0)
        //                continue;
        //            List<ShieldingEvent> _totalSheildingProvided = new List<ShieldingEvent>();
        //            foreach (var sheildEffect in healingShieldModifiers.Where(m=>m.Target == recipient))
        //            {
        //                var absorbsDuringShield = sheildingLogs.Where(l => l.TimeStamp > sheildEffect.StartTime && l.TimeStamp <= sheildEffect.StopTime).ToList();
        //                if (absorbsDuringShield.Any())
        //                {
        //                    var indexOfLastShield = sheildingLogs.IndexOf(absorbsDuringShield.Last());
        //                    if(indexOfLastShield < sheildingLogs.Count-1)
        //                    {
        //                        var nextSheildLog = sheildingLogs[indexOfLastShield +1];
        //                        if(Math.Abs((nextSheildLog.TimeStamp - sheildEffect.StopTime).TotalSeconds)<2)
        //                        {
        //                            absorbsDuringShield.Add(nextSheildLog);
        //                        }
        //                    }
        //                }
        //                else
        //                {
        //                    var nextShield = sheildingLogs.FirstOrDefault(l => l.TimeStamp > sheildEffect.StopTime);
        //                    if (nextShield!=null && Math.Abs((nextShield.TimeStamp - sheildEffect.StopTime).TotalSeconds)<2)
        //                    {
        //                        absorbsDuringShield.Add(nextShield);
        //                    }
        //                }
        //                if (absorbsDuringShield.Count == 0)
        //                    continue;
        //                var shieldAdded = new ShieldingEvent { ShieldValue = absorbsDuringShield.Sum(l => l.Value.Modifier.DblValue), ShieldingTime = sheildEffect.StopTime, SheildName = sheildEffect.Name };

        //                _totalSheildingProvided.Add(shieldAdded);
        //            }


        //            foreach (var sheild in _totalSheildingProvided)
        //            {
        //                var logToInsertAfter = logs.FirstOrDefault(l => l.TimeStamp > sheild.ShieldingTime);
        //                if (logToInsertAfter == null)
        //                    continue;
        //                var indexToInsert = logs.IndexOf(logToInsertAfter);
        //                var sheildLog = new ParsedLogEntry
        //                {
        //                    TimeStamp = sheild.ShieldingTime,
        //                    Ability = sheild.SheildName+" on "+recipient.Name,
        //                    Effect = new Effect()
        //                    {
        //                        EffectType = EffectType.AbsorbShield,
        //                        EffectName = "Healer Shield"
        //                    },
        //                    SourceInfo = new EntityInfo { Entity = source },
        //                    TargetInfo = new EntityInfo() { Entity = recipient},
        //                    Value = new Value
        //                    {
        //                        EffectiveDblValue = sheild.ShieldValue,
        //                        ValueType = DamageType.heal
        //                    }
        //                };
        //                combat.AllLogs.Insert(
        //                    indexToInsert, sheildLog
        //                    );
        //                combat.ShieldingProvidedLogs[source].Add(sheildLog);
        //                combat.TotalProvidedSheilding[source] += sheild.ShieldValue;
        //            }
        //        }

        //    }

        //}
        public static void AddShieldLogsByTarget(Dictionary<Entity, List<ParsedLogEntry>> allPriticipantSheildingLogs, Combat combat)
        {
            var start = DateTime.Now;
            var currentAbsorbAbilities = AbilityLoader.GetAbosrbAbilities().Values.Select(v => v.name).ToList();
            var state = CombatLogStateBuilder.CurrentState;
            var modifiers = state.Modifiers;
            var allShieldLogs = allPriticipantSheildingLogs.Values.SelectMany(l => l).ToList();

            Dictionary<Entity, List<ShieldingEvent>> _totalSheildingProvided = allPriticipantSheildingLogs.ToDictionary(kvp=>kvp.Key,kvp=>new List<ShieldingEvent>());
            var absorbTargets = allShieldLogs.Select(l => l.Target).Distinct();
            
            foreach(var target in absorbTargets)
            {
                var logsForTarget = allShieldLogs.Where(l=>l.Target == target).ToList();
                var absorbsOnTarget = modifiers.Where(m => currentAbsorbAbilities.Contains(m.Value.First().Value.EffectName)).SelectMany(kvp => kvp.Value).Where(mod => mod.Value.Target == target).Select(kvp=>kvp.Value).ToList();
                foreach (var log in logsForTarget)
                {
                    var activeAbsorbs = absorbsOnTarget.Where((m, i) => IsModifierActive(m, i, absorbsOnTarget, log)).OrderBy(a=>a.StartTime).ToList();

                    for (var i = 0; i<activeAbsorbs.Count; i++)
                    {
                        var absorb = activeAbsorbs[i];
                        if (absorb.HasAbsorbBeenCounted)
                            continue;
                        var ammount = GetAbsorbAmmount(log, activeAbsorbs, i);
                        if (ammount == 0)
                            continue;
                        var source = absorb.Source;
                        if (!_totalSheildingProvided.ContainsKey(source))
                        {
                            _totalSheildingProvided[source] = new List<ShieldingEvent>();
                        }
                        var activeAbsorb = _totalSheildingProvided[source].FirstOrDefault(shield => shield.ShieldingTime == absorb.StopTime && shield.SheildName == absorb.Name && shield.Target == target);
                        if (activeAbsorb == null)
                        {
                            _totalSheildingProvided[source].Add(new ShieldingEvent
                            {
                                SheildName = absorb.Name,
                                ShieldingTime = absorb.StopTime,
                                ShieldValue = ammount,
                                Source = source,
                                Target = target
                            });
                        }
                        else
                        {
                            activeAbsorb.ShieldValue += ammount;
                        }
                    }
                }
            }
            
            foreach (var source in _totalSheildingProvided.Keys)
            {
                var shieldEvents = _totalSheildingProvided[source];
                var logs = combat.GetLogsInvolvingEntity(source);
                logs.RemoveAll(l => l.Effect.EffectType == EffectType.AbsorbShield);
                combat.ShieldingProvidedLogs[source] = new List<ParsedLogEntry>();
                combat.TotalProvidedSheilding[source] = 0;
                foreach (var sheild in shieldEvents)
                {
                    var logToInsertAfter = logs.FirstOrDefault(l => l.TimeStamp > sheild.ShieldingTime);
                    if (logToInsertAfter == null)
                        continue;
                    var indexToInsert = logs.IndexOf(logToInsertAfter);
                    var sheildLog = new ParsedLogEntry
                    {
                        TimeStamp = sheild.ShieldingTime,
                        Ability = sheild.SheildName+" on "+sheild.Target.Name,
                        Effect = new Effect()
                        {
                            EffectType = EffectType.AbsorbShield,
                            EffectName = "Processed Absorb"
                        },
                        SourceInfo = new EntityInfo { Entity = source },
                        TargetInfo = new EntityInfo() { Entity = sheild.Target },
                        Value = new Value
                        {
                            EffectiveDblValue = sheild.ShieldValue,
                            DisplayValue = sheild.ShieldValue.ToString("N2"),
                            ValueType = DamageType.heal
                        }
                    };
                    combat.AllLogs.Insert(
                        indexToInsert, sheildLog
                        );
                    combat.ShieldingProvidedLogs[source].Add(sheildLog);
                    combat.TotalProvidedSheilding[source] += sheild.ShieldValue;
                }
            }
            modifiers.ForEach(m => m.Value.ForEach(mv => mv.Value.HasAbsorbBeenCounted=false));
        }

        private static bool IsModifierActive(CombatModifier modifier, int i, List<CombatModifier> absorbsOnTarget, ParsedLogEntry log)
        {
            //todo mark modifiers are accounted for once they've been fully used 
            if (modifier.HasAbsorbBeenCounted)
                return false;
            if(modifier.StartTime < log.TimeStamp && (modifier.StopTime.AddSeconds(4.25) >= log.TimeStamp))
            {
                return true;
                if (i + 1 == absorbsOnTarget.Count)
                    return true;
                return modifier.StopTime.AddSeconds(4.25) < absorbsOnTarget[i+1].StartTime;
            }
            return false;
        }

        private static double GetAbsorbAmmount(ParsedLogEntry log, List<CombatModifier> absorbs, int index)
        {
            if (absorbs.Count == 1)
            {
                if (log.Value.EffectiveDblValue > 0)
                    absorbs[index].HasAbsorbBeenCounted = true;
                return log.Value.Modifier.DblValue;
            }
            if (absorbs.Count >1)
            {
                if (index > 1 && log.Value.EffectiveDblValue == 0)
                    return 0;
                var absorbedByFirst = 0d;
                var absorbedByRemainder = 0d;
                if(log.Value.DblValue == log.Value.Modifier.DblValue)
                {
                    absorbedByFirst = log.Value.Modifier.DblValue;
                }
                else
                {
                    if(index == 0)
                        absorbs[index].HasAbsorbBeenCounted = true;
                    absorbedByFirst = (log.Value.DblValue - log.Value.Modifier.DblValue)-log.Value.EffectiveDblValue;
                    absorbedByRemainder = log.Value.Modifier.DblValue;
                }
                
                if (index == 0)
                {
                    return absorbedByFirst;
                }
                if (index == 1)
                {
                    return absorbedByRemainder;
                }
            }
            return 0;
        }
    }
}
