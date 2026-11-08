using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.DataStructures.SessionReview;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.SessionReview;
using SWTORCombatParser.ViewModels.SessionReview;

namespace SWTORCombatParser_Test;

public class SessionReviewTests
{
    private static readonly DateTime Start = new(2026, 1, 2, 23, 59, 50);
    private static Entity Player(string name, bool local = false) => new() { Name = name, IsCharacter = true, IsLocalPlayer = local };
    private static ParsedLogEntry Log(Entity source, Entity target, ulong effect, double effective, double taken = 0,
        ulong ability = 1, EffectType type = EffectType.Apply, int seconds = 1) => new()
    {
        SourceInfo = new EntityInfo { Entity = source }, TargetInfo = new EntityInfo { Entity = target },
        TimeStamp = Start.AddSeconds(seconds), AbilityId = ability,
        Effect = new Effect { EffectId = effect, EffectType = type },
        Value = new Value { DblValue = effective * 2, EffectiveDblValue = effective, MitigatedDblValue = taken }
    };

    [Test]
    public void MetricsUseExistingDamageHealingAndMitigationRulesWithoutParserState()
    {
        var local = Player("Local", true);
        var other = Player("Other");
        var enemy = new Entity { Name = "Boss" };
        var logs = new[]
        {
            Log(local, enemy, _7_0LogParsing._damageEffectId, 1000),
            Log(other, enemy, _7_0LogParsing._damageEffectId, 2000),
            Log(local, local, _7_0LogParsing._damageEffectId, 20, 10), // self-damage is only incoming
            Log(enemy, local, _7_0LogParsing._damageEffectId, 500, 300),
            Log(local, other, _7_0LogParsing._healEffectId, 400),
            Log(local, local, _7_0LogParsing._healEffectId, 900, ability: 813707324030976),
            Log(local, local, _7_0LogParsing._healEffectId, 900, ability: 3289210509328384),
            Log(local, enemy, _7_0LogParsing._damageEffectId, 900, type: EffectType.Remove),
            Log(enemy, local, _7_0LogParsing.DeathCombatId, 0, type: EffectType.Event),
            Log(enemy, local, _7_0LogParsing.DeathCombatId, 0, type: EffectType.Event),
        };
        var summary = SessionReviewBuilder.Summarize("Boss", Start, Start.AddSeconds(20), SessionCombatOutcome.Wipe, logs);
        Assert.Multiple(() =>
        {
            Assert.That(summary.ForPlayer(null).Damage, Is.EqualTo(1000));
            Assert.That(summary.ForPlayer(null).EffectiveHealing, Is.EqualTo(400));
            Assert.That(summary.ForPlayer(null).DamageTaken, Is.EqualTo(310));
            Assert.That(summary.ForPlayer(null).Deaths, Is.EqualTo(1));
            Assert.That(summary.ForPlayer("other").Damage, Is.EqualTo(2000));
            Assert.That(summary.Players.ContainsKey("Boss"), Is.False);
            Assert.That(summary.DurationSeconds, Is.EqualTo(20));
            Assert.That(summary.Outcome, Is.EqualTo(SessionCombatOutcome.Wipe));
        });
    }

    [Test]
    public void WeightedRatesIncludeWipesButDistinguishCombatTimeFromElapsedTime()
    {
        var local = Player("Local", true);
        var enemy = new Entity { Name = "Boss" };
        var wipe = SessionReviewBuilder.Summarize("Boss", Start, Start.AddSeconds(10), SessionCombatOutcome.Wipe,
            new[] { Log(local, enemy, _7_0LogParsing._damageEffectId, 1000) });
        var kill = SessionReviewBuilder.Summarize("Boss", Start.AddSeconds(40), Start.AddSeconds(70), SessionCombatOutcome.Kill,
            new[] { Log(local, enemy, _7_0LogParsing._damageEffectId, 9000, seconds: 50) });
        var session = new SessionReviewSnapshot("Operation", "Master 8", new[] { wipe, kill });
        Assert.Multiple(() =>
        {
            Assert.That(session.ElapsedSeconds, Is.EqualTo(70));
            Assert.That(session.CombatSeconds, Is.EqualTo(40));
            Assert.That(session.DowntimeSeconds, Is.EqualTo(30));
            Assert.That(session.CombatDps(null), Is.EqualTo(250));
            Assert.That(session.SessionDps(null), Is.EqualTo(10000d / 70).Within(.001));
            Assert.That(session.Kills, Is.EqualTo(1));
            Assert.That(session.Wipes, Is.EqualTo(1));
        });
    }

