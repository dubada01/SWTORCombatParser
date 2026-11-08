using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json.Nodes;
using NUnit.Framework;
using SWTORCombatParser.Model.LogIndex;
using SWTORCombatParser.Model.LogParsing;

namespace SWTORCombatParser_Test;

[TestFixture]
public class CombatLogIndexTests
{
    private string _temporaryDirectory;
    private string _logs;
    private string _cache;
    private string _metadata;

    [SetUp]
    public void SetUp()
    {
        _temporaryDirectory = Path.Combine(Path.GetTempPath(), "orbs-log-index-tests-" + Guid.NewGuid().ToString("N"));
        _logs = Path.Combine(_temporaryDirectory, "logs");
        _cache = Path.Combine(_temporaryDirectory, "cache");
        Directory.CreateDirectory(_logs);
        _metadata = Path.Combine(FindRepository(), "DataStructures", "EncounterInfo");
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_temporaryDirectory, true);

    [Test]
    public async Task IndexesBossAttemptsMidnightPlayersAndDeclaredModeWithoutChangingLiveState()
    {
        var path = Path.Combine(_logs, "combat_2026-11-06_23_59_00.txt");
        File.WriteAllLines(path, new[]
        {
            Area("23:59:58.000", "Eternity Vault {833571547775670} 8 Player Veteran {836045448953652}"),
            Event("23:59:59.000", "EnterCombat {836045448945489}"),
            Hit("00:00:02.000", "Gharj {2034526008115200}:42"),
            Event("00:00:03.000", "ExitCombat {836045448945490}"),
            Death("00:00:03.100", "Gharj {2034526008115200}:42"),
            Event("00:01:00.000", "EnterCombat {836045448945489}"),
            Hit("00:01:03.000", "Soa {2289823159156736}:43"),
            Event("00:01:10.000", "ExitCombat {836045448945490}")
        });
        var liveState = CombatLogStateBuilder.CurrentState;
        var logs = await Service().RefreshAsync(_logs);

        Assert.That(CombatLogStateBuilder.CurrentState, Is.SameAs(liveState));
        var log = logs.Single();
        Assert.That(log.StartTime.Date, Is.EqualTo(new DateTime(2026, 11, 6)));
        Assert.That(log.EndTime.Date, Is.EqualTo(new DateTime(2026, 11, 7)));
        Assert.That(log.Players, Is.EquivalentTo(new[] { "Tester" }));
        Assert.That(log.Encounters.Select(e => e.BossName), Is.EqualTo(new[] { "Gharj", "Soa" }));
        Assert.That(log.Encounters.Select(e => e.Outcome), Is.EqualTo(new[] { "Kill", "Attempt" }));
        Assert.That(log.Encounters.All(e => e.ActivityType == "Operation" && e.PlayerCount == 8 && e.Difficulty == "Veteran"), Is.True);
        Assert.That(CombatLogSearch.Search(logs, new CombatLogSearchFilter { FromDate = new DateTime(2026, 11, 7) }), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task CompanionCombatEventsDoNotSplitThePlayerAttempt()
    {
        File.WriteAllLines(Path.Combine(_logs, "combat_2026-11-06_23_59_00.txt"), new[]
        {
            Area("00:00:00.000", "Hammer Station {833571547775682} 4 Player Veteran {836045448953657}"),
            Event("00:00:01.000", "EnterCombat {836045448945489}"),
            Hit("00:00:02.000", "Battlelord Kreshan {0}:44"),
            "[00:00:03.000] [@Tester#123/Lana Beniko {3600471084236800}:55] [] [] [Event {836045448945472}: ExitCombat {836045448945490}]",
            Hit("00:00:08.000", "Battlelord Kreshan {0}:44"),
            Event("00:00:10.000", "ExitCombat {836045448945490}")
        });
        var indexed = (await Service().RefreshAsync(_logs)).Single();
        Assert.That(indexed.StartTime.Date, Is.EqualTo(new DateTime(2026, 11, 7)), "The first log entry may occur after midnight relative to the filename.");
        Assert.That(indexed.Encounters, Has.Count.EqualTo(1));
        Assert.That((indexed.Encounters.Single().EndTime - indexed.Encounters.Single().StartTime).TotalSeconds, Is.EqualTo(9));
        Assert.That(indexed.Players, Is.EqualTo(new[] { "Tester" }));
    }

    [Test]
    public async Task ReusesPersistentCacheAndReindexesChangedFilesWhileRemovingDeletedFiles()
    {
        var first = Path.Combine(_logs, "combat_2026-11-06_10_00_00.txt");
        var second = Path.Combine(_logs, "combat_2026-11-06_11_00_00.txt");
        File.WriteAllLines(first, SimpleCombat());
        File.WriteAllLines(second, SimpleCombat());
        var initial = await Service().RefreshAsync(_logs);
        Assert.That(initial, Has.Count.EqualTo(2));
        var reloaded = Service();
        Assert.That(reloaded.GetCached(_logs), Has.Count.EqualTo(2));
        LogIndexProgress progress = null;
        await reloaded.RefreshAsync(_logs, new SynchronousProgress(p => progress = p));
        Assert.That(progress.ReusedFiles, Is.EqualTo(2));
        Assert.That(progress.IndexedFiles, Is.Zero);

        File.AppendAllLines(first, new[] { Event("11:00:00.000", "EnterCombat {836045448945489}"), Hit("11:00:01.000", "Training Dummy {123}:9") });
        File.Delete(second);
        var changed = await reloaded.RefreshAsync(_logs, new SynchronousProgress(p => progress = p));
        Assert.That(changed, Has.Count.EqualTo(1));
        Assert.That(progress.IndexedFiles, Is.EqualTo(1));
        Assert.That(changed.Single().Encounters, Has.Count.EqualTo(2));
        Assert.That(changed.Single().Encounters.Last().Outcome, Is.EqualTo("In progress"));
        Assert.That(initial.Single(l => l.FilePath == first).Encounters, Has.Count.EqualTo(1), "Published snapshots must not mutate during refresh.");
    }

    [Test]
    public async Task CancellationPreservesPreviousSnapshotAndAllowsNextRefresh()
    {
        File.WriteAllLines(Path.Combine(_logs, "combat_2026-11-06_10_00_00.txt"), SimpleCombat());
        var service = Service();
        var initial = await service.RefreshAsync(_logs);
        File.WriteAllLines(Path.Combine(_logs, "combat_2026-11-07_10_00_00.txt"), SimpleCombat());
        using var cancellation = new CancellationTokenSource();
        var progress = new SynchronousProgress(_ => cancellation.Cancel());
        Assert.ThrowsAsync<OperationCanceledException>(async () => await service.RefreshAsync(_logs, progress, cancellation.Token));
        Assert.That(service.GetCached(_logs), Is.SameAs(initial));
        Assert.That(await service.RefreshAsync(_logs), Has.Count.EqualTo(2));
    }

    [Test]
    public async Task SavedMetadataSurvivesBuildChangesWithoutReadingUnchangedLogsOrCatalog()
    {
        var path = Path.Combine(_logs, "combat_2026-11-06_10_00_00.txt");
        File.WriteAllLines(path, SimpleCombat());
        await Service().RefreshAsync(_logs);
        var cachePath = Directory.GetFiles(_cache, "*.json").Single();
        var oldCache = JsonNode.Parse(File.ReadAllText(cachePath));
        oldCache["CatalogFingerprint"] = "a-different-build-or-catalog";
        File.WriteAllText(cachePath, oldCache.ToJsonString());
        var cacheStamp = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(cachePath, cacheStamp);

        var reopened = new CombatLogIndexService(_cache, Path.Combine(_temporaryDirectory, "unavailable-catalog"));
        Assert.That(reopened.GetCached(_logs), Has.Count.EqualTo(1));
        LogIndexProgress progress = null;
        using var lockedLog = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var results = await reopened.RefreshAsync(_logs, new SynchronousProgress(p => progress = p));
        Assert.Multiple(() =>
        {
            Assert.That(results.Single().Encounters, Has.Count.EqualTo(1));
            Assert.That(progress.ReusedFiles, Is.EqualTo(1));
            Assert.That(progress.IndexedFiles, Is.Zero);
            Assert.That(progress.FailedFiles, Is.Zero);
            Assert.That(File.GetLastWriteTimeUtc(cachePath), Is.EqualTo(cacheStamp), "A no-op refresh must not rewrite the saved index.");
        });
    }

    [Test]
    public async Task NewestResultIsPublishedAndDurableBeforeOlderLogsAreScanned()
    {
        var names = new[] { "combat_2026-11-07_10_00_00.txt", "combat_2026-11-06_10_00_00.txt", "combat_2026-11-05_10_00_00.txt" };
        for (var i = 0; i < names.Length; i++)
        {
            var path = Path.Combine(_logs, names[i]);
            File.WriteAllLines(path, SimpleCombat());
            // A copied old file may have the newest modification date: prioritize the actual session date.
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddHours(i));
        }
        var service = Service();
        using var cancellation = new CancellationTokenSource();
        var progress = new SynchronousProgress(update =>
        {
            if (update.IndexedFiles != 1 || update.AvailableLogs == null) return;
            Assert.That(update.ProcessedFiles, Is.EqualTo(1));
            Assert.That(update.AvailableLogs.Single().FileName, Is.EqualTo(names[0]));
            Assert.That(Service().GetCached(_logs).Single().FileName, Is.EqualTo(names[0]), "Publication must follow the durable write.");
            cancellation.Cancel();
        });
        Assert.CatchAsync<OperationCanceledException>(async () => await service.RefreshAsync(_logs, progress, cancellation.Token));
        Assert.That(service.GetCached(_logs), Has.Count.EqualTo(1));

        LogIndexProgress finished = null;
        using var lockedNewest = new FileStream(Path.Combine(_logs, names[0]), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var resumed = await Service().RefreshAsync(_logs, new SynchronousProgress(update => finished = update));
        Assert.That(resumed.Select(log => log.FileName), Is.EqualTo(names));
        Assert.That(finished.ReusedFiles, Is.EqualTo(1));
        Assert.That(finished.IndexedFiles, Is.EqualTo(2), "Restarting must continue with older files, not parse the newest one again.");
        Assert.That(Directory.GetFiles(_cache), Has.Length.EqualTo(1), "A finished refresh compacts the journal into one snapshot.");
    }

    [Test]
    public async Task DeletedLogIsRemovedPermanentlyWithoutOpeningRemainingLogs()
    {
        var retained = Path.Combine(_logs, "combat_2026-11-07_10_00_00.txt");
        var deleted = Path.Combine(_logs, "combat_2026-11-06_10_00_00.txt");
        File.WriteAllLines(retained, SimpleCombat());
        File.WriteAllLines(deleted, SimpleCombat());
        await Service().RefreshAsync(_logs);
        File.Delete(deleted);
        using var locked = new FileStream(retained, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        LogIndexProgress progress = null;
        await new CombatLogIndexService(_cache, "catalog-must-not-be-read").RefreshAsync(_logs, new SynchronousProgress(update => progress = update));
        Assert.That(progress.IndexedFiles, Is.Zero);
        Assert.That(progress.FailedFiles, Is.Zero);
        Assert.That(Service().GetCached(_logs).Single().FilePath, Is.EqualTo(retained));
    }

    [Test]
    public async Task SameSizeFileEditIsDetectedByModificationStampAndSaved()
    {
        var path = Path.Combine(_logs, "combat_2026-11-06_10_00_00.txt");
        File.WriteAllLines(path, SimpleCombat());
        var before = (await Service().RefreshAsync(_logs)).Single();
        File.WriteAllText(path, File.ReadAllText(path).Replace("Tester", "Player"));
        File.SetLastWriteTimeUtc(path, before.LastWriteTimeUtc.AddSeconds(2));
        Assert.That(new FileInfo(path).Length, Is.EqualTo(before.FileSize));
        LogIndexProgress progress = null;
        await Service().RefreshAsync(_logs, new SynchronousProgress(update => progress = update));
        Assert.That(progress.IndexedFiles, Is.EqualTo(1));
        Assert.That(Service().GetCached(_logs).Single().Players, Is.EqualTo(new[] { "Player" }));
    }

    [Test]
    public async Task TemporarilyUnreadableChangedLogKeepsItsLastGoodMetadataAndRetriesLater()
    {
        var path = Path.Combine(_logs, "combat_2026-11-06_10_00_00.txt");
        File.WriteAllLines(path, SimpleCombat());
        var before = (await Service().RefreshAsync(_logs)).Single();
        File.AppendAllText(path, Event("10:00:20.000", "EnterCombat {836045448945489}") + Environment.NewLine);
        LogIndexProgress progress = null;
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var available = await Service().RefreshAsync(_logs, new SynchronousProgress(update => progress = update));
            Assert.That(available.Single().FileSize, Is.EqualTo(before.FileSize));
            Assert.That(progress.FailedFiles, Is.EqualTo(1));
            Assert.That(Service().GetCached(_logs).Single().FileSize, Is.EqualTo(before.FileSize));
        }
        await Service().RefreshAsync(_logs, new SynchronousProgress(update => progress = update));
        Assert.That(progress.IndexedFiles, Is.EqualTo(1));
        Assert.That(progress.FailedFiles, Is.Zero);
        Assert.That(Service().GetCached(_logs).Single().FileSize, Is.GreaterThan(before.FileSize));
    }

    [Test]
    public async Task InterruptedJournalRetainsCompletedFilesAndAcceptsFurtherUpdates()
    {
        for (var day = 5; day <= 7; day++)
            File.WriteAllLines(Path.Combine(_logs, $"combat_2026-11-{day:00}_10_00_00.txt"), SimpleCombat());
        async Task IndexOneMore()
        {
            using var cancellation = new CancellationTokenSource();
            var progress = new SynchronousProgress(update => { if (update.IndexedFiles == 1) cancellation.Cancel(); });
            await Service().RefreshAsync(_logs, progress, cancellation.Token);
        }
        Assert.CatchAsync<OperationCanceledException>(IndexOneMore);
        var journal = Directory.GetFiles(_cache, "*.updates").Single();
        File.AppendAllText(journal, "{\"incomplete-record\":");
        Assert.CatchAsync<OperationCanceledException>(IndexOneMore);
        Assert.That(Service().GetCached(_logs).Select(log => log.FileName), Is.EqualTo(new[]
        {
            "combat_2026-11-07_10_00_00.txt", "combat_2026-11-06_10_00_00.txt"
        }));
        Assert.That(await Service().RefreshAsync(_logs), Has.Count.EqualTo(3));
    }

    [Test]
    public async Task FailedDiskSaveCanBeRetriedWithoutParsingTheLogAgain()
    {
        var path = Path.Combine(_logs, "combat_2026-11-06_10_00_00.txt");
        File.WriteAllLines(path, SimpleCombat());
        File.WriteAllText(_cache, "The cache directory is temporarily blocked by a file.");
        var service = Service();
        LogIndexProgress progress = null;
        Assert.That(await service.RefreshAsync(_logs, new SynchronousProgress(update => progress = update)), Has.Count.EqualTo(1));
        Assert.That(progress.Message, Does.Contain("could not be saved"));
        File.Delete(_cache);
        using var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        await service.RefreshAsync(_logs, new SynchronousProgress(update => progress = update));
        Assert.That(progress.IndexedFiles, Is.Zero);
        Assert.That(progress.ReusedFiles, Is.EqualTo(1));
        Assert.That(progress.Message, Does.Not.Contain("could not be saved"));
        Assert.That(Service().GetCached(_logs), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task CorruptCacheFallsBackToScanningAndPvpKeepsUnknownGroupSize()
    {
        File.WriteAllLines(Path.Combine(_logs, "combat_2026-11-06_10_00_00.txt"), new[]
        {
            Area("10:00:00.000", "The Civil War {137438956902}"),
            Event("10:00:01.000", "EnterCombat {836045448945489}"),
            Hit("10:00:02.000", "@Opponent#987654"),
            Event("10:00:10.000", "ExitCombat {836045448945490}")
        });
        await Service().RefreshAsync(_logs);
        File.WriteAllText(Directory.GetFiles(_cache).Single(), "{broken");
        var service = Service();
        Assert.That(service.GetCached(_logs), Is.Empty);
        var indexed = await service.RefreshAsync(_logs);
        var pvp = indexed.Single().Encounters.Single();
        Assert.That(pvp.ActivityType, Is.EqualTo("PvP"));
        Assert.That(pvp.PlayerCount, Is.Zero);
        Assert.That(pvp.Players, Does.Contain("Opponent"));
        Assert.That(CombatLogSearch.Search(indexed, new CombatLogSearchFilter { IsPvp = true, PlayerName = "opp" }), Has.Count.EqualTo(1));
        Assert.That(CombatLogSearch.Search(indexed, new CombatLogSearchFilter { IsPvp = false }), Is.Empty);
    }

    [Test]
    public void FiltersMustMatchOneEncounterIncludingPlayerAndCalendarDate()
    {
        var log = new IndexedCombatLog
        {
            StartTime = new DateTime(2026, 11, 6, 22, 0, 0), EndTime = new DateTime(2026, 11, 7, 1, 0, 0),
            Players = new() { "Alice", "Bob" },
            Encounters = new()
            {
                new IndexedEncounter { EncounterName = "Eternity Vault", BossName = "Gharj", Difficulty = "Story", PlayerCount = 8,
                    StartTime = new DateTime(2026, 11, 6, 22, 0, 0), EndTime = new DateTime(2026, 11, 6, 22, 5, 0), Players = new() { "Alice" } },
                new IndexedEncounter { EncounterName = "Hammer Station", BossName = "Battlelord Kreshan", Difficulty = "Master", PlayerCount = 4,
                    StartTime = new DateTime(2026, 11, 7, 0, 30, 0), EndTime = new DateTime(2026, 11, 7, 0, 35, 0), Players = new() { "Bob" } }
            }
        };
        Assert.That(CombatLogSearch.Search(new[] { log }, new CombatLogSearchFilter { BossName = "Gharj", Difficulty = "Master" }), Is.Empty);
        Assert.That(CombatLogSearch.Search(new[] { log }, new CombatLogSearchFilter { BossName = "Gharj", PlayerName = "Bob" }), Is.Empty);
        Assert.That(CombatLogSearch.Search(new[] { log }, new CombatLogSearchFilter { BossName = "Gharj", FromDate = new DateTime(2026, 11, 7) }), Is.Empty);
        Assert.That(CombatLogSearch.Search(new[] { log }, new CombatLogSearchFilter { BossName = "gharj", Difficulty = "story", PlayerName = "ALI" }), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task BundledCombatLogRecognizesHammerStationAndDoesNotModifyLiveParser()
    {
        var fixture = Path.Combine(FindRepository(), "TestCombatLogs", "combat_2024-10-18_19_38_43_410593.txt");
        File.Copy(fixture, Path.Combine(_logs, Path.GetFileName(fixture)));
        var liveState = CombatLogStateBuilder.CurrentState;
        var indexed = await Service().RefreshAsync(_logs);
        var flashpoint = indexed.Single().Encounters.Where(e => e.EncounterName == "Hammer Station").ToArray();
        Assert.That(flashpoint, Is.Not.Empty);
        Assert.That(flashpoint.Any(e => !string.IsNullOrEmpty(e.BossName)), Is.True);
        Assert.That(flashpoint.All(e => e.PlayerCount == 4 && e.Difficulty == "Veteran"), Is.True);
        Assert.That(CombatLogStateBuilder.CurrentState, Is.SameAs(liveState));
    }

    private CombatLogIndexService Service() => new(_cache, _metadata);
    private static string[] SimpleCombat() => new[]
    {
        Area("10:00:00.000", "Hammer Station {833571547775682} 4 Player Veteran {836045448953657}"),
        Event("10:00:01.000", "EnterCombat {836045448945489}"),
        Hit("10:00:02.000", "Battlelord Kreshan {0}:44"),
        Event("10:00:10.000", "ExitCombat {836045448945490}")
    };
    private static string Area(string time, string area) => $"[{time}] [@Tester#123|(0,0,0,0)|(100/100)] [] [] [AreaEntered {{836045448953664}}: {area}]";
    private static string Event(string time, string effect) => $"[{time}] [@Tester#123|(0,0,0,0)|(100/100)] [] [] [Event {{836045448945472}}: {effect}]";
    private static string Hit(string time, string target) => $"[{time}] [@Tester#123] [{target}] [Attack {{1000}}] [ApplyEffect {{836045448945477}}: Damage {{836045448945501}}] (100 energy {{836045448940874}})";
    private static string Death(string time, string target) => $"[{time}] [@Tester#123] [{target}] [] [Event {{836045448945472}}: Death {{836045448945493}}]";
    private static string FindRepository()
    {
        for (var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); directory != null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "SWTORCombatParser.csproj"))) return directory.FullName;
        throw new DirectoryNotFoundException("Cannot find the combat log test fixtures.");
    }
    private sealed class SynchronousProgress(Action<LogIndexProgress> report) : IProgress<LogIndexProgress>
    {
        public void Report(LogIndexProgress value) => report(value);
    }
}
