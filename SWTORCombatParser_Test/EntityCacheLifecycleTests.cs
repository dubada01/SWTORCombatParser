using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;

namespace SWTORCombatParser_Test;

[NonParallelizable]
public class EntityCacheLifecycleTests
{
    private static readonly DateTime ParseDate = new(2026, 11, 7);
    private static readonly FieldInfo EntityPool = typeof(_7_0LogParsing)
        .GetField("_currentEntities", BindingFlags.Static | BindingFlags.NonPublic);
    private LogState _previousState;
    private object _previousEntities;

    [SetUp]
    public void Setup()
    {
        _previousState = CombatLogStateBuilder.CurrentState;
        _previousEntities = EntityPool.GetValue(null);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        _7_0LogParsing.SetupRegex();
        CombatLogParser.SetParseDate(ParseDate);
    }

    [TearDown]
    public void Cleanup()
    {
        CombatLogStateBuilder.CurrentState = _previousState;
        EntityPool.SetValue(null, _previousEntities);
    }

    [Test]
    public void LoadingAnotherLogDoesNotReuseNamesOrFlagsFromThePreviousLog()
    {
        var first = Parse(Entry("@First#777")).Single().Source;
        first.IsBoss = true;
        first.IsLocalPlayer = true;

        var second = Parse(Entry("@Second#777")).Single().Source;

        Assert.Multiple(() =>
        {
            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(second.Name, Is.EqualTo("Second"));
            Assert.That(second.IsBoss, Is.False);
            Assert.That(second.IsLocalPlayer, Is.False);
            Assert.That(first.Name, Is.EqualTo("First"));
            Assert.That(first.IsBoss && first.IsLocalPlayer, Is.True,
                "Resetting the parser pool must not mutate retained combat entities.");
        });
    }

    [Test]
    public void EntityPoolRetainsOnlyEntitiesFromTheCurrentLog()
    {
        Parse(Enumerable.Range(1, 64).Select(id => Entry($"NPC {{42}}:{id}")).ToArray());
        Assert.That(CurrentPool.Count, Is.EqualTo(64));

        Parse(Entry("NPC {42}:1001"), Entry("NPC {42}:1002"));

        Assert.That(CurrentPool.Keys, Is.EquivalentTo(new long[] { 1001, 1002 }));
    }

    [Test]
    public void HistoricalToLiveHandoffPreservesEntitiesAndClosesBuffAcrossMidnight()
    {
        var historical = Parse(Entry("@Player#777", time: "23:59:59.000")).Single();
        var state = CombatLogStateBuilder.CurrentState;
        var modifier = state.Modifiers[1234].Values.Single();

        // The streamer adjusts the date after the historical pass before tailing.
        _7_0LogParsing.SetStartDate(historical.TimeStamp);
        var live = CombatLogParser.ParseLine(
            Entry("@Player#777", time: "00:00:01.000", remove: true), 1, historical.TimeStamp);

        Assert.Multiple(() =>
        {
            Assert.That(live.Error, Is.EqualTo(ErrorType.None));
            Assert.That(live.Source, Is.SameAs(historical.Source));
            Assert.That(CombatLogStateBuilder.CurrentState, Is.SameAs(state));
            Assert.That(live.TimeStamp, Is.EqualTo(ParseDate.AddDays(1).AddSeconds(1)));
            Assert.That(state.Modifiers[1234].Values, Has.Count.EqualTo(1));
            Assert.That(modifier.Complete, Is.True);
            Assert.That(modifier.StopTime, Is.EqualTo(live.TimeStamp));
        });
    }

