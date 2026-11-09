using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;

namespace SWTORCombatParser_Test;

[NonParallelizable]
public class HistoricalLogParsingTests
{
    private static readonly DateTime ParseDate = new(2024, 1, 1);
    private LogState _previousState;

    [SetUp]
    public void SetUp()
    {
        _previousState = CombatLogStateBuilder.CurrentState;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        _7_0LogParsing.SetupRegex();
        CombatLogParser.SetParseDate(ParseDate);
    }

    [TearDown]
    public void TearDown() => CombatLogStateBuilder.CurrentState = _previousState;

    [TestCase("windows-1252", "\r\n")]
    [TestCase("windows-1252", "\n")]
    [TestCase("utf-8", "\r\n")]
    [TestCase("utf-8", "\n")]
    [TestCase("utf-16", "\r\n")]
    [TestCase("utf-16", "\n")]
    public void ReadsLongNonAsciiRecordsAndPreservesPhysicalByteCursor(string encodingName, string newline)
    {
        var encoding = Encoding.GetEncoding(encodingName);
        var name = encodingName == "windows-1252" ? "Éclair" : "Éclair 漢字 🚀";
        var first = Entry("12:00:00.000", "ApplyEffect", new string('x', 9000) + name);
        var second = Entry("12:00:01.000", "RemoveEffect", name);
        var completed = first + newline + newline + second + newline;
        var logs = Parse(completed + "unfinished " + name, encoding, false, out var bytes, out var lines);

        Assert.Multiple(() =>
        {
            Assert.That(logs.Select(log => log.LogLineNumber), Is.EqualTo(new long[] { 0, 2 }));
            Assert.That(logs.Select(log => log.Ability), Is.EqualTo(new[] { new string('x', 9000) + name, name }));
            Assert.That(logs.All(log => log.Error == ErrorType.None), Is.True);
            Assert.That(bytes, Is.EqualTo(encoding.GetPreamble().Length + (long)encoding.GetByteCount(completed)));
            Assert.That(lines, Is.EqualTo(3));
        });
    }