    [Test]
    public void OverlappingRangesAndZeroDurationDoNotProduceNegativeOrInvalidRates()
    {
        SessionCombatSummary Combat(int start, int end) => new("PvP", Start.AddSeconds(start), Start.AddSeconds(end),
            SessionCombatOutcome.PvP, new Dictionary<string, SessionPlayerMetrics>(), Array.Empty<string>());
        var session = new SessionReviewSnapshot("Arena", "PvP", new[] { Combat(20, 40), Combat(0, 30), Combat(50, 50) });
        Assert.That(session.CombatSeconds, Is.EqualTo(40));
        Assert.That(session.DowntimeSeconds, Is.EqualTo(10));
        var empty = new SessionReviewSnapshot("Empty", "", Array.Empty<SessionCombatSummary>());
        Assert.That(empty.CombatDps(null), Is.Zero);
        Assert.That(empty.SessionDps(null), Is.Zero);
    }

    [Test]
    public void SameNamedPlayersAcrossSeparateEntityInstancesAreCombined()
    {
        var logs = new[]
        {
            Log(Player("Local", true), new Entity { Name = "A" }, _7_0LogParsing._damageEffectId, 100),
            Log(Player("LOCAL", true), new Entity { Name = "B" }, _7_0LogParsing._damageEffectId, 200)
        };
        var summary = SessionReviewBuilder.Summarize("Combat", Start, Start.AddSeconds(10), SessionCombatOutcome.Combat, logs);
        Assert.That(summary.ForPlayer(null).Damage, Is.EqualTo(300));
        Assert.That(summary.Players.Count, Is.EqualTo(1));
    }