    [TestCase("@Player#777", "Player", 777L, 777L, true, false)]
    [TestCase("@Owner#777/Companion {888}:999", "Companion", 888L, 888L, true, true)]
    [TestCase("Unknown {333}", "Unknown", 333L, 333L, false, false)]
    [TestCase("::444", "444", 444L, 444L, false, false)]
    [TestCase("NPC {42}:555", "NPC", 555L, 42L, false, false)]
    public void ParallelCreationAndWarmLookupKeepOneCanonicalEntityForEveryFormat(
        string source, string name, long id, long logId, bool character, bool companion)
    {
        // ParseAllLines uses parallel workers, including simultaneous first lookups.
        var logs = Parse(Enumerable.Repeat(Entry(source), 512).ToArray());
        var canonical = logs[0].Source;
        canonical.IsBoss = true;
        canonical.IsLocalPlayer = true;
        var changedName = source.Replace("Player", "Renamed").Replace("Companion", "Renamed")
            .Replace("NPC", "Renamed");
        var warm = CombatLogParser.ParseLine(Entry(changedName, hp: 50), 512,
            DateTime.MinValue, realTime: false);

        Assert.Multiple(() =>
        {
            Assert.That(logs.All(log => ReferenceEquals(log.Source, canonical)), Is.True);
            Assert.That(logs.All(log => ReferenceEquals(log.Source, log.Target)), Is.True);
            Assert.That(warm.Error, Is.EqualTo(ErrorType.None));
            Assert.That(warm.Source, Is.SameAs(canonical));
            Assert.That(canonical.Name, Is.EqualTo(name), "The existing first-value-wins behavior is preserved.");
            Assert.That(canonical.Id, Is.EqualTo(id));
            Assert.That(canonical.LogId, Is.EqualTo(logId));
            Assert.That(canonical.IsCharacter, Is.EqualTo(character));
            Assert.That(canonical.IsCompanion, Is.EqualTo(companion));
            Assert.That(canonical.IsBoss && canonical.IsLocalPlayer, Is.True);
            Assert.That(warm.SourceInfo.CurrentHP, Is.EqualTo(50), "Per-event entity info still updates.");
            Assert.That(logs[0].SourceInfo.CurrentHP, Is.EqualTo(100));
            Assert.That(CurrentPool.Count, Is.EqualTo(1));
        });
    }

    [Test]
    public void NpcInstancesWithTheSameTypeRemainSeparate()
    {
        var logs = Parse(Entry("NPC {42}:1001"), Entry("NPC {42}:1002"));
        var warm = CombatLogParser.ParseLine(Entry("Renamed {43}:1001"), 2,
            DateTime.MinValue, realTime: false);

        Assert.Multiple(() =>
        {
            Assert.That(logs[0].Source, Is.Not.SameAs(logs[1].Source));
            Assert.That(logs[0].Source.LogId, Is.EqualTo(logs[1].Source.LogId));
            Assert.That(warm.Source, Is.SameAs(logs[0].Source));
            Assert.That(warm.Source.LogId, Is.EqualTo(42));
            Assert.That(warm.Source.Name, Is.EqualTo("NPC"));
        });
    }

    [Test]
    public void EmptyEntitiesRemainTheSharedSentinelAndAreNotInterned()
    {
        var logs = Parse(Entry(""));

        Assert.Multiple(() =>
        {
            Assert.That(logs.Single().Source, Is.SameAs(Entity.EmptyEntity));
            Assert.That(logs.Single().Target, Is.SameAs(Entity.EmptyEntity));
            Assert.That(CurrentPool, Is.Empty);
        });
    }

    private static ConcurrentDictionary<long, Entity> CurrentPool =>
        (ConcurrentDictionary<long, Entity>)EntityPool.GetValue(null);

    private static List<ParsedLogEntry> Parse(params string[] entries)
    {
        using var stream = new MemoryStream(Encoding.GetEncoding(1252)
            .GetBytes(string.Join('\n', entries) + "\n"));
        using var reader = new StreamReader(stream, Encoding.GetEncoding(1252));
        var logs = CombatLogParser.ParseAllLines(new CombatLogFile { Data = reader, Name = "entities.txt" }, true);
        Assert.That(logs, Has.Count.EqualTo(entries.Length));
        Assert.That(logs.All(log => log.Error == ErrorType.None), Is.True);
        return logs;
    }

    private static string Entry(string source, string time = "12:00:00.000", bool remove = false, int hp = 100)
    {
        var entity = string.IsNullOrEmpty(source) ? "" : $"{source}|(0,0,0,0)|({hp}/100)";
        return $"[{time}] [{entity}] [=] [Buff {{1234}}] " +
               $"[{(remove ? "RemoveEffect" : "ApplyEffect")} {{{(remove ? 836045448945478 : 836045448945477)}}}: Buff {{1234}}]";
    }
}