    [Test]
    public void ReadsCrLfSplitAcrossTheCharacterBufferBoundary()
    {
        var entry = Entry("12:00:00.000", "ApplyEffect", "");
        entry = Entry("12:00:00.000", "ApplyEffect", new string('x', 4095 - entry.Length));
        var logs = Parse(entry + "\r\n", Encoding.GetEncoding(1252), false, out var bytes, out var lines);

        Assert.Multiple(() =>
        {
            Assert.That(logs.Count, Is.EqualTo(1));
            Assert.That(logs[0].Error, Is.EqualTo(ErrorType.None));
            Assert.That(bytes, Is.EqualTo(4097));
            Assert.That(lines, Is.EqualTo(1));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void EqualTimestampEffectsKeepFileOrderAndIncompleteRecordsDoNotUpdateState(bool includeIncomplete)
    {
        var contents = string.Join('\n',
            Entry("12:00:00.000", "ApplyEffect"),
            "unfinished record",
            Entry("12:00:00.000", "RemoveEffect"),
            Entry("12:00:00.000", "ApplyEffect")) + "\n";
        var logs = Parse(contents, Encoding.GetEncoding(1252), includeIncomplete, out _, out _);
        var modifiers = CombatLogStateBuilder.CurrentState.Modifiers[4256312590336000].Values;

        Assert.Multiple(() =>
        {
            Assert.That(logs.Where(log => log.Error == ErrorType.None).Select(log => log.LogLineNumber),
                Is.EqualTo(new long[] { 0, 2, 3 }));
            Assert.That(logs.Count, Is.EqualTo(includeIncomplete ? 4 : 3));
            if (includeIncomplete)
            {
                Assert.That(logs[0].Error, Is.EqualTo(ErrorType.IncompleteLine));
                Assert.That(logs[0].TimeStamp, Is.EqualTo(DateTime.MinValue));
            }
            Assert.That(modifiers.Count, Is.EqualTo(2));
            Assert.That(modifiers.Count(modifier => modifier.Complete), Is.EqualTo(1));
            Assert.That(modifiers.Single(modifier => modifier.Complete).StopTime,
                Is.EqualTo(ParseDate.AddHours(12)));
        });
    }

    [Test]
    public void RectifiesMidnightBeforeSortingSmallOutOfOrderRecords()
    {
        var contents = string.Join('\n',
            Entry("23:59:59.000", "ApplyEffect"),
            Entry("00:00:02.000", "ApplyEffect"),
            "unfinished record",
            Entry("00:00:01.000", "ApplyEffect"),
            Entry("00:00:02.000", "RemoveEffect")) + "\n";
        var logs = Parse(contents, Encoding.GetEncoding(1252), false, out _, out _);

        Assert.Multiple(() =>
        {
            Assert.That(logs.Select(log => log.LogLineNumber), Is.EqualTo(new long[] { 0, 3, 1, 4 }));
            Assert.That(logs.Select(log => log.TimeStamp), Is.EqualTo(new[]
            {
                ParseDate.AddDays(1).AddSeconds(-1),
                ParseDate.AddDays(1).AddSeconds(1),
                ParseDate.AddDays(1).AddSeconds(2),
                ParseDate.AddDays(1).AddSeconds(2)
            }));
        });
    }

    [Test]
    public void HistoricalPlayerNotificationsPreserveReferenceChangesAndCompletionFlags()
    {
        const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
        const BindingFlags shared = BindingFlags.Static | BindingFlags.NonPublic;
        // Exercise the actual historical path without app startup settings or UI.
        var streamer = (CombatLogStreamer)RuntimeHelpers.GetUninitializedObject(typeof(CombatLogStreamer));
        typeof(CombatLogStreamer).GetField("_currentCombatLogs", instance)!.SetValue(streamer, new List<ParsedLogEntry>());
        typeof(CombatLogStreamer).GetField("_combatDetector", instance)!.SetValue(streamer, new CombatDetector());
        var inCombat = typeof(CombatLogStreamer).GetField("_isInCombat", shared)!;
        var finished = typeof(CombatLogStreamer).GetField("HistoricalLogsFinished", shared)!;
        var originalInCombat = inCombat.GetValue(null);
        var originalFinished = finished.GetValue(null);
        var notifications = new List<Entity>();
        var completed = new List<bool>();
        var first = new Entity { Id = 1, LogId = 1, IsLocalPlayer = true };
        var changed = new Entity { Id = 2, LogId = 1, IsLocalPlayer = true };
        var other = new Entity { Id = 3, LogId = 3 };
        ParsedLogEntry Log(Entity source) => new()
        {
            TimeStamp = ParseDate.AddHours(12), Effect = new Effect(),
            SourceInfo = new EntityInfo { Entity = source }, TargetInfo = new EntityInfo { Entity = source }
        };
        var parse = typeof(CombatLogStreamer).GetMethod("ParseHistoricalLog", instance)!;
        streamer.LocalPlayerIdentified += notifications.Add;
        try
        {
            inCombat.SetValue(null, false);
            finished.SetValue(null, (Action<DateTime, bool>)((_, identified) => completed.Add(identified)));
            var incomplete = Log(new Entity { IsLocalPlayer = true });
            incomplete.Error = ErrorType.IncompleteLine;
            parse.Invoke(streamer, new object[] { new List<ParsedLogEntry>
            {
                incomplete, Log(first), Log(first), Log(other), Log(first), Log(changed), Log(changed), Log(first)
            } });
            // Each new historical load still identifies its first local player.
            parse.Invoke(streamer, new object[] { new List<ParsedLogEntry> { Log(first) } });
            parse.Invoke(streamer, new object[] { new List<ParsedLogEntry> { Log(other) } });
            Assert.Multiple(() =>
            {
                Assert.That(notifications.Count, Is.EqualTo(4));
                Assert.That(notifications[0], Is.SameAs(first));
                Assert.That(notifications[1], Is.SameAs(changed));
                Assert.That(notifications[2], Is.SameAs(first));
                Assert.That(notifications[3], Is.SameAs(first));
                Assert.That(completed, Is.EqualTo(new[] { true, true, false }));
            });
        }
        finally
        {
            finished.SetValue(null, originalFinished);
            inCombat.SetValue(null, originalInCombat);
        }
    }

    private static List<ParsedLogEntry> Parse(string contents, Encoding encoding, bool includeIncomplete,
        out long completedBytes, out int physicalLines)
    {
        using var stream = new MemoryStream();
        stream.Write(encoding.GetPreamble());
        stream.Write(encoding.GetBytes(contents));
        stream.Position = 0;
        // Start from the normal log encoding so UTF BOM detection is also exercised.
        using var reader = new StreamReader(stream, Encoding.GetEncoding(1252), true);
        var file = new CombatLogFile { Data = reader, Name = "historical-test.txt" };
        return CombatLogParser.ParseAllLines(file, includeIncomplete, out completedBytes, out physicalLines);
    }

    private static string Entry(string time, string effect, string ability = "Advanced Kyrprax Proficient Stim") =>
        $"[{time}] [@Player#689235581908948|(1605.93,-934.38,364.08,-23.85)|(8032/8032)] [=] [{ability} {{4256312590336000}}] [{effect} {{{(effect == "RemoveEffect" ? 836045448945478 : 836045448945477)}}}: Advanced Kyrprax Proficient Stim {{4256312590336000}}]";
}