    [Test]
    public void CancellationInterruptsScanningAndMissingDataIsExplicit()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => SessionReviewBuilder.Summarize("Combat", Start, Start,
            SessionCombatOutcome.Combat, new[] { Log(Player("Local"), Player("Other"), _7_0LogParsing._damageEffectId, 1) }, cancellation.Token));
        var summary = SessionReviewBuilder.Summarize("Unavailable", Start, Start, SessionCombatOutcome.Combat, Array.Empty<ParsedLogEntry>());
        Assert.That(summary.HasData, Is.False);
        Assert.That(new SessionReviewSnapshot("Session", "", new[] { summary }).MissingCombats, Is.EqualTo(1));
    }

    [Test]
    public void GraphBucketsConserveTotalsAndUseActualPartialBucketDuration()
    {
        var player = Player("Local", true);
        var enemy = new Entity { Name = "Boss" };
        var combat = SessionReviewBuilder.Summarize("Boss", Start, Start.AddSeconds(12.5), SessionCombatOutcome.Kill,
            new[] { Log(player, enemy, _7_0LogParsing._damageEffectId, 100), Log(player, enemy, _7_0LogParsing._damageEffectId, 200, seconds: 6), Log(player, enemy, _7_0LogParsing._damageEffectId, 30, seconds: 12) });
        var graph = SessionGraphBuilder.Build(new SessionReviewSnapshot("Operation", "", new[] { combat }), new[] { "Local" }, SessionGraphMetric.Dps, 5);
        var points = graph.Series.Single().Points.Where(p => p.Value.HasValue).ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(points.Select(p => p.Value), Is.EqualTo(new[] { 20d, 40d, 12d }));
            Assert.That(points.Sum(p => p.Value!.Value * (p.EndSeconds - p.StartSeconds)), Is.EqualTo(330));
            Assert.That(points.Last().EndSeconds, Is.EqualTo(12.5));
        });
    }

    [Test]
    public void BossFilterIncludesLiveAttemptsAndPreservesOriginalTimeAxis()
    {
        SessionCombatSummary Combat(int from, int to, SessionCombatOutcome outcome, bool boss) => new("Fight", Start.AddSeconds(from), Start.AddSeconds(to), outcome,
            new Dictionary<string, SessionPlayerMetrics>(), Array.Empty<string>()) { IsBoss = boss };
        var snapshot = new SessionReviewSnapshot("Session", "", new[] { Combat(0, 10, SessionCombatOutcome.Combat, false), Combat(20, 30, SessionCombatOutcome.Wipe, true), Combat(40, 50, SessionCombatOutcome.InProgress, true), Combat(60, 70, SessionCombatOutcome.PvP, false) });
        var filtered = snapshot.FilterBosses(true);
        Assert.Multiple(() =>
        {
            Assert.That(filtered.Combats.Count, Is.EqualTo(2));
            Assert.That(filtered.Combats.Last().Outcome, Is.EqualTo(SessionCombatOutcome.InProgress));
            Assert.That(filtered.Start, Is.EqualTo(Start));
            Assert.That(filtered.End, Is.EqualTo(Start.AddSeconds(70)));
            Assert.That(filtered.CombatSeconds, Is.EqualTo(20));
        });
    }

    [Test]
    public void ObservedHealthKeepsZerosButNeverFillsUnknownSecondsOrOtherCombats()
    {
        var player = Player("Local", true);
        var first = Log(player, new Entity(), 0, 0, seconds: 1);
        first.SourceInfo.MaxHP = 1000; first.SourceInfo.CurrentHP = 500;
        var later = Log(player, new Entity(), 0, 0, seconds: 1);
        later.TimeStamp = later.TimeStamp.AddMilliseconds(800);
        later.SourceInfo.MaxHP = 1000; later.SourceInfo.CurrentHP = 300;
        var dead = Log(new Entity(), player, _7_0LogParsing.DeathCombatId, 0, seconds: 3);
        dead.TargetInfo.MaxHP = 1000; dead.TargetInfo.CurrentHP = 0;
        var combat = SessionReviewBuilder.Summarize("Boss", Start, Start.AddSeconds(5), SessionCombatOutcome.Wipe, new[] { later, first, dead });
        var next = SessionReviewBuilder.Summarize("Boss", Start.AddSeconds(10), Start.AddSeconds(15), SessionCombatOutcome.Kill,
            new[] { Log(player, new Entity(), _7_0LogParsing._damageEffectId, 100, seconds: 11) });
        var graph = SessionGraphBuilder.Build(new SessionReviewSnapshot("Session", "", new[] { combat, next }), new[] { "Local" }, SessionGraphMetric.HealthPercent, 30);
        var observed = graph.Series.Single().Points.Where(p => p.Value.HasValue).ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(graph.BucketSeconds, Is.EqualTo(1));
            Assert.That(observed.Select(p => p.Value), Is.EqualTo(new[] { 30d, 0d }));
            Assert.That(observed.Select(p => p.StartSeconds), Is.EqualTo(new[] { 1d, 3d }));
            Assert.That(graph.Series.Single().Points.Single(p => p.StartSeconds == 2).Value, Is.Null);
            Assert.That(graph.Series.Single().Points.Where(p => p.StartSeconds >= 10).All(p => p.Value is null), Is.True);
        });
    }

    [Test]
    public void GraphSeparatesCombatsAndDoesNotInventMetricsForAbsentPlayers()
    {
        var first = SessionReviewBuilder.Summarize("Boss", Start, Start.AddSeconds(5), SessionCombatOutcome.Wipe,
            new[] { Log(Player("Local", true), new Entity(), _7_0LogParsing._damageEffectId, 100) });
        var second = SessionReviewBuilder.Summarize("Boss", Start.AddSeconds(10), Start.AddSeconds(15), SessionCombatOutcome.Kill,
            new[] { Log(Player("Other"), new Entity(), _7_0LogParsing._damageEffectId, 300, seconds: 11) });
        var graph = SessionGraphBuilder.Build(new SessionReviewSnapshot("Session", "", new[] { first, second }), new[] { "Local", "Other" }, SessionGraphMetric.Dps);
        Assert.That(graph.Series[0].Points.Where(p => p.Value.HasValue).Select(p => p.StartSeconds), Is.EqualTo(new[] { 0d }));
        Assert.That(graph.Series[1].Points.First().Value, Is.Null);
        Assert.That(graph.Series[1].Points.Where(p => p.Value.HasValue).Select(p => p.StartSeconds), Is.EqualTo(new[] { 10d }));
    }

    [Test]
    public void SummaryRatesExcludeFightsWherePlayerWasNotRecordedAndUnavailableFights()
    {
        var own = SessionReviewBuilder.Summarize("Boss", Start, Start.AddSeconds(10), SessionCombatOutcome.Kill,
            new[] { Log(Player("Local", true), new Entity(), _7_0LogParsing._damageEffectId, 1000) });
        var others = SessionReviewBuilder.Summarize("Boss", Start.AddSeconds(20), Start.AddSeconds(50), SessionCombatOutcome.Kill,
            new[] { Log(Player("Other"), new Entity(), _7_0LogParsing._damageEffectId, 3000, seconds: 21) });
        var missing = new SessionCombatSummary("Missing", Start.AddSeconds(60), Start.AddSeconds(90), SessionCombatOutcome.Wipe,
            new Dictionary<string, SessionPlayerMetrics>(), Array.Empty<string>(), false);
        var snapshot = new SessionReviewSnapshot("Session", "", new[] { own, others, missing });
        var viewModel = new SessionPerformanceViewModel();
        viewModel.Update(snapshot, "Local");
        Assert.Multiple(() =>
        {
            Assert.That(snapshot.PlayerCombatSeconds("Local"), Is.EqualTo(10));
            Assert.That(snapshot.CombatDps("Local"), Is.EqualTo(100));
            Assert.That(viewModel.CombatDps, Is.EqualTo(100d.ToString("N0")));
            Assert.That(viewModel.PlayerCombatTime, Is.EqualTo("00:10"));
        });
    }

    [Test]
    public void RoleMetadataUsesCapturedClassAndRawLogsNeverCountSyntheticAbsorbsAsDirectHealing()
    {
        var player = Player("Healer", true);
        var direct = Log(player, Player("Tank"), _7_0LogParsing._healEffectId, 200);
        var absorb = Log(player, Player("Tank"), _7_0LogParsing._healEffectId, 800, type: EffectType.AbsorbShield);
        var info = new Dictionary<string, SessionPlayerInfo> { ["Healer"] = new(Role.Healer, "Corruption"), ["Tank"] = new(Role.Tank, "Immortal") };
        var combat = SessionReviewBuilder.Summarize("Boss", Start, Start.AddSeconds(10), SessionCombatOutcome.Kill, new[] { direct, absorb }, playerInfo: info);
        var graph = SessionGraphBuilder.Build(new SessionReviewSnapshot("Session", "", new[] { combat }), new[] { "Healer" }, SessionGraphMetric.DirectEhps, 5);
        Assert.Multiple(() =>
        {
            Assert.That(combat.PlayerInfo["Healer"].Role, Is.EqualTo(Role.Healer));
            Assert.That(combat.PlayerInfo["Tank"].Role, Is.EqualTo(Role.Tank));
            Assert.That(combat.ForPlayer("Healer").EffectiveHealing, Is.EqualTo(200));
            Assert.That(graph.Series.Single().Points.Where(p => p.Value.HasValue).Select(p => p.Value), Is.EqualTo(new[] { 40d, 0d }));
        });
    }
}
