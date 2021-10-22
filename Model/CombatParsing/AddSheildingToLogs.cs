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
        public DateTime SheildingTime;
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

                var healingShieldModifiers = modifiers.Where(m => (m.Name == "Static Barrier" || m.Name == "Force Armor") && m.Source == source).ToList();
                foreach(var recipient in allPriticipantSheildingLogs.Keys)
                {
                    var sheildingLogs = allPriticipantSheildingLogs[recipient];
                    if (healingShieldModifiers.Count == 0)
                        continue;

                    List<SheildingEvent> _totalSheildingProvided = new List<SheildingEvent>();
                    foreach (var sheildEffect in healingShieldModifiers)
                    {
                        var absorbsDuringShield = sheildingLogs.Where(l => l.TimeStamp > sheildEffect.StartTime && l.TimeStamp <= sheildEffect.StopTime.AddSeconds(1.5)).ToList();
                        if (absorbsDuringShield.Count == 0)
                            continue;
                        _totalSheildingProvided.Add(new SheildingEvent { SheildValue = absorbsDuringShield.Sum(l => l.Value.Modifier.DblValue), SheildingTime = sheildEffect.StopTime });
                    }


                    foreach (var sheild in _totalSheildingProvided)
                    {
                        var logToInsertAfter = logs.FirstOrDefault(l => l.TimeStamp > sheild.SheildingTime);
                        if (logToInsertAfter == null)
                            continue;
                        var indexToInsert = logs.IndexOf(logToInsertAfter);
                        var sheildLog = new ParsedLogEntry
                        {
                            TimeStamp = sheild.SheildingTime,
                            Ability = "Healer Bubble",
                            Effect = new Effect()
                            {
                                EffectType = EffectType.Apply,
                                EffectName = "Sheild"
                            },
                            SourceInfo = new EntityInfo { Entity = source },
                            TargetInfo = new EntityInfo() { Entity = recipient},
                            Value = new Value
                            {
                                DblValue = sheild.SheildValue,
                                ValueType = DamageType.shield
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
