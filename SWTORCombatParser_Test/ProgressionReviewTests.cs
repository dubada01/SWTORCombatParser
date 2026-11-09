using System;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Phases;
using SWTORCombatParser.Model.Progression;

namespace SWTORCombatParser_Test;

public class ProgressionReviewTests
{
    private static readonly DateTime Start = new(2026, 3, 7, 20, 0, 0);
    private static readonly string RaidId = new('A', 64);
    private static readonly ProgressionRaidChoice Raid = new(RaidId, "Operation", Start, false);
    private static readonly ProgressionCohort Cohort = new("Operation", "Master", "8-player");
    private static readonly Entity Player = new() { Id = 1, Name = "Player", IsCharacter = true };
    private static readonly Entity Healer = new() { Id = 2, Name = "Healer", IsCharacter = true };
    private static readonly Entity Boss = new() { Id = 3, LogId = 300, Name = "Boss" };

    [Test]
    public void RecapDeduplicatesDeathsAndKeepsTheNextDeathAfterRevive()
    {
        var logs = new[]
        {
            Hit(1, 1, 10, "Opening strike", 45),
            Entry(2, 2, _7_0LogParsing._healEffectId, EffectType.Apply, Healer, Player, "Heal", 7, 60),
            Hit(3, 3, 11, "Heavy strike", 100),
            Death(4, 3), Death(5, 3),
            Entry(6, 5, _7_0LogParsing.RevivedCombatId, EffectType.Event, Healer, Player),
            Hit(7, 12, 12, "Slam", 120),
            Death(8, 13)
        };
        var summary = Build(logs, Phase("Opening", PhaseTrigger.CombatStart), Phase("Burn", PhaseTrigger.CombatDuration, 10));
        Assert.Multiple(() =>
        {
            Assert.That(summary.Deaths, Has.Count.EqualTo(2));
            Assert.That(summary.FirstDeath!.Time, Is.EqualTo(Start.AddSeconds(3)));
            Assert.That(summary.FirstDeath.Phase, Is.EqualTo("Opening"));
            Assert.That(summary.FirstDeath.LastHit!.Ability, Is.EqualTo("Heavy strike"));
            Assert.That(summary.FirstDeath.LastHit.Amount, Is.EqualTo(100));
            Assert.That(summary.FirstDeath.Leadup.Select(item => item.Kind), Is.EqualTo(new[]
                { ProgressionEventKind.Damage, ProgressionEventKind.Heal, ProgressionEventKind.Damage }));
            Assert.That(summary.Deaths[1].Leadup.Select(item => item.Ability), Is.EqualTo(new[] { "Slam" }));
            Assert.That(summary.Deaths[1].Phase, Is.EqualTo("Burn"));
            Assert.That(summary.LastPhase, Is.EqualTo("Burn"));
            Assert.That(summary.EndingPhase, Is.EqualTo("Burn"));
        });
    }

    [Test]
    public void RecentDamageCanBeAmbiguousAndAnExplicitResetHasNoLastHit()
    {
        var logs = new[]
        {
            Hit(0, 1, 10, "One", 10), Hit(0, 1, 11, "Two", 20), Death(1, 2),
            Hit(2, 3, 12, "Stale", 30), Death(3, 9),
            Hit(4, 10, 13, "Recent", 40), Death(5, 11, "/stuck"),
            Hit(6, 12, 13, "Recent", 50), Entry(7, 13, _7_0LogParsing.DeathCombatId, EffectType.Event, Entity.EmptyEntity, Player),
            Hit(8, 14, 13, "Recent", 60), Death(9, 19)
        };
        var summary = Build(logs);
        Assert.Multiple(() =>
        {
            Assert.That(summary.Deaths, Has.Count.EqualTo(5));
            Assert.That(summary.Deaths.Take(4).All(death => death.LastHit == null), Is.True);
            Assert.That(summary.Deaths[2].IsExplicitReset, Is.True);
            Assert.That(summary.Deaths[3].IsExplicitReset, Is.False);
            Assert.That(summary.Deaths[4].LastHit!.Ability, Is.EqualTo("Recent"));
            Assert.That(summary.LastPhase, Is.Null);
            Assert.That(summary.Deaths.All(death => death.Phase == null), Is.True);
        });
    }

