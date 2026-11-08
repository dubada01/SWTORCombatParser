using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;
using SWTORCombatParser.Model.RunRace;

namespace SWTORCombatParser_Test;

public class RunRaceTests
{
    private static readonly DateTime Entered = new(2026, 11, 7, 18, 0, 0);
    private static readonly RunCohort Cohort = new("Test operation", "Story", "8");

    private static RunRaceTracker Tracker(params string[] bosses)
    {
        var tracker = new RunRaceTracker();
        tracker.Begin(Cohort, bosses, Entered);
        return tracker;
    }

    [Test]
    public void LastBossAloneDoesNotSilentlyBecomeACompleteRun()
    {
        var tracker = Tracker("First", "Last");
        tracker.AddAttempt("Last", Entered.AddSeconds(60), Entered.AddSeconds(120), true);
        Assert.That(tracker.IsComplete, Is.False);
        Assert.That(tracker.CompletedRun("Player"), Is.Null);
    }

    [Test]
    public void WipesTravelAndDuplicateFinishEventsProduceOneStableRun()
    {
        var tracker = Tracker("First", "Last");
        tracker.AddAttempt("First", Entered.AddSeconds(30), Entered.AddSeconds(60), false);
        tracker.AddAttempt("First", Entered.AddSeconds(90), Entered.AddSeconds(120), true);
        Assert.That(tracker.AddAttempt("First", Entered.AddSeconds(90), Entered.AddSeconds(120), true), Is.False);
        tracker.AddAttempt("Last", Entered.AddSeconds(200), Entered.AddSeconds(260), true);
        tracker.Advance(Entered.AddHours(2));
        var run = tracker.CompletedRun("Player");
        Assert.Multiple(() =>
        {
            Assert.That(run, Is.Not.Null);
            Assert.That(run.DurationSeconds, Is.EqualTo(260));
            Assert.That(run.Splits.Select(split => split.Boss), Is.EqualTo(new[] { "First", "Last" }));
            Assert.That(tracker.Wipes, Is.EqualTo(1));
            Assert.That(tracker.Snapshot(null, "Personal best").Checkpoints[0].Attempts, Is.EqualTo(2));
        });
    }

    [Test]
    public void GhostsRequireSameCohortAndDifferentRun()
    {
        var tracker = Tracker("Boss");
        var ghost = Ghost(Entered.AddDays(-1), 120, "Boss");
        Assert.That(tracker.Accepts(ghost), Is.True);
        Assert.That(tracker.Accepts(ghost with { Cohort = Cohort with { Difficulty = "Veteran" } }), Is.False);
        Assert.That(tracker.Accepts(ghost with { Cohort = Cohort with { GroupSize = "16" } }), Is.False);
        Assert.That(tracker.Accepts(ghost with { Entered = Entered }), Is.False);
    }

    [Test]
    public void DifferentCheckpointRoutesDoNotReportMisleadingDelta()
    {
        var tracker = Tracker("First", "Last");
        tracker.AddAttempt("Last", Entered.AddSeconds(10), Entered.AddSeconds(30), true);
        var ghost = new RunGhost(Cohort, Entered.AddDays(-1), Entered.AddDays(-1).AddSeconds(100), "Player",
            new[] { new RunSplit("First", 10, 40), new RunSplit("Last", 60, 100) });
        var checkpoint = tracker.Snapshot(ghost, "Personal best").Checkpoints.Single(row => row.Boss == "Last");
        Assert.That(checkpoint.RouteMatches, Is.False);
        Assert.That(checkpoint.DeltaSeconds, Is.Null);
    }

    [Test]
    public void MatchingCheckpointReportsAheadFromAreaEntry()
    {
        var tracker = Tracker("Boss");
        tracker.AddAttempt("Boss", Entered.AddSeconds(30), Entered.AddSeconds(90), true);
        var ghost = Ghost(Entered.AddDays(-1), 120, "Boss");
        Assert.That(tracker.Snapshot(ghost, "Personal best").Checkpoints[0].DeltaSeconds, Is.EqualTo(-30));
    }

    [Test]
    public void OptionalBossRouteCompletesAutomaticallyAtFinalKill()
    {
        var tracker = Tracker("First", "Optional", "Last");
        Assert.That(tracker.IsComplete, Is.False);
        tracker.AddAttempt("First", Entered.AddSeconds(10), Entered.AddSeconds(30), true);
        tracker.AddAttempt("Last", Entered.AddSeconds(60), Entered.AddSeconds(90), true);
        tracker.Advance(Entered.AddSeconds(150));
        Assert.That(tracker.IsComplete, Is.True);
        var run = tracker.CompletedRun("Player");
        Assert.Multiple(() =>
        {
            Assert.That(run.IsCustomRoute, Is.True);
            Assert.That(run.RouteKey, Is.Not.EqualTo("catalog"));
            Assert.That(run.Splits.Count, Is.EqualTo(2));
            Assert.That(run.DurationSeconds, Is.EqualTo(90));
        });
    }

    [Test]
    public void NewVisitResetsAllCheckpointAndWipeState()
    {
        var tracker = Tracker("Boss");
        tracker.AddAttempt("Boss", Entered, Entered.AddSeconds(10), false);
        tracker.Begin(Cohort, new[] { "Boss" }, Entered.AddHours(1));
        Assert.That(tracker.Wipes, Is.Zero);
        Assert.That(tracker.Snapshot(null, "Personal best").ElapsedSeconds, Is.Zero);
        Assert.That(tracker.AddAttempt("Boss", Entered, Entered.AddSeconds(20), true), Is.False);
    }

    [Test]
    public void StoreRetainsPersonalBestAndCustomRouteIndependently()
    {
        var directory = Path.Combine(Path.GetTempPath(), "OrbsRunRaceTests_" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "ghosts.json");
        try
        {
            var store = new RunGhostStore(path);
            for (int i = 0; i < 8; i++) store.Save(Ghost(Entered.AddDays(i), 60 + i * 10, "Boss"));
            store.Save(Ghost(Entered.AddDays(10), 20, "Other boss") with { IsCustomRoute = true });
            var loaded = new RunGhostStore(path).Read();
            Assert.That(loaded.Count(run => !run.IsCustomRoute), Is.EqualTo(6));
            Assert.That(loaded.Where(run => !run.IsCustomRoute).Min(run => run.DurationSeconds), Is.EqualTo(60));
            Assert.That(loaded.Count(run => run.IsCustomRoute), Is.EqualTo(1));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Test]
    public void CorruptGhostDurationsCannotOverflowTheDisplay()
    {
        var directory = Path.Combine(Path.GetTempPath(), "OrbsRunRaceTests_" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "ghosts.json");
        try
        {
            Directory.CreateDirectory(directory);
            var invalid = Ghost(Entered, 30, "Boss") with { Splits = new[] { new RunSplit("Boss", 0, double.MaxValue) } };
            File.WriteAllText(path, JsonSerializer.Serialize(new[] { invalid }));
            Assert.That(new RunGhostStore(path).Read(), Is.Empty);
            File.WriteAllText(path, "[null]");
            Assert.That(new RunGhostStore(path).Read(), Is.Empty);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    private static RunGhost Ghost(DateTime entered, double seconds, string boss) =>
        new(Cohort, entered, entered.AddSeconds(seconds), "Player", new[] { new RunSplit(boss, 0, seconds) });
}
