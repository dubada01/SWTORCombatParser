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

            foreach (var participant in combat.CharacterParticipants)
            {
                combat.Logs[participant].RemoveAll(l => l.Ability == "Healer Bubble");
                combat.SheildingProvidedLogs[participant] = new List<ParsedLogEntry>();
                combat.TotalProvidedSheilding[participant] = 0;

                var healingShieldModifiers = modifiers.Where(m => (m.Name == "Static Barrier" || m.Name == "Force Armor") && m.Source == participant).ToList();
                var sheildingLogs = allPriticipantSheildingLogs.SelectMany(l =>l.Value).ToList();
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
                    var logToInsertAfter = combat.Logs[participant].FirstOrDefault(l => l.TimeStamp > sheild.SheildingTime);
                    if (logToInsertAfter == null)
                        continue;
                    var indexToInsert = combat.Logs[participant].IndexOf(logToInsertAfter);
                    var sheildLog = new ParsedLogEntry
                    {
                        TimeStamp = sheild.SheildingTime,
                        Ability = "Healer Bubble",
                        Effect = new Effect()
                        {
                            EffectType = EffectType.Apply,
                            EffectName = "Sheild"
                        },
                        Source = participant,
                        Target = new Entity(),
                        Value = new Value
                        {
                            DblValue = sheild.SheildValue,
                            ValueType = DamageType.shield
                        }
                    };
                    combat.Logs[participant].Insert(
                        indexToInsert, sheildLog
                        );
                    combat.SheildingProvidedLogs[participant].Add(sheildLog);
                    combat.TotalProvidedSheilding[participant] += sheild.SheildValue;
                }
            }

        }
    }
}