    [Test]
    public void LeadupIsBoundedAndDoesNotEraseTheLastKnownHit()
    {
        var logs = new[] { Hit(1, 12, 10, "Earlier strike", 100) }.Concat(Enumerable.Range(0, 25)
            .Select(index => Entry(index + 2, 13 + index / 20d, _7_0LogParsing._healEffectId, EffectType.Apply, Healer, Player, "Heal", 7, 1)))
            .Append(Death(27, 15)).ToArray();
        var death = Build(logs).FirstDeath!;
        Assert.That(death.Leadup, Has.Count.EqualTo(ProgressionSummaryBuilder.LeadupLimit));
        Assert.That(death.Leadup.All(item => item.Kind == ProgressionEventKind.Heal), Is.True);
        Assert.That(death.LastHit!.Ability, Is.EqualTo("Earlier strike"));
    }

    [Test]
    public void AReachedPhaseCanEndBeforeThePullEnds()
    {
        var phase = Phase("Opening", PhaseTrigger.CombatStart);
        phase.EndTrigger = PhaseTrigger.CombatDuration;
        phase.EndArgs.CombatDuration = 4;
        var summary = Build(new[] { Hit(1, 1, 10, "Hit", 100), Hit(2, 6, 10, "Hit", 50), Death(3, 8) }, phase);
        Assert.That(summary.LastPhase, Is.EqualTo("Opening"));
        Assert.That(summary.EndingPhase, Is.Null);
        Assert.That(summary.FirstDeath!.Phase, Is.Null);
    }

    [Test]
    public void ARepeatedPhaseDoesNotFillAnExplicitGap()
    {
        var phase = Phase("Cycle", PhaseTrigger.AbilityUsage);
        phase.StartArgs.AbilityIds.Add("Begin");
        phase.EndTrigger = PhaseTrigger.AbilityUsage;
        phase.EndArgs.AbilityIds.Add("End");
        ParsedLogEntry Cast(long line, int seconds, string ability) =>
            Entry(line, seconds, _7_0LogParsing.AbilityActivateId, EffectType.Event, Boss, Player, ability);
        var summary = Build(new[] { Cast(1, 1, "Begin"), Cast(2, 3, "End"), Death(3, 5), Cast(4, 7, "Begin"), Death(5, 9) }, phase);
        Assert.That(summary.Deaths[0].Phase, Is.Null);
        Assert.That(summary.Deaths[1].Phase, Is.EqualTo("Cycle"));
    }

    [Test]
    public void RecurrencesCountPullsAmongTheFirstThreeDeathsOfNonClears()
    {
        var summary = Build(new[]
        {
            Hit(1, 1, 10, "Shock", 100), Death(2, 2), Hit(3, 3, 10, "Shock", 100), Death(4, 4),
            Hit(5, 5, 10, "Shock", 100), Death(6, 6), Hit(7, 7, 11, "Cascade", 100), Death(8, 8)
        }, Phase("Burn", PhaseTrigger.CombatStart));
        var other = summary with { Metadata = summary.Metadata with { Id = "other" } };
        var kill = summary with { Metadata = summary.Metadata with { Id = "kill", WasKilled = true } };
        Assert.That(ProgressionPatterns.EarlyHits(new[] { summary, other, kill }), Is.EqualTo(new[] { new ProgressionPattern("Shock", 2) }));
        Assert.That(ProgressionPatterns.EndingPhases(new[] { summary, other, kill }), Is.EqualTo(new[] { new ProgressionPattern("Burn", 2) }));
    }

    [Test]
    public void CancellationDuringPhasePreparationDoesNotPublishAPartialRecap()
    {
        Assert.Throws<OperationCanceledException>(() => ProgressionSummaryBuilder.Build(Metadata(), new[] { Death(1, 1) },
            new[] { Phase("Opening", PhaseTrigger.CombatStart) }, "test", new CancellationToken(canceled: true)));
    }

