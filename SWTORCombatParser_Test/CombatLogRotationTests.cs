using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Newtonsoft.Json;
using NUnit.Framework;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.Timers;

namespace SWTORCombatParser_Test;

[NonParallelizable]
public class CombatLogRotationTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags Shared = BindingFlags.Static | BindingFlags.NonPublic;
    private static readonly DateTime OldDate = new(2026, 11, 5);
    private static readonly DateTime NewDate = OldDate.AddDays(1);
    private readonly Dictionary<FieldInfo, object> _savedFields = new();
    private readonly List<string> _events = new();
    private readonly List<CombatStatusUpdate> _updates = new();
    private readonly List<ParsedLogEntry> _liveLines = new();
    private string _directory;
    private string _previousLogDirectory;
    private LogState _previousState;
    private CombatLogStreamer _streamer;
    private CombatDetector _detector;

    [SetUp]
    public void Setup()
    {
        _directory = Path.Combine(Path.GetTempPath(), "orbs-rotation-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _previousState = CombatLogStateBuilder.CurrentState;
        _previousLogDirectory = CombatLogLoader.LoggingPath;
        _events.Clear();
        _updates.Clear();
        _liveLines.Clear();
        SaveAndSet(typeof(Settings), "_appDataPath", _directory);
        var settings = Path.Combine(_directory, "settings.json");
        File.WriteAllText(settings, JsonConvert.SerializeObject(new { combat_logs_path = _directory }));
        SaveAndSet(typeof(Settings), "_settingsPath", settings);
        SaveAndSet(typeof(CombatLogStreamer), "_isInCombat", false);
        SaveAndSet(typeof(CombatLogStreamer), "CombatStarted", (Action)(() => _events.Add("start")));
        SaveAndSet(typeof(CombatLogStreamer), "CombatUpdated", (Action<CombatStatusUpdate>)(update =>
        {
            _updates.Add(update);
            _events.Add(update.Type.ToString());
        }));
        SaveAndSet(typeof(CombatLogStreamer), "HistoricalLogsStarted", (Action)(() => _events.Add("history-start")));
        SaveAndSet(typeof(CombatLogStreamer), "HistoricalLogsFinished", (Action<DateTime, bool>)((_, _) => _events.Add("history-finish")));
        SaveAndSet(typeof(CombatLogStreamer), "NewLineStreamed", (Action<ParsedLogEntry>)_liveLines.Add);
        SaveAndSet(typeof(EncounterTimerTrigger), "PvPEncounterEntered", (Action<DateTime, EncounterInfo>)((_, _) => { }));
        SaveAndSet(typeof(EncounterTimerTrigger), "NonPvpEncounterEntered", (Action<DateTime, EncounterInfo>)((_, _) => { }));
        SaveAndSet(typeof(EncounterTimerTrigger), "<CurrentEncounter>k__BackingField", EncounterTimerTrigger.CurrentEncounter);
        SaveAndSet(typeof(_7_0LogParsing), "_currentEntities", new ConcurrentDictionary<long, Entity>());
        SaveAndSet(typeof(_7_0LogParsing), "_dateTime", OldDate);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        _7_0LogParsing.SetupRegex();
        CombatLogLoader.LoggingPath = _directory;
        _detector = new CombatDetector();
        _streamer = (CombatLogStreamer)RuntimeHelpers.GetUninitializedObject(typeof(CombatLogStreamer));
        Set("_combatLock", new object());
        Set("_combatDetector", _detector);
        Set("_pendingLiveLogs", new List<ParsedLogEntry>());
        Set("_currentCombatLogs", new List<ParsedLogEntry>());
        Set("_fileEncoding", Encoding.GetEncoding(1252));
        Set("_followNewestLog", true);
        _streamer.LogFileRotated += () =>
        {
            Assert.That(_detector.InCombat, Is.False, "The old detector must stop before UI reset.");
            _events.Add("rotated");
        };
    }

    [TearDown]
    public void Cleanup()
    {
        _detector?.Reset();
        foreach (var saved in _savedFields)
            saved.Key.SetValue(null, saved.Value);
        _savedFields.Clear();
        CombatLogStateBuilder.CurrentState = _previousState;
        CombatLogLoader.LoggingPath = _previousLogDirectory;
        Directory.Delete(_directory, true);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AutomaticRotationFinishesOldFightAndInitializesNewStateBeforeTailing(bool forceUpdates)
    {
        var oldPath = WriteLog("old.txt", "Old", OldDate, Encoding.GetEncoding(1252));
        LoadInitial(oldPath);
        var oldState = CombatLogStateBuilder.CurrentState;
        var oldPlayer = Entities[777];
        oldPlayer.IsBoss = true;
        oldPlayer.IsLocalPlayer = true;
        // An abruptly ended boss log must not make the next file's EnterCombat
        // look like a revive/continuation of the previous encounter.
        typeof(CombatDetector).GetField("_bossCombat", Private)!.SetValue(_detector, true);
        File.AppendAllText(oldPath, Damage("Old") + "\n", Encoding.GetEncoding(1252));
        File.SetLastWriteTime(oldPath, OldDate.AddHours(18));
        var newPath = WriteLog("new.txt", "New 漢字", NewDate, Encoding.Unicode);
        Set("_forceUpdateOfLogs", forceUpdates);
        Poll();

        Assert.Multiple(() =>
        {
            Assert.That(_updates.Select(update => update.Type), Is.EqualTo(new[] { UpdateType.Update, UpdateType.Stop }));
            Assert.That(_updates.All(update => update.CombatStartTime == OldDate.AddHours(12)), Is.True,
                "Stopping the previous combat must retain its original start identity.");
            Assert.That(_updates[0].Logs.Single().Effect.EffectId, Is.EqualTo(_7_0LogParsing._damageEffectId),
                "The previous file's final damage must be delivered before its Stop event.");
            Assert.That(_events, Is.EqualTo(new[] { "Update", "Stop", "rotated", "history-start", "history-finish" }));
            Assert.That(_streamer.CurrentLog, Is.EqualTo(newPath));
            Assert.That(CombatLogStateBuilder.CurrentState, Is.Not.SameAs(oldState));
            Assert.That(Entities[777], Is.Not.SameAs(oldPlayer));
            Assert.That(Entities[777].Name, Is.EqualTo("New 漢字"));
            Assert.That(Entities[777].IsBoss || Entities[777].IsLocalPlayer, Is.False);
            Assert.That(oldPlayer.IsBoss && oldPlayer.IsLocalPlayer, Is.True,
                "Previously retained combat entities must not be mutated by the reset.");
            Assert.That(Get<DateTime>("_currentCombatStartTime"), Is.EqualTo(NewDate.AddHours(12)));
            Assert.That(Get<Encoding>("_fileEncoding").CodePage, Is.EqualTo(Encoding.Unicode.CodePage));
            Assert.That(Get<long>("_numberOfProcessedBytes"), Is.EqualTo(new FileInfo(newPath).Length));
            Assert.That(_liveLines, Has.Count.EqualTo(1), "Only the old file's unread damage should be streamed; the new historical seed is not live.");
            Assert.That(_liveLines.Single().Source, Is.SameAs(oldPlayer));
            Assert.That(_liveLines.Single().LogName, Is.EqualTo("old.txt"));
        });

        var newPlayer = Entities[777];
        _liveLines.Clear();
        File.AppendAllText(newPath, Buff("New 漢字", "12:00:02.000", true) + "\n", Encoding.Unicode);
        Poll();
        Poll();
        Assert.Multiple(() =>
        {
            Assert.That(_updates.Select(update => update.Type), Is.EqualTo(new[] { UpdateType.Update, UpdateType.Stop, UpdateType.Update }));
            Assert.That(_updates.Last().CombatStartTime, Is.EqualTo(NewDate.AddHours(12)));
            Assert.That(_liveLines, Has.Count.EqualTo(1), "The first tail frame and subsequent polls must not duplicate the seed or append.");
            Assert.That(_liveLines.Single().TimeStamp, Is.EqualTo(NewDate.AddHours(12).AddSeconds(2)));
            Assert.That(_liveLines.Single().Source, Is.SameAs(newPlayer));
            Assert.That(CombatLogStateBuilder.CurrentState.Modifiers[1234].Values.Single().Complete, Is.True);
        });
    }

    [Test]
    public void RotationStillInitializesNewFileWhenOldFileHasBeenRemoved()
    {
        var oldPath = WriteLog("removed.txt", "Old", OldDate, Encoding.GetEncoding(1252));
        LoadInitial(oldPath);
        File.Delete(oldPath);
        var newPath = WriteLog("new.txt", "New", NewDate, Encoding.UTF8);

        Poll();

        Assert.That(_streamer.CurrentLog, Is.EqualTo(newPath));
        Assert.That(_events, Is.EqualTo(new[] { "Stop", "rotated", "history-start", "history-finish" }));
        Assert.That(Entities[777].Name, Is.EqualTo("New"));
    }

    [Test]
    public void PinnedReplayDoesNotFollowANewerLog()
    {
        var pinned = WriteLog("pinned.txt", "Pinned", OldDate, Encoding.UTF8);
        LoadInitial(pinned);
        var state = CombatLogStateBuilder.CurrentState;
        var newer = WriteLog("newer.txt", "Other", NewDate, Encoding.Unicode);
        Set("_followNewestLog", false);
        File.AppendAllText(pinned, Buff("Pinned", "12:00:02.000", true) + "\n", Encoding.UTF8);
        File.SetLastWriteTime(newer, DateTime.Now.AddDays(1));

        Poll();

        Assert.Multiple(() =>
        {
            Assert.That(_streamer.CurrentLog, Is.EqualTo(pinned));
            Assert.That(CombatLogStateBuilder.CurrentState, Is.SameAs(state));
            Assert.That(_events, Is.EqualTo(new[] { "Update" }));
            Assert.That(_liveLines.Single().Source.Name, Is.EqualTo("Pinned"));
        });
    }

    private string WriteLog(string name, string player, DateTime date, Encoding encoding)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, Enter(player) + "\n" + Buff(player, "12:00:01.000") + "\n", encoding);
        File.SetLastWriteTime(path, date.AddHours(18));
        return path;
    }

    private void LoadInitial(string path)
    {
        Set("_logToMonitor", path);
        typeof(CombatLogStreamer).GetMethod("ParseExisitingLogs", Private)!.Invoke(_streamer, new object[] { true, null });
        Assert.That(CombatLogStreamer.InCombat, Is.True);
        _events.Clear();
        _updates.Clear();
    }

    private void Poll() => typeof(CombatLogStreamer).GetMethod("GenerateNewFrame", Private)!.Invoke(_streamer, null);
    private void Set(string field, object value) => typeof(CombatLogStreamer).GetField(field, Private)!.SetValue(_streamer, value);
    private T Get<T>(string field) => (T)typeof(CombatLogStreamer).GetField(field, Private)!.GetValue(_streamer)!;
    private static ConcurrentDictionary<long, Entity> Entities => (ConcurrentDictionary<long, Entity>)typeof(_7_0LogParsing)
        .GetField("_currentEntities", Shared)!.GetValue(null)!;

    private void SaveAndSet(Type type, string name, object value)
    {
        var field = type.GetField(name, Shared)!;
        _savedFields[field] = field.GetValue(null);
        field.SetValue(null, value);
    }

    private static string Enter(string player) => $"[12:00:00.000] [@{player}#777|(0,0,0,0)|(100/100)] [=] [] [Event {{836045448945472}}: EnterCombat {{836045448945489}}]";
    private static string Damage(string player) => $"[12:00:01.500] [@{player}#777|(0,0,0,0)|(100/100)] [NPC {{42}}:42|(0,0,0,0)|(100/100)] [Attack {{100}}] [ApplyEffect {{836045448945477}}: Damage {{836045448945501}}] (10 energy {{836045448940874}}) <10>";
    private static string Buff(string player, string time, bool remove = false) =>
        $"[{time}] [@{player}#777|(0,0,0,0)|(100/100)] [=] [Buff {{1234}}] [{(remove ? "RemoveEffect" : "ApplyEffect")} {{{(remove ? 836045448945478 : 836045448945477)}}}: Buff {{1234}}]";
}
