using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.RunRace;

namespace SWTORCombatParser_Test;

[NonParallelizable]
public class RunRaceIntegrationTests
{
    private static readonly DateTime Entered = new(2026, 11, 7, 18, 0, 0);
    private LogState _previousState;
    private string _temporaryDirectory;
    private RunRaceService _service;

    [SetUp]
    public void SetUp()
    {
        _previousState = CombatLogStateBuilder.CurrentState;
        CombatLogStateBuilder.CurrentState = new LogState();
        _temporaryDirectory = Path.Combine(Path.GetTempPath(), "orbs-run-import-tests-" + Guid.NewGuid().ToString("N"));
        _service = new RunRaceService(new RunGhostStore(Path.Combine(_temporaryDirectory, "ghosts.json")));
    }

    [TearDown]
    public void TearDown()
    {
        _service.Dispose();
        CombatLogStateBuilder.CurrentState = _previousState;
        if (Directory.Exists(_temporaryDirectory)) Directory.Delete(_temporaryDirectory, true);
    }

    [Test]
    public void RepeatedAreaAnnouncementsRetainTheWholeObservedRoute()
    {
        var firstArea = Flashpoint();
        var repeatedArea = Flashpoint();
        Enter(Entered, firstArea);
        Enter(Entered.AddMinutes(1), repeatedArea);
        _service.ImportHistoricalCombats(new[]
        {
            Kill(firstArea, "First", Entered.AddSeconds(10), Entered.AddSeconds(30)),
            Kill(repeatedArea, "Last", Entered.AddMinutes(2), Entered.AddMinutes(3))
        });

        var snapshot = _service.Snapshot;
        Assert.That(snapshot.Entered, Is.EqualTo(Entered));
        Assert.That(snapshot.Kills, Is.EqualTo(2));
        Assert.That(snapshot.Checkpoints.Single(c => c.Boss == "Last").Seconds, Is.EqualTo(180));
        Assert.That(snapshot.IsComplete, Is.True, "The final kill completes a route even when an optional boss is skipped.");
        Assert.That(snapshot.IsHistorical, Is.True);
        _service.Dispose();
        Assert.That(new RunGhostStore(Path.Combine(_temporaryDirectory, "ghosts.json")).Read().Single().IsComplete, Is.True);
    }

    [Test]
    public void ReturningToTheSameFlashpointAfterFleetStartsANewPreview()
    {
        var previousVisit = Flashpoint();
        var nextVisit = Flashpoint();
        Enter(Entered, previousVisit);
        Enter(Entered.AddMinutes(5), new EncounterInfo { Name = "Fleet", EncounterType = EncounterType.OpenWorld });
        Enter(Entered.AddMinutes(6), nextVisit);
        _service.ImportHistoricalCombats(new[]
        {
            Kill(previousVisit, "First", Entered.AddSeconds(10), Entered.AddSeconds(30)),
            Kill(nextVisit, "Last", Entered.AddMinutes(7), Entered.AddMinutes(8))
        });

        var snapshot = _service.Snapshot;
        Assert.That(snapshot.Entered, Is.EqualTo(Entered.AddMinutes(6)));
        Assert.That(snapshot.Kills, Is.EqualTo(1));
        Assert.That(snapshot.Checkpoints.Any(c => c.Boss == "First" && c.Attempts > 0), Is.False);
        Assert.That(snapshot.Checkpoints.Single(c => c.Boss == "Last").Seconds, Is.EqualTo(120));
    }

    [Test]
    public void LoadingAnEmptyLogClearsThePreviousHistoricalRun()
    {
        var area = Flashpoint();
        Enter(Entered, area);
        _service.ImportHistoricalCombats(new[] { Kill(area, "First", Entered.AddSeconds(10), Entered.AddSeconds(30)) });
        Assert.That(_service.Snapshot.Cohort, Is.Not.Null);

        CombatLogStateBuilder.CurrentState = new LogState();
        _service.ImportHistoricalCombats(Array.Empty<Combat>());
        Assert.That(_service.Snapshot.Cohort, Is.Null);
        Assert.That(_service.Snapshot.Checkpoints, Is.Empty);
    }

    private static EncounterInfo Flashpoint() => new()
    {
        Name = "Test flashpoint", EncounterType = EncounterType.Flashpoint,
        Difficutly = "Veteran", NumberOfPlayer = "4",
        BossInfos = new List<BossInfo>
        {
            new() { EncounterName = "First" }, new() { EncounterName = "Optional" }, new() { EncounterName = "Last" }
        }
    };

    private static Combat Kill(EncounterInfo area, string boss, DateTime start, DateTime end) => new()
    {
        ParentEncounter = area, EncounterBossDifficultyParts = (boss, area.Difficutly, area.NumberOfPlayer),
        StartTime = start, EndTime = end, BossKillOverride = true
    };

    private static void Enter(DateTime time, EncounterInfo area) => CombatLogStateBuilder.CurrentState.EncounterEnteredInfo[time] = area;
}