    [Test]
    public void HistoryRoundTripsDetachedPullsAndTeamAssignmentsWithoutDuplicatingReloads()
    {
        WithStore(directory =>
        {
            var store = new ProgressionHistoryStore(directory);
            var summary = Build(new[] { Hit(1, 1, 10, "Shock", 90), Death(2, 2) });
            var second = summary with { Metadata = summary.Metadata with { Id = "second", Cohort = Cohort with { Difficulty = "Veteran", GroupSize = "16-player" } } };
            store.Upsert(Raid, new[] { summary, second });
            var otherInstance = new ProgressionHistoryStore(directory);
            Assert.That(otherInstance.Read().Raids.Single().Team, Is.Null);
            otherInstance.AssignTeam(Raid, "  Thursday team  ");
            store.Upsert(Raid, new[] { summary, second, ProgressionPullSummary.Placeholder(summary.Metadata, ProgressionDataState.Unavailable) });

            var restored = new ProgressionHistoryStore(directory).Read().Raids.Single();
            Assert.Multiple(() =>
            {
                Assert.That(restored.Team, Is.EqualTo("Thursday team"));
                Assert.That(restored.Pulls, Has.Count.EqualTo(2));
                Assert.That(restored.Pulls.Select(pull => pull.Metadata.Cohort).Distinct().Count(), Is.EqualTo(2));
                Assert.That(restored.Pulls[0].FirstDeath!.LastHit!.Amount, Is.EqualTo(90));
                Assert.That(restored.Pulls[0].FirstDeath!.Leadup.Single().Ability, Is.EqualTo("Shock"));
            });
            store.AssignTeam(Raid, " ");
            Assert.That(new ProgressionHistoryStore(directory).Read().Raids.Single().Team, Is.Null);
        });
    }

    [Test]
    public void CorruptRaidHistoryIsReportedAndPreservedWhileOtherRaidsRemainUsable()
    {
        WithStore(directory =>
        {
            const string damaged = "{broken";
            var damagedFile = Path.Combine(directory, RaidId + ".json");
            File.WriteAllText(damagedFile, damaged);
            var store = new ProgressionHistoryStore(directory);
            var otherRaid = Raid with { Id = new string('B', 64) };
            store.AssignTeam(otherRaid, "Second team");
            Assert.Throws<System.Text.Json.JsonException>(() => store.AssignTeam(Raid, "First team"));
            Assert.That(store.Read().HasReadErrors, Is.True);
            Assert.That(store.Read().Raids.Single().Team, Is.EqualTo("Second team"));
            Assert.That(File.ReadAllText(damagedFile), Is.EqualTo(damaged));
        });
    }

    private static void WithStore(Action<string> exercise)
    {
        var path = Path.Combine(Path.GetTempPath(), "orbs-progression-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try { exercise(path); }
        finally { Directory.Delete(path, recursive: true); }
    }

    private static ProgressionPullMetadata Metadata() => new("pull", RaidId, Cohort, Boss.Name, Start, Start.AddSeconds(20), false);
    private static ProgressionPullSummary Build(ParsedLogEntry[] logs, params Phase[] phases) =>
        ProgressionSummaryBuilder.Build(Metadata(), logs, phases, "test", CancellationToken.None);

    private static Phase Phase(string name, PhaseTrigger trigger, double seconds = 0) => new()
    {
        Id = Guid.NewGuid(), Name = name, PhaseSource = "Operation|Boss", SourcePt2 = "Boss", StartTrigger = trigger,
        StartArgs = new PhaseArgs { CombatDuration = seconds }, EndArgs = new PhaseArgs()
    };

    private static ParsedLogEntry Hit(long line, double time, ulong id, string ability, double amount) =>
        Entry(line, time, _7_0LogParsing._damageEffectId, EffectType.Apply, Boss, Player, ability, id, amount);
    private static ParsedLogEntry Death(long line, double time, string ability = "") =>
        Entry(line, time, _7_0LogParsing.DeathCombatId, EffectType.Event, Boss, Player, ability);
    private static ParsedLogEntry Entry(long line, double time, ulong effect, EffectType type, Entity source, Entity target,
        string ability = "", ulong abilityId = 0, double amount = 0) => new()
    {
        LogLineNumber = line, TimeStamp = Start.AddSeconds(time), SourceInfo = new EntityInfo { Entity = source },
        TargetInfo = new EntityInfo { Entity = target }, Ability = ability, AbilityId = abilityId,
        Effect = new Effect { EffectId = effect, EffectType = type }, Value = new Value { EffectiveDblValue = amount }
    };
}
