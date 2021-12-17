using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Model.LogParsing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SWTORCombatParser.Model.CombatParsing
{
    public class SheildingEvent
    {
        public double SheildValue;
        public DateTime ShieldingTime;
    }
    public static class AddSheildingToLogs
    {
        public static void AddSheildLogs(Dictionary<Entity,List<ParsedLogEntry>> allPriticipantSheildingLogs, Combat combat)
        {
            var state = CombatLogStateBuilder.CurrentState;
            var modifiers = state.Modifiers;

            foreach (var source in combat.CharacterParticipants)
            {
                var logs = combat.GetLogsInvolvingEntity(source);
                logs.RemoveAll(l => l.Ability == "Healer Bubble");
                combat.SheildingProvidedLogs[source] = new List<ParsedLogEntry>();
                combat.TotalProvidedSheilding[source] = 0;

                var healingShieldModifiers = modifiers.Where(m => m.Key == "Static Barrier" || m.Key == "Force Armor").SelectMany(kvp=>kvp.Value).Where(mod=>mod.Source == source).ToList();
                if (!healingShieldModifiers.Any())
                    continue;
                foreach (var recipient in combat.CharacterParticipants)
                {
                    if (!allPriticipantSheildingLogs.ContainsKey(recipient))
                        continue;
                    var sheildingLogs = allPriticipantSheildingLogs[recipient];
                    if (sheildingLogs.Count == 0)
                        continue;
                    List<SheildingEvent> _totalSheildingProvided = new List<SheildingEvent>();
                    foreach (var sheildEffect in healingShieldModifiers.Where(m=>m.Target == recipient))
                    {
                        var absorbsDuringShield = sheildingLogs.Where(l => l.TimeStamp > sheildEffect.StartTime && l.TimeStamp <= sheildEffect.StopTime).ToList();
                        if (absorbsDuringShield.Count == 0)
                            continue;
                        var shieldAdded = new SheildingEvent { SheildValue = absorbsDuringShield.Sum(l => l.Value.Modifier.DblValue), ShieldingTime = sheildEffect.StopTime };

                        _totalSheildingProvided.Add(shieldAdded);
                    }


                    foreach (var sheild in _totalSheildingProvided)
                    {
                        var logToInsertAfter = logs.FirstOrDefault(l => l.TimeStamp > sheild.ShieldingTime);
                        if (logToInsertAfter == null)
                            continue;
                        var indexToInsert = logs.IndexOf(logToInsertAfter);
                        var sheildLog = new ParsedLogEntry
                        {
                            TimeStamp = sheild.ShieldingTime,
                            Ability = "Bubble on "+recipient.Name,
                            Effect = new Effect()
                            {
                                EffectType = EffectType.HealerShield,
                                EffectName = "Healer Shield"
                            },
                            SourceInfo = new EntityInfo { Entity = source },
                            TargetInfo = new EntityInfo() { Entity = recipient},
                            Value = new Value
                            {
                                EffectiveDblValue = sheild.SheildValue,
                                ValueType = DamageType.absorbed
                            }
                        };
                        combat.AllLogs.Insert(
                            indexToInsert, sheildLog
                            );
                        combat.SheildingProvidedLogs[source].Add(sheildLog);
                        combat.TotalProvidedSheilding[source] += sheild.SheildValue;
                    }
                }

            }

        }
    }
}
