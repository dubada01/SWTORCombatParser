using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogIndex;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.RunRace;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.LogBrowser;

namespace SWTORCombatParser_Test;

[NonParallelizable]
public class CombatLogReplayTests
{
    private string _directory;
    private LogState _previousState;
    private static readonly DateTime Anchor = new(2026, 11, 7, 12, 0, 0);
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [SetUp]
    public void Setup()
    {
        _directory = Path.Combine(Path.GetTempPath(), "orbs-replay-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _previousState = CombatLogStateBuilder.CurrentState;
        CombatLogStateBuilder.CurrentState = new LogState();
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        _7_0LogParsing.SetupRegex();
    }

    [TearDown]
    public void Cleanup()
    {
        CombatLogStateBuilder.CurrentState = _previousState;
        Directory.Delete(_directory, true);
    }

    [Test]
    public async Task WholeLogPreservesRealSpacingTextAndSourceBytes()
    {
        var source = Source("[18:00:00.000] Première", "[18:00:00.160] Second");
        var original = File.ReadAllBytes(source);
        using var replay = await CombatLogReplay.PrepareAsync(new LogReplayRequest(source));
        await replay.WritePreludeAsync(Anchor);
        Assert.That(new FileInfo(replay.OutputPath).Length, Is.EqualTo(3), "Even an empty prefix needs the UTF-8 BOM for the live reader.");
        var time = Stopwatch.StartNew();
        await replay.PlayAsync(Anchor);
        Assert.Multiple(() =>
        {
            Assert.That(time.ElapsedMilliseconds, Is.GreaterThanOrEqualTo(130));
            Assert.That(File.ReadAllLines(replay.OutputPath), Is.EqualTo(new[] { "[12:00:00.000] Première", "[12:00:00.160] Second" }));
            Assert.That(File.ReadAllBytes(source), Is.EqualTo(original));
            Assert.That(replay.OutputPath, Is.Not.EqualTo(source));
            Assert.That(LogReplayContext.IsReplayFile(replay.OutputPath), Is.True);
        });
    }

    [Test]
    public async Task SelectedCombatPrimesEarlierContextWithoutPlayingOtherCombats()
    {
        var source = Source("[18:00:00.000] area", "[18:00:01.000] preceding combat", "[18:00:02.000] selected enter",
            "[18:00:02.060] selected exit", "[18:00:03.000] later combat");
        var from = new DateTime(2026, 11, 6, 18, 0, 2);
        using var replay = await CombatLogReplay.PrepareAsync(new LogReplayRequest(source, from, from.AddMilliseconds(60)));
        await replay.WritePreludeAsync(Anchor);
        Assert.That(File.ReadAllLines(replay.OutputPath), Is.EqualTo(new[] { "[11:59:58.000] area", "[11:59:59.000] preceding combat" }));
        await replay.PlayAsync(Anchor);
        Assert.That(File.ReadAllLines(replay.OutputPath).Skip(2), Is.EqualTo(new[] { "[12:00:00.000] selected enter", "[12:00:00.060] selected exit" }));
        Assert.That(replay.Duration, Is.EqualTo(TimeSpan.FromMilliseconds(60)));
    }

    [Test]
    public async Task MidnightAndSmallOutOfOrderEventsKeepPhysicalOrdering()
    {
        var path = Path.Combine(_directory, "combat_2026-11-06_23_59_59.txt");
        File.WriteAllLines(path, new[] { "[23:59:59.990] first", "[00:00:00.020] second", "[00:00:00.010] late" });
        using var replay = await CombatLogReplay.PrepareAsync(new LogReplayRequest(path));
        await replay.WritePreludeAsync(Anchor);
        await replay.PlayAsync(Anchor);
        Assert.That(replay.Duration, Is.EqualTo(TimeSpan.FromMilliseconds(30)));
        Assert.That(File.ReadAllLines(replay.OutputPath), Is.EqualTo(new[] { "[12:00:00.000] first", "[12:00:00.030] second", "[12:00:00.020] late" }));
    }

    [Test]
    public async Task CancellationDuringAnIdleGapStopsWritesAndDeletesOnlyGeneratedFiles()
    {
        var source = Source("[18:00:00.000] first", "[18:05:00.000] must not play");
        var replay = await CombatLogReplay.PrepareAsync(new LogReplayRequest(source));
        var output = replay.OutputPath;
        try
        {
            await replay.WritePreludeAsync(Anchor);
            using var cancellation = new CancellationTokenSource(70);
            Assert.CatchAsync<OperationCanceledException>(async () => await replay.PlayAsync(Anchor, token: cancellation.Token));
            Assert.That(File.ReadAllLines(output), Is.EqualTo(new[] { "[12:00:00.000] first" }));
        }
        finally { replay.Dispose(); }
        Assert.That(File.Exists(output), Is.False);
        Assert.That(Directory.Exists(Path.GetDirectoryName(output)), Is.False);
        Assert.That(File.Exists(source), Is.True);
    }

    [Test]
    public void InvalidRangeAndMissingEventsAreRejected()
    {
        var source = Source("not a timestamped log");
        Assert.ThrowsAsync<InvalidDataException>(async () => await CombatLogReplay.PrepareAsync(new LogReplayRequest(source)));
        Assert.ThrowsAsync<ArgumentException>(async () => await CombatLogReplay.PrepareAsync(new LogReplayRequest(source, Anchor, Anchor.AddSeconds(-1))));
    }

    [Test]
    public async Task SourceChangesAfterPreparationDoNotExtendTheReplay()
    {
        var source = Source("[18:00:00.000] snapshot");
        using var replay = await CombatLogReplay.PrepareAsync(new LogReplayRequest(source));
        File.AppendAllText(source, "[18:00:30.000] later append\n");
        await replay.WritePreludeAsync(Anchor);
        await replay.PlayAsync(Anchor);
        Assert.That(File.ReadAllLines(replay.OutputPath), Is.EqualTo(new[] { "[12:00:00.000] snapshot" }));
    }

    [Test]
    public async Task PartialWriteGoesThroughPinnedLiveReaderAndFinalDrainExactlyOnce()
    {
        var source = Source(ValidLine("18:00:00.000"), ValidLine("18:00:00.020"));
        using var replay = await CombatLogReplay.PrepareAsync(new LogReplayRequest(source, SimulatePartialWrite: true));
        await replay.WritePreludeAsync(Anchor);
        CombatLogParser.SetParseDate(Anchor);
        var received = new List<ParsedLogEntry>();
        var streamer = TailReader(replay.OutputPath, new FileInfo(replay.OutputPath).Length);
        var generate = typeof(CombatLogStreamer).GetMethod("GenerateNewFrame", Private);
        var originalLogPath = CombatLogLoader.LoggingPath;
        CombatLogLoader.LoggingPath = Path.Combine(_directory, "nonexistent-live-folder");
        Action<ParsedLogEntry> onLine = received.Add;
        CombatLogStreamer.NewLineStreamed += onLine;
        try
        {
            var playing = replay.PlayAsync(Anchor);
            await Task.Delay(50);
            generate.Invoke(streamer, null);
            Assert.That(received, Is.Empty, "The tail reader must retain an incomplete line.");
            await playing;
            await streamer.StopMonitoring(readFinalFrame: true);
            Assert.That(received, Has.Count.EqualTo(2));
            Assert.That(received.All(entry => LogReplayContext.IsReplayFile(entry.LogName)), Is.True);
            generate.Invoke(streamer, null);
            Assert.That(received, Has.Count.EqualTo(2), "Final flush and later polling must not duplicate events.");
            var combat = CombatIdentifier.GenerateCombatSnapshotFromLogs(received);
            Assert.That(combat.IsReplay, Is.True, "Provenance must survive rebuilding a combat from cached log entries.");
        }
        finally
        {
            CombatLogStreamer.NewLineStreamed -= onLine;
            CombatLogLoader.LoggingPath = originalLogPath;
        }
    }

    [Test]
    public async Task ReplayClockDoesNotSpendCombatTimeOnHistoricalWarmup()
    {
        using (var context = LogReplayContext.Begin(Anchor))
        {
            await Task.Delay(25);
            Assert.That(TimeUtility.CorrectedTime, Is.EqualTo(Anchor));
            context.Start();
            await Task.Delay(25);
            Assert.That(TimeUtility.CorrectedTime, Is.GreaterThan(Anchor));
            Assert.Throws<InvalidOperationException>(() => LogReplayContext.Begin(Anchor));
        }
        Assert.That(LogReplayContext.IsActive, Is.False);
        Assert.That(LogReplayContext.CurrentTime, Is.Null);
    }

    [Test]
    public async Task ReplayedBossCombatUsesTheNormalStartUpdateAndKillEvents()
    {
        const string player = "[@Player#689235581908948|(1605.93,-934.38,364.08,-23.85)|(8032/8032)]";
        const string boss = "[Gharj {2034526008115200}:42|(0,0,0,0)|(100/100)]";
        var source = Source(
            $"[18:00:00.000] {player} [=] [] [Event {{836045448945472}}: EnterCombat {{836045448945489}}]",
            $"[18:00:00.020] {player} {boss} [Attack {{100}}] [ApplyEffect {{836045448945477}}: Damage {{836045448945501}}] (100 energy {{836045448940874}}) <100>",
            $"[18:00:00.040] {player} {boss} [] [Event {{836045448945472}}: Death {{836045448945493}}]");
        CombatLogStateBuilder.CurrentState.EncounterEnteredInfo[Anchor] = new EncounterInfo
        {
            Name = "Eternity Vault", Difficutly = "Story", NumberOfPlayer = "8", EncounterType = EncounterType.Operation,
            BossInfos = new List<BossInfo> { new() { EncounterName = "Gharj", TargetIds = new List<long> { 2034526008115200 }, TargetsRequiredForKill = new List<long> { 2034526008115200 } } }
        };
        typeof(LogState).GetMethod("CacheEncounterEnterList", Private).Invoke(CombatLogStateBuilder.CurrentState, null);
        using var replay = await CombatLogReplay.PrepareAsync(new LogReplayRequest(source));
        await replay.WritePreludeAsync(Anchor);
        CombatLogParser.SetParseDate(Anchor);
        var streamer = TailReader(replay.OutputPath, new FileInfo(replay.OutputPath).Length);
        var updates = new List<CombatStatusUpdate>();
        Action<CombatStatusUpdate> handler = updates.Add;
        CombatLogStreamer.CombatUpdated += handler;
        try
        {
            await replay.PlayAsync(Anchor);
            await streamer.StopMonitoring(readFinalFrame: true);
            Assert.That(updates.Select(update => update.Type), Is.EqualTo(new[] { UpdateType.Start, UpdateType.Update, UpdateType.Stop }));
            Assert.That(updates[1].Logs, Has.Count.EqualTo(2), string.Join("; ", updates[1].Logs.Select(log =>
                $"{log.TimeStamp:o} effect={log.Effect.EffectId} target={log.Target.LogId} area={CombatLogStateBuilder.CurrentState.GetEncounterActiveAtTime(log.TimeStamp).Name}")));
            Assert.That(updates[2].Logs.Single().Effect.EffectId, Is.EqualTo(_7_0LogParsing.DeathCombatId));
            Assert.That(CombatLogStreamer.InCombat, Is.False);
        }
        finally { CombatLogStreamer.CombatUpdated -= handler; }
    }

    [Test]
    public async Task ReplayCannotUploadOrReplaceRealGhosts()
    {
        var response = await ParselyUploader.TryUploadText("test", "orbs_replay_example.txt");
        Assert.That(response.WasSuccess, Is.False);
        Assert.That(response.ErrorMessage, Does.Contain("Debug replays"));
        var area = new EncounterInfo
        {
            Name = "Fixture FP", Difficutly = "Veteran", NumberOfPlayer = "4", EncounterType = EncounterType.Flashpoint,
            BossInfos = new List<BossInfo> { new() { EncounterName = "Optional" }, new() { EncounterName = "Boss" } }
        };
        CombatLogStateBuilder.CurrentState.EncounterEnteredInfo[Anchor] = area;
        var combat = new Combat
        {
            IsReplay = true, ParentEncounter = area, StartTime = Anchor.AddSeconds(1), EndTime = Anchor.AddSeconds(2),
            EncounterBossDifficultyParts = ("Boss", "Veteran", "4"), BossKillOverride = true
        };
        var path = Path.Combine(_directory, "ghosts.json");
        using var race = new RunRaceService(new RunGhostStore(path));
        race.ImportHistoricalCombats(new[] { combat });
        Assert.That(race.Snapshot.IsComplete, Is.True);
        Assert.That(race.Snapshot.IsReplay, Is.True);
        await Task.Delay(40);
        Assert.That(File.Exists(path), Is.False);
        // These entry points must return before reading metadata or sending a request.
        await EncounterTimeTrialRecorder.Shared.RecordAsync(new Combat { IsReplay = true });
        await Stats.RecordCombatState(new Combat { IsReplay = true });
        Leaderboards.TryAddLeaderboardEntry(new Combat { IsReplay = true });
    }

    [Test]
    public void LibraryReplayRangeUsesSelectedCombatAndBuildFlag()
    {
        using var browser = new LogBrowserViewModel(_directory);
        browser.SelectedLog = new LogFileCardViewModel(new IndexedCombatLog
        {
            FilePath = "fixture.txt", FileName = "fixture.txt",
            Encounters = new List<IndexedEncounter> { new() { BossName = "Chosen boss", StartTime = Anchor, EndTime = Anchor.AddSeconds(10) } }
        });
        browser.SelectedReplay = browser.ReplayChoices[1];
        var selection = browser.CreateReplayRequest();
#if DEBUG
        Assert.That(browser.ReplayAvailable, Is.True);
        Assert.That(selection.From, Is.EqualTo(Anchor));
        Assert.That(selection.Through, Is.EqualTo(Anchor.AddSeconds(10)));
#else
        Assert.That(browser.ReplayAvailable, Is.False);
        Assert.That(selection, Is.Null);
#endif
    }

    private string Source(params string[] lines)
    {
        var path = Path.Combine(_directory, "combat_2026-11-06_18_00_00.txt");
        File.WriteAllLines(path, lines, Encoding.GetEncoding(1252));
        return path;
    }
    private static string ValidLine(string time) => $"[{time}] [@Player#689235581908948|(1605.93,-934.38,364.08,-23.85)|(8032/8032)] [=] [Advanced Kyrprax Proficient Stim {{4256312590336000}}] [ApplyEffect {{836045448945477}}: Advanced Kyrprax Proficient Stim {{4256312590336000}}]";

    private static CombatLogStreamer TailReader(string path, long cursor)
    {
        // Isolate the real reader from startup settings/network/UI, as in TestLogCursor.
        var streamer = (CombatLogStreamer)RuntimeHelpers.GetUninitializedObject(typeof(CombatLogStreamer));
        void Set(string name, object value) => typeof(CombatLogStreamer).GetField(name, Private).SetValue(streamer, value);
        Set("_logToMonitor", path);
        Set("_fileEncoding", Encoding.UTF8);
        Set("_numberOfProcessedBytes", cursor);
        Set("_combatLock", new object());
        Set("_monitoringLock", new object());
        Set("_monitoringTask", Task.CompletedTask);
        Set("_combatDetector", new CombatDetector());
        Set("_pendingLiveLogs", new List<ParsedLogEntry>());
        Set("_currentCombatLogs", new List<ParsedLogEntry>());
        Set("_followNewestLog", false);
        return streamer;
    }
}
