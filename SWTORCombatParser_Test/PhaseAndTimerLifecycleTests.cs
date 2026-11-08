using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Phases;
using SWTORCombatParser.Model.Timers;

namespace SWTORCombatParser_Test;

[TestFixture, NonParallelizable]
public class PhaseAndTimerLifecycleTests
{
    [Test]
    public void LivePhaseStaysOpenUntilItsHpEndTrigger()
    {
        var flags = BindingFlags.NonPublic | BindingFlags.Static;
        var type = typeof(PhaseManager);
        var phasesField = type.GetField("< _loadedPhases >k__BackingField".Replace(" ", ""), flags)!;
        var bossField = type.GetField("_currentBossName", flags)!;
        var originalPhases = phasesField.GetValue(null);
        var originalBoss = bossField.GetValue(null);
        var phase = new Phase
        {
            Id = Guid.NewGuid(), Name = "Test", SourcePt2 = "Boss",
            StartTrigger = PhaseTrigger.EntityHP, EndTrigger = PhaseTrigger.EntityHP,
            StartArgs = new PhaseArgs { EntityIds = new List<long> { 5 }, HPPercentage = 70 },
            EndArgs = new PhaseArgs { EntityIds = new List<long> { 5 }, HPPercentage = 30 }
        };
        var time = new DateTime(2025, 1, 1, 12, 0, 0);
        var update = type.GetMethod("UpdatePhases", flags)!;
        ParsedLogEntry Log(int seconds, uint hp) => new()
        {
            TimeStamp = time.AddSeconds(seconds),
            SourceInfo = new EntityInfo { Entity = new Entity { LogId = 5 }, MaxHP = 100, CurrentHP = hp },
            TargetInfo = new EntityInfo(), Effect = new Effect()
        };
        try
        {
            phasesField.SetValue(null, new List<Phase> { phase });
            bossField.SetValue(null, "Boss");
            update.Invoke(null, new object[] { new CombatStatusUpdate { Type = UpdateType.Start, CombatStartTime = time } });
            update.Invoke(null, new object[] { new CombatStatusUpdate { Type = UpdateType.Update, Logs = new List<ParsedLogEntry> { Log(1, 60) } } });
            Assert.That(PhaseManager.ActivePhases.Single().PhaseEnd, Is.EqualTo(DateTime.MinValue));
            update.Invoke(null, new object[] { new CombatStatusUpdate { Type = UpdateType.Update, Logs = new List<ParsedLogEntry> { Log(2, 20) } } });
            Assert.That(PhaseManager.ActivePhases.Single().PhaseEnd, Is.EqualTo(time.AddSeconds(2)));
        }
        finally
        {
            type.GetMethod("ResetPhases", flags)!.Invoke(null, null);
            phasesField.SetValue(null, originalPhases);
            bossField.SetValue(null, originalBoss);
        }
    }

    [Test]
    public void ReplacedTimerCanDetachAllStaticStreamSubscriptions()
    {
        var flags = BindingFlags.NonPublic | BindingFlags.Static;
        var timer = new TimerInstance(new SWTORCombatParser.DataStructures.Timer());
        try
        {
            foreach (var eventName in new[] { "CombatUpdated", "HistoricalLogsFinished", "HistoricalLogsStarted" })
            {
                var field = typeof(CombatLogStreamer).GetField(eventName, flags)!;
                var handlers = ((Delegate)field.GetValue(null)!).GetInvocationList();
                Assert.That(handlers.Count(h => ReferenceEquals(h.Target, timer)), Is.EqualTo(1));
            }
            timer.DetachFromLogStream();
            foreach (var eventName in new[] { "CombatUpdated", "HistoricalLogsFinished", "HistoricalLogsStarted" })
            {
                var field = typeof(CombatLogStreamer).GetField(eventName, flags)!;
                var handlers = ((Delegate)field.GetValue(null)!).GetInvocationList();
                Assert.That(handlers.Any(h => ReferenceEquals(h.Target, timer)), Is.False);
            }
        }
        finally
        {
            timer.DetachFromLogStream();
        }
    }
}
