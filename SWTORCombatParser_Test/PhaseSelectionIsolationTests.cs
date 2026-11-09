using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using NUnit.Framework;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Phases;

namespace SWTORCombatParser_Test;

[NonParallelizable]
public class PhaseSelectionIsolationTests
{
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
    private static readonly DateTime Start = new(2025, 1, 1, 12, 0, 0);
    private string _directory;
    private object _oldPhasePath;
    private object _oldDefinitions;
    private object _oldBoss;
    private LogState _oldLogState;
    private Combat _oldCombat;
    private Entity _npc;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "OrbsPhaseSelectionTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        var phasePath = typeof(DefaultPhaseManager).GetField("_phaseFile", Static)!;
        _oldPhasePath = phasePath.GetValue(null);
        phasePath.SetValue(null, Path.Combine(_directory, "phases.json"));
        _oldDefinitions = typeof(PhaseManager).GetField("<_loadedPhases>k__BackingField", Static)!.GetValue(null);
        _oldBoss = typeof(PhaseManager).GetField("_currentBossName", Static)!.GetValue(null);
        _oldLogState = CombatLogStateBuilder.CurrentState;
        _oldCombat = CombatIdentifier.CurrentCombat;
        CombatLogStateBuilder.ClearState();
        Invoke("ResetPhases");
        _npc = new Entity { Id = 5, LogId = 5, Name = "Boss" };
    }

    [TearDown]
    public void TearDown()
    {
        Invoke("ResetPhases");
        typeof(DefaultPhaseManager).GetField("_phaseFile", Static)!.SetValue(null, _oldPhasePath);
        typeof(PhaseManager).GetField("<_loadedPhases>k__BackingField", Static)!.SetValue(null, _oldDefinitions);
        typeof(PhaseManager).GetField("_currentBossName", Static)!.SetValue(null, _oldBoss);
        CombatLogStateBuilder.CurrentState = _oldLogState;
        CombatIdentifier.CurrentCombat = _oldCombat;
        Directory.Delete(_directory, true);
    }

    [TestCase(PhaseTrigger.CombatStart)]
    [TestCase(PhaseTrigger.EntitySpawn)]
    [TestCase(PhaseTrigger.EntityDeath)]
    [TestCase(PhaseTrigger.EntityHP)]
    [TestCase(PhaseTrigger.AbilityUsage)]
    [TestCase(PhaseTrigger.AbilityCancel)]
    [TestCase(PhaseTrigger.EffectGain)]
    [TestCase(PhaseTrigger.EffectLoss)]
    [TestCase(PhaseTrigger.CombatDuration)]
    public void LiveAndHistoricalTriggersKeepTheirExpectedIntervals(PhaseTrigger trigger)
    {
        var phase = Definition("Boss", trigger);
        Configure(phase);
        var start = Line(1);
        if (trigger == PhaseTrigger.EntityDeath) start.Effect.EffectId = _7_0LogParsing.DeathCombatId;
        if (trigger == PhaseTrigger.EntityHP) start.TargetInfo.CurrentHP = 60;
        if (trigger == PhaseTrigger.AbilityUsage) start.Effect.EffectId = _7_0LogParsing.AbilityActivateId;
        if (trigger == PhaseTrigger.AbilityCancel) start.Effect.EffectId = _7_0LogParsing.AbilityCancelId;
        if (trigger == PhaseTrigger.EffectGain) start.Effect.EffectType = EffectType.Apply;
        if (trigger == PhaseTrigger.EffectLoss) start.Effect.EffectType = EffectType.Remove;
        var end = Line(3);
        end.Effect = new Effect { EffectId = 900, EffectType = EffectType.Remove };
        var combat = Combat("Boss", Start, Start.AddSeconds(4), start, end);

        Update(new CombatStatusUpdate { Type = UpdateType.Start, CombatStartTime = Start });
        Update(new CombatStatusUpdate { Type = UpdateType.Update, Logs = new List<ParsedLogEntry> { start } });
        Assert.That(PhaseManager.GetLivePhaseSnapshot().Single().PhaseEnd, Is.EqualTo(DateTime.MinValue));
        Update(new CombatStatusUpdate { Type = UpdateType.Update, Logs = new List<ParsedLogEntry> { end } });
        var live = PhaseManager.GetLivePhaseSnapshot().Single();
        var prepared = Invoke("PrepareSelection", combat, null);
        Invoke("PublishSelection", combat, prepared, false);
        var historical = PhaseManager.ActivePhases.Single();

        Assert.Multiple(() =>
        {
            Assert.That(live.SourcePhase.Id, Is.EqualTo(phase.Id));
            Assert.That(live.PhaseStart, Is.EqualTo(Start.AddSeconds(1)));
            Assert.That(live.PhaseEnd, Is.EqualTo(Start.AddSeconds(3)));
            Assert.That(historical.SourcePhase.Id, Is.EqualTo(live.SourcePhase.Id));
            Assert.That(historical.PhaseStart, Is.EqualTo(live.PhaseStart));
            Assert.That(historical.PhaseEnd, Is.EqualTo(live.PhaseEnd));
            Assert.That(PhaseManager.GetLivePhaseIds(), Does.Contain(phase.Id));
        });
    }

    [Test]
    public void HistoricalSelectionCannotResetOrCloseTheLiveAccumulator()
    {
        var livePhase = Definition("Boss", PhaseTrigger.CombatStart);
        var oldPhase = Definition("Old boss", PhaseTrigger.CombatStart);
        Configure(livePhase, oldPhase);
        Update(new CombatStatusUpdate { Type = UpdateType.Start, CombatStartTime = Start });
        Update(new CombatStatusUpdate { Type = UpdateType.Update, Logs = new List<ParsedLogEntry> { Line(1) } });
        var historical = Combat("Old boss", Start.AddMinutes(-5), Start.AddMinutes(-4), Line(-299));
        Invoke("PublishSelection", historical, Invoke("PrepareSelection", historical, null), false);

        var end = Line(3);
        end.Effect = new Effect { EffectType = EffectType.Remove, EffectId = 900 };
        Update(new CombatStatusUpdate { Type = UpdateType.Update, Logs = new List<ParsedLogEntry> { end } });
        Assert.Multiple(() =>
        {
            Assert.That(PhaseManager.GetLivePhaseSnapshot().Single().SourcePhase.Id, Is.EqualTo(livePhase.Id));
            Assert.That(PhaseManager.GetLivePhaseSnapshot().Single().PhaseEnd, Is.EqualTo(end.TimeStamp));
            Assert.That(PhaseManager.ActivePhases.Single().SourcePhase.Id, Is.EqualTo(oldPhase.Id));
            Assert.That(PhaseManager.ActivePhases.Single().PhaseEnd, Is.EqualTo(DateTime.MinValue));
            Assert.That(PhaseManager.GetLivePhaseIds(), Is.EquivalentTo(new[] { livePhase.Id }));
            Assert.That(PhaseManager.IsDisplayingCombat(historical), Is.True);
            Assert.That(PhaseManager.IsDisplayingCombat(Combat("Boss", Start, end.TimeStamp)), Is.False);
        });

        var current = Combat("Boss", Start, end.TimeStamp);
        Invoke("PublishSelection", current, null, true);
        Assert.That(PhaseManager.ActivePhases.Single().PhaseEnd, Is.EqualTo(end.TimeStamp));
        Assert.That(PhaseManager.IsDisplayingCombat(current), Is.True);
    }

    [Test]
    public void CancelledPreparationAndSupersededPublicationLeaveNewerDisplayAlone()
    {
        Configure(Definition("Boss", PhaseTrigger.CombatStart));
        var first = Combat("Boss", Start, Start.AddSeconds(10), Line(1));
        var second = Combat("Boss", Start.AddMinutes(1), Start.AddMinutes(2), Line(61));
        var firstSnapshot = Invoke("PrepareSelection", first, null);
        Invoke("PublishSelection", first, firstSnapshot, false);
        var generation = (long)typeof(PhaseManager).GetField("_displayGeneration", Static)!.GetValue(null)!;
        Invoke("PublishSelection", second, Invoke("PrepareSelection", second, null), false);
        Invoke("PublishDisplay", firstSnapshot, generation, true);
        Assert.That(PhaseManager.IsDisplayingCombat(second), Is.True);
        Assert.That(PhaseManager.ActivePhases.Single().PhaseStart, Is.EqualTo(Start.AddSeconds(61)));
        Assert.That(Invoke("PrepareSelection", first, (Func<bool>)(() => false)), Is.Null);
        Assert.That(PhaseManager.ActivePhases.Single().PhaseStart, Is.EqualTo(Start.AddSeconds(61)));
    }

    [Test]
    public void ReturnedSnapshotsCannotMutateLiveOrDisplayedPhaseIntervals()
    {
        Configure(Definition("Boss", PhaseTrigger.CombatStart));
        Update(new CombatStatusUpdate { Type = UpdateType.Start, CombatStartTime = Start });
        Update(new CombatStatusUpdate { Type = UpdateType.Update, Logs = new List<ParsedLogEntry> { Line(1) } });
        PhaseManager.GetLivePhaseSnapshot().Single().PhaseEnd = Start.AddDays(1);
        PhaseManager.GetDisplayedPhaseSnapshot().Single().PhaseEnd = Start.AddDays(1);
        Assert.That(PhaseManager.GetLivePhaseSnapshot().Single().PhaseEnd, Is.EqualTo(DateTime.MinValue));
        Assert.That(PhaseManager.ActivePhases.Single().PhaseEnd, Is.EqualTo(DateTime.MinValue));
    }

    [Test]
    public void ReusedHistoricalPhasesAreDetachedAndInvalidateWhenDefinitionsOrEventsChange()
    {
        var definition = Definition("Boss", PhaseTrigger.CombatStart);
        Configure(definition);
        var combat = Combat("Boss", Start, Start.AddSeconds(10), Line(1));
        Invoke("PublishSelection", combat, Invoke("PrepareSelection", combat, null), false);
        PhaseManager.ActivePhases.Single().PhaseStart = Start.AddDays(1);
        Invoke("PublishSelection", combat, Invoke("PrepareSelection", combat, null), false);
        Assert.That(PhaseManager.ActivePhases.Single().PhaseStart, Is.EqualTo(Start.AddSeconds(1)));
        var removal = Line(3); removal.Effect = new Effect { EffectId = 900, EffectType = EffectType.Remove };
        combat.AllLogs[3] = removal;
        Invoke("PublishSelection", combat, Invoke("PrepareSelection", combat, null), false);
        Assert.That(PhaseManager.ActivePhases.Single().PhaseEnd, Is.EqualTo(Start.AddSeconds(3)));
        Configure(Definition("Another boss", PhaseTrigger.CombatStart));
        Invoke("PublishSelection", combat, Invoke("PrepareSelection", combat, null), false);
        Assert.That(PhaseManager.ActivePhases, Is.Empty);
    }

    [Test]
    public void HistoricalSelectionOnlyWaitsForUnpreparedMatchingPhases()
    {
        Configure(Definition("Boss", PhaseTrigger.CombatStart));
        var boss = Combat("Boss", Start, Start.AddSeconds(10), Line(1));
        var trash = Combat("", Start, Start.AddSeconds(10), Line(1));

        Assert.That(PhaseManager.GetPreparedSelectionIfReady(boss), Is.Null);
        Assert.That(PhaseManager.GetPreparedSelectionIfReady(trash)!.Clean, Is.Empty);
        Assert.That(PhaseManager.PrepareSelection(boss)!.Clean, Has.Count.EqualTo(1));

        var warm = PhaseManager.GetPreparedSelectionIfReady(boss)!;
        warm.Clean.Single().PhaseStart = Start.AddDays(1);
        Assert.That(PhaseManager.GetPreparedSelectionIfReady(boss)!.Clean.Single().PhaseStart, Is.EqualTo(Start.AddSeconds(1)));

        boss.AllLogs[3] = Line(3);
        Assert.That(PhaseManager.GetPreparedSelectionIfReady(boss), Is.Null);
        Configure(Definition("Other", PhaseTrigger.CombatStart));
        Assert.That(PhaseManager.GetPreparedSelectionIfReady(boss)!.Clean, Is.Empty);
    }

    [Test]
    public void SelectingAnOldPhaseDoesNotChangeLiveOrUnrelatedCombatDuration()
    {
        var live = Combat("Boss", Start, Start.AddSeconds(120));
        var other = Combat("Other", Start.AddHours(-1), Start.AddHours(-1).AddSeconds(60));
        PhaseManager.SelectedPhases = new() { new() { PhaseStart = Start.AddHours(-2), PhaseEnd = Start.AddHours(-2).AddSeconds(5) } };
        PhaseManager.SetSelectedPhaseDuration();
        Assert.That(live.DurationMS, Is.EqualTo(120000));
        Assert.That(other.DurationMS, Is.EqualTo(60000));
    }

    [Test]
    public void SparseDisjointPhaseCopyKeepsExplicitDurationAfterSelectionChanges()
    {
        var combat = Combat("", Start, Start.AddSeconds(100), DamageLine(11), DamageLine(62));
        var phases = PhaseMap((10, 30), (60, 80));
        var scoped = combat.GetPhaseCopy(phases);
        Assert.That(scoped.DurationMS, Is.EqualTo(40000), "Two 20-second windows, independent of the log span.");
        PhaseManager.SelectedPhases = new() { new() { PhaseStart = Start, PhaseEnd = Start.AddSeconds(1) } };
        PhaseManager.SetSelectedPhaseDuration();
        Assert.That(scoped.DurationMS, Is.EqualTo(40000));
        Assert.That(combat.DurationMS, Is.EqualTo(100000));
    }

    [Test]
    public void OpenHistoricalPhaseUsesItsSourceCombatEndAndSparseWindowDuration()
    {
        var combat = Combat("", Start, Start.AddSeconds(100), DamageLine(91), DamageLine(92));
        CombatIdentifier.CurrentCombat = Combat("Live", Start.AddHours(1), Start.AddHours(1).AddSeconds(300));
        var phases = new ConcurrentDictionary<Guid, PhaseInstance>();
        phases[Guid.NewGuid()] = new PhaseInstance { PhaseStart = Start.AddSeconds(90) };
        var scoped = combat.GetPhaseCopy(phases);
        Assert.That(scoped.DurationMS, Is.EqualTo(10000));
        Assert.That(scoped.AllLogs, Has.Count.EqualTo(2));
    }

    [Test]
    public void ZeroLengthPhaseWindowsDoNotFallBackToTheGapBetweenLogs()
    {
        var combat = Combat("", Start, Start.AddSeconds(100), DamageLine(10), DamageLine(60));
        Assert.That(combat.GetPhaseCopy(PhaseMap((10, 10), (60, 60))).DurationMS, Is.Zero);
    }

    private void Configure(params Phase[] phases)
    {
        File.WriteAllText(Path.Combine(_directory, "phases.json"), JsonConvert.SerializeObject(phases));
        typeof(PhaseManager).GetField("<_loadedPhases>k__BackingField", Static)!.SetValue(null, phases);
        typeof(PhaseManager).GetField("_currentBossName", Static)!.SetValue(null, "Boss");
    }

    private static Phase Definition(string boss, PhaseTrigger start) => new()
    {
        Id = Guid.NewGuid(), Name = boss + " phase", PhaseSource = "Test|" + boss, SourcePt2 = boss,
        StartTrigger = start, EndTrigger = PhaseTrigger.EffectLoss,
        StartArgs = new PhaseArgs { EntityIds = new() { 5 }, AbilityIds = new() { "42" }, EffectIds = new() { "42" }, HPPercentage = 70, CombatDuration = 0.5 },
        EndArgs = new PhaseArgs { EffectIds = new() { "900" } }
    };

    private ParsedLogEntry Line(int seconds) => new()
    {
        LogLineNumber = seconds, TimeStamp = Start.AddSeconds(seconds), Ability = "Ability", AbilityId = 42,
        SourceInfo = new EntityInfo { Entity = _npc, CurrentHP = 100, MaxHP = 100 },
        TargetInfo = new EntityInfo { Entity = _npc, CurrentHP = 100, MaxHP = 100 },
        Effect = new Effect { EffectId = 42 }, Value = new Value()
    };

    private ParsedLogEntry DamageLine(int seconds)
    {
        var line = Line(seconds);
        line.Effect = new Effect { EffectId = _7_0LogParsing._damageEffectId, EffectType = EffectType.Apply };
        line.Value = new Value { DblValue = 100, EffectiveDblValue = 100 };
        return line;
    }

    private static Combat Combat(string boss, DateTime start, DateTime end, params ParsedLogEntry[] logs) => new()
    {
        StartTime = start, EndTime = end, EncounterBossDifficultyParts = (boss, "", ""),
        AllLogs = new ConcurrentDictionary<long, ParsedLogEntry>(logs.Select(log => new KeyValuePair<long, ParsedLogEntry>(log.LogLineNumber, log)))
    };

    private static ConcurrentDictionary<Guid, PhaseInstance> PhaseMap(params (int Start, int End)[] spans) => new(spans.Select(span =>
        new KeyValuePair<Guid, PhaseInstance>(Guid.NewGuid(), new PhaseInstance { PhaseStart = Start.AddSeconds(span.Start), PhaseEnd = Start.AddSeconds(span.End) })));

    private static object Invoke(string name, params object[] arguments) => typeof(PhaseManager).GetMethod(name, Static)!.Invoke(null, arguments);
    private static void Update(CombatStatusUpdate update) => Invoke("UpdatePhases", update);
}
