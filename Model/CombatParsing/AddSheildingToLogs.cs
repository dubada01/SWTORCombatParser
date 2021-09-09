﻿using SWTORCombatParser.Model.CloudRaiding;
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
    //public static class AddSheildingToLogs
    //{
    //    public static void AddSheildLogs(LogState state, List<ParsedLogEntry> sheildingLogs, Combat sheildSource)
    //    {
    //        var modifiers = state.Modifiers;
    //        var healingShieldModifiers = modifiers.Where(m => (m.Name == "Static Barrier" || m.Name == "Force Armor") && m.Source == sheildSource.Owner).ToList();
    //        if (healingShieldModifiers.Count == 0)
    //            return;
    //        List<SheildingEvent> _totalSheildingProvided = new List<SheildingEvent>();
    //        foreach (var sheildEffect in healingShieldModifiers)
    //        {
    //            var absorbsDuringShield = sheildingLogs.Where(l => l.TimeStamp > sheildEffect.StartTime && l.TimeStamp <= sheildEffect.StopTime.AddSeconds(1.5)).ToList();
    //            if (absorbsDuringShield.Count == 0)
    //                continue;
    //            _totalSheildingProvided.Add(new SheildingEvent { SheildValue = absorbsDuringShield.Sum(l => l.Value.Modifier.DblValue), SheildingTime = sheildEffect.StopTime });
    //        }
    //        //foreach (var source in _totalSheildingProvided.Keys)
    //        //{
    //        //var sheildingSource = commonCombats.First(m => m.Participant.PlayerName == source.Name);

    //        //reset sheilds
    //        sheildSource.Logs.RemoveAll(l => l.Ability == "Healer Bubble");
    //        sheildSource.SheildingProvidedLogs.Clear();
    //        sheildSource.TotalProvidedSheilding = 0;

    //        foreach (var sheild in _totalSheildingProvided)
    //        {
    //            var logToInsertAfter = sheildSource.Logs.FirstOrDefault(l => l.TimeStamp > sheild.SheildingTime);
    //            if (logToInsertAfter == null)
    //                continue;
    //            var indexToInsert = sheildSource.Logs.IndexOf(logToInsertAfter);
    //            var sheildLog = new ParsedLogEntry
    //            {
    //                TimeStamp = sheild.SheildingTime,
    //                Ability = "Healer Bubble",
    //                Effect = new Effect()
    //                {
    //                    EffectType = EffectType.Apply,
    //                    EffectName = "Sheild"
    //                },
    //                Source = sheildSource.Owner,
    //                Target = new Entity(),
    //                Value = new Value
    //                {
    //                    DblValue = sheild.SheildValue,
    //                    ValueType = DamageType.shield
    //                }
    //            };
    //            sheildSource.Logs.Insert(
    //                indexToInsert, sheildLog
    //                );
    //            sheildSource.SheildingProvidedLogs.Add(sheildLog);
    //            sheildSource.TotalProvidedSheilding += sheild.SheildValue;
    //            //}
    //            //sheildingSource.Combat.TotalProvidedSheilding = _totalSheildingProvided[source];
    //        }
    //    }
    //}
}