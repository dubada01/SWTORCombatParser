using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using SWTORCombatParser.DataStructures.EncounterInfo;

namespace SWTORCombatParser.Model.LogIndex;

/// <summary>A lightweight, isolated reader of area, player, and combat boundaries; no live parser state or event publication.</summary>
internal sealed class CombatLogMetadataScanner
{
    private const string EnterCombat = "836045448945489";
    private const string ExitCombat = "836045448945490";
    private const string Death = "836045448945493";
    private const string Damage = "836045448945501";
    private const string AreaEntered = "836045448953664";
    private readonly Dictionary<ulong, Area> _areas = new();
    private readonly Dictionary<long, Boss> _worldBosses = new();

    public CombatLogMetadataScanner(string metadataDirectory)
    {
        ReadAreas(Path.Combine(metadataDirectory, "RaidNames.json"), "Operation");
        ReadAreas(Path.Combine(metadataDirectory, "FlashpointInfo.json"), "Flashpoint");
        ReadAreas(Path.Combine(metadataDirectory, "PVPAreaNames.json"), "PvP");
        var worldFile = Path.Combine(metadataDirectory, "OpenWorldBosses.json");
        if (File.Exists(worldFile))
            foreach (var boss in JsonConvert.DeserializeObject<List<OpenWorldBoss>>(File.ReadAllText(worldFile)) ?? new())
                _worldBosses[boss.BossId] = new Boss(boss.BossName, new() { boss.BossId }, new() { boss.BossId }, new(), 0);
    }

    private void ReadAreas(string path, string activity)
    {
        // Failure to load packaged metadata is actionable; do not persist an index silently stripped of bosses.
        var encounters = JsonConvert.DeserializeObject<List<EncounterInfo>>(File.ReadAllText(path)) ?? new();
        LogIdFactory.AddIdsToEncounters(encounters);
        foreach (var encounter in encounters)
            _areas[encounter.LogId] = new Area(encounter.Name, activity, encounter);
    }

    public IndexedCombatLog Scan(FileInfo file, CancellationToken cancellationToken)
    {
        var length = file.Length;
        var result = new IndexedCombatLog
        {
            FilePath = file.FullName, FileName = file.Name, FileSize = length, LastWriteTimeUtc = file.LastWriteTimeUtc
        };
        var fileStart = FileStart(file);
        var date = fileStart.Date;
        DateTime? lastTime = null;
        string localPlayer = "";
        var players = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var area = new Area("Open World", "Other", null);
        string difficulty = "";
        int groupSize = 0;
        Combat? combat = null;
        DateTime? exitedAt = null;
        var byId = new Dictionary<long, Boss>(_worldBosses);
        var byName = new Dictionary<string, Boss>(StringComparer.OrdinalIgnoreCase);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var fileStream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 65536, FileOptions.SequentialScan);
        using var bounded = new BoundedReadStream(fileStream, length);
        using var reader = new StreamReader(bounded, Encoding.GetEncoding(1252), true, 65536);
        string? line;
        int lineNumber = 0;
        while ((line = reader.ReadLine()) != null)
        {
            if ((lineNumber++ & 255) == 0) cancellationToken.ThrowIfCancellationRequested();
            var remaining = line.AsSpan();
            var timestampText = NextField(ref remaining);
            if (!TimeSpan.TryParseExact(timestampText, @"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture, out var clock)) continue;
            var timestamp = date + clock;
            if (!lastTime.HasValue && timestamp < fileStart.AddHours(-12))
            {
                date = date.AddDays(1);
                timestamp = date + clock;
            }
            // A small out-of-order timestamp is not a midnight boundary.
            if (lastTime.HasValue && timestamp < lastTime.Value.AddHours(-12))
            {
                date = date.AddDays(1);
                timestamp = date + clock;
            }
            if (!lastTime.HasValue) result.StartTime = timestamp;
            lastTime = !lastTime.HasValue || timestamp > lastTime.Value ? timestamp : lastTime;
            var source = Actor.Read(NextField(ref remaining));
            var targetField = NextField(ref remaining);
            var target = targetField.SequenceEqual("=") ? source : Actor.Read(targetField);
            var ability = NextField(ref remaining);
            var effect = NextField(ref remaining);
            if (effect.IsEmpty) continue; // Ignore a partial trailing record from a currently written log.
            if (source.IsPlayer) players.Add(source.Name);
            if (target.IsPlayer) players.Add(target.Name);

            if (combat != null && exitedAt.HasValue && timestamp - exitedAt.Value > TimeSpan.FromSeconds(3))
            {
                CompleteCombat(result, combat, exitedAt.Value, false);
                combat = null;
                exitedAt = null;
            }

            if (effect.Contains(AreaEntered, StringComparison.Ordinal))
            {
                if (combat != null)
                {
                    CompleteCombat(result, combat, exitedAt ?? timestamp, false);
                    combat = null;
                    exitedAt = null;
                }
                if (source.IsPlayer) localPlayer = source.Name;
                ReadArea(effect, out var areaId, out var areaName, out var difficultyId);
                area = _areas.TryGetValue(areaId, out var known) ? known : new Area(areaName, "Other", null);
                difficulty = EncounterLoader.GetLeaderboardFriendlyDifficulty(difficultyId);
                groupSize = difficultyId switch
                {
                    836045448953651 or 836045448953652 or 836045448953655 => 8,
                    836045448953653 or 836045448953654 or 836045448953656 => 16,
                    836045448953657 or 836045448953658 or 836045448953659 => 4,
                    _ => 0
                };
                BuildBossLookup(area, difficulty, groupSize, out byId, out byName);
                continue;
            }

            bool localEvent = source.IsPlayer && (localPlayer.Length == 0 || source.Name.Equals(localPlayer, StringComparison.OrdinalIgnoreCase));
            if (localEvent && effect.Contains(EnterCombat, StringComparison.Ordinal))
            {
                if (localPlayer.Length == 0) localPlayer = source.Name;
                if (combat != null && exitedAt.HasValue)
                {
                    CompleteCombat(result, combat, exitedAt.Value, false);
                    combat = null;
                }
                combat ??= new Combat(timestamp, area, difficulty, groupSize);
                exitedAt = null;
            }
            if (combat == null) continue;
            if (source.IsPlayer) combat.Players.Add(source.Name);
            if (target.IsPlayer) combat.Players.Add(target.Name);
            bool isDamage = effect.Contains(Damage, StringComparison.Ordinal);
            bool isDeath = effect.Contains(Death, StringComparison.Ordinal);
            if (isDamage || isDeath)
            {
                NoteBoss(combat, source, byId, byName, false);
                NoteBoss(combat, target, byId, byName, isDeath);
            }
            // Some encounters signal victory through an ability instead of an NPC death.
            var abilityId = FirstId(ability);
            foreach (var attempt in combat.Bosses.Values)
                if (attempt.Boss.KillAbility != 0 && abilityId == attempt.Boss.KillAbility) attempt.AbilityVictory = true;
            if (localEvent && effect.Contains(ExitCombat, StringComparison.Ordinal)) exitedAt = timestamp;
        }
        cancellationToken.ThrowIfCancellationRequested();
        result.EndTime = lastTime ?? date;
        if (!lastTime.HasValue) result.StartTime = date;
        if (combat != null) CompleteCombat(result, combat, exitedAt ?? result.EndTime, !exitedAt.HasValue);
        result.Players = players.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
        return result;
    }

    private void BuildBossLookup(Area area, string difficulty, int groupSize, out Dictionary<long, Boss> ids,
        out Dictionary<string, Boss> names)
    {
        ids = new Dictionary<long, Boss>(_worldBosses);
        names = new Dictionary<string, Boss>(StringComparer.OrdinalIgnoreCase);
        if (area.Metadata == null) return;
        var metadata = area.Metadata;
        foreach (var rawName in metadata.BossNames.Concat(metadata.BossIds.Keys).Distinct())
        {
            var parts = rawName.Split("~?~", StringSplitOptions.None);
            var name = parts[0];
            var aliases = (parts.Length > 1 ? parts[1].Split('|') : new[] { name }).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var targetIds = new HashSet<long>();
            var requiredIds = new HashSet<long>();
            ulong killAbility = 0;
            string mode = difficulty + " " + groupSize;
            if (metadata.BossIds.TryGetValue(name, out var modes))
            {
                foreach (var id in modes.Values.SelectMany(v => v)) targetIds.Add(id);
                if (!modes.ContainsKey(mode) && groupSize == 4 && difficulty == "Story") mode = "Veteran 4";
                if (modes.ContainsKey("All")) mode = "All";
                var required = metadata.RequiredIdsForKill.TryGetValue(name, out var requiredModes) ? requiredModes : modes;
                if (required.TryGetValue(mode, out var needed)) requiredIds.UnionWith(needed);
                if (metadata.RequiredAbilitiesForKill?.TryGetValue(name, out var abilities) == true)
                    abilities.TryGetValue(mode, out killAbility);
            }
            var boss = new Boss(name, targetIds, requiredIds, aliases, killAbility);
            foreach (var id in targetIds) ids[id] = boss;
            foreach (var alias in aliases) names[alias] = boss;
        }
    }

    private static void NoteBoss(Combat combat, Actor actor, Dictionary<long, Boss> ids, Dictionary<string, Boss> names, bool dead)
    {
        if (actor.IsPlayer || actor.Name.Length == 0) return;
        if (!ids.TryGetValue(actor.Id, out var boss) && !names.TryGetValue(actor.Name, out boss)) return;
        if (!combat.Bosses.TryGetValue(boss.Name, out var attempt))
        {
            attempt = new Attempt(boss);
            combat.Bosses.Add(boss.Name, attempt);
        }
        if (dead)
        {
            attempt.DeadIds.Add(actor.Id);
            attempt.DeadNames.Add(actor.Name);
        }
    }

    private static void CompleteCombat(IndexedCombatLog log, Combat combat, DateTime end, bool inProgress)
    {
        var names = combat.Players.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
        if (combat.Bosses.Count == 0)
            log.Encounters.Add(Entry("", inProgress ? "In progress" : "Combat"));
        else
            foreach (var attempt in combat.Bosses.Values)
            {
                bool killed = attempt.AbilityVictory ||
                    (attempt.Boss.RequiredIds.Count > 0 && attempt.Boss.RequiredIds.IsSubsetOf(attempt.DeadIds)) ||
                    (attempt.Boss.Ids.Count == 0 && attempt.Boss.Aliases.Count > 0 && attempt.Boss.Aliases.IsSubsetOf(attempt.DeadNames));
                log.Encounters.Add(Entry(attempt.Boss.Name, killed ? "Kill" : inProgress ? "In progress" : "Attempt"));
            }
        IndexedEncounter Entry(string boss, string outcome) => new()
        {
            StartTime = combat.Start, EndTime = end < combat.Start ? combat.Start : end,
            EncounterName = combat.Area.Name, ActivityType = combat.Area.Activity,
            BossName = boss, Difficulty = combat.Difficulty, PlayerCount = combat.GroupSize,
            Players = names, Outcome = outcome
        };
    }

    private static void ReadArea(ReadOnlySpan<char> effect, out ulong id, out string name, out ulong difficulty)
    {
        int separator = effect.IndexOf(':');
        var location = separator >= 0 ? effect[(separator + 1)..].Trim() : ReadOnlySpan<char>.Empty;
        int brace = location.IndexOf('{');
        name = brace >= 0 ? location[..brace].Trim().ToString() : "Open World";
        id = FirstId(location);
        int close = location.IndexOf('}');
        difficulty = close >= 0 ? FirstId(location[(close + 1)..]) : 0;
    }

    private static ulong FirstId(ReadOnlySpan<char> field)
    {
        int open = field.IndexOf('{');
        if (open < 0) return 0;
        var rest = field[(open + 1)..];
        int close = rest.IndexOf('}');
        return close >= 0 && ulong.TryParse(rest[..close], out var value) ? value : 0;
    }

    private static ReadOnlySpan<char> NextField(ref ReadOnlySpan<char> input)
    {
        int start = input.IndexOf('[');
        if (start < 0) return ReadOnlySpan<char>.Empty;
        int depth = 1;
        for (int i = start + 1; i < input.Length; i++)
        {
            if (input[i] == '[') depth++;
            if (input[i] != ']') continue;
            depth--;
            if (depth != 0) continue;
            var value = input.Slice(start + 1, i - start - 1);
            input = input[(i + 1)..];
            return value;
        }
        return ReadOnlySpan<char>.Empty;
    }

    internal static DateTime FileStart(FileInfo file)
    {
        var name = file.Name;
        if (name.StartsWith("combat_", StringComparison.OrdinalIgnoreCase) && name.Length >= 26 &&
            DateTime.TryParseExact(name.AsSpan(7, 19), "yyyy-MM-dd_HH_mm_ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start))
            return start;
        if (name.StartsWith("combat_", StringComparison.OrdinalIgnoreCase) && name.Length >= 17 &&
            DateTime.TryParseExact(name.AsSpan(7, 10), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;
        return file.LastWriteTime.Date;
    }

    private sealed record Area(string Name, string Activity, EncounterInfo? Metadata);
    private sealed record Boss(string Name, HashSet<long> Ids, HashSet<long> RequiredIds, HashSet<string> Aliases, ulong KillAbility);
    private sealed class Attempt(Boss boss)
    {
        public Boss Boss { get; } = boss;
        public HashSet<long> DeadIds { get; } = new();
        public HashSet<string> DeadNames { get; } = new(StringComparer.OrdinalIgnoreCase);
        public bool AbilityVictory { get; set; }
    }
    private sealed class Combat(DateTime start, Area area, string difficulty, int groupSize)
    {
        public DateTime Start { get; } = start;
        public Area Area { get; } = area;
        public string Difficulty { get; } = difficulty;
        public int GroupSize { get; } = groupSize;
        public HashSet<string> Players { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, Attempt> Bosses { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
    private readonly record struct Actor(string Name, long Id, bool IsPlayer)
    {
        public static Actor Read(ReadOnlySpan<char> field)
        {
            field = field.Trim();
            if (field.IsEmpty) return new Actor("", 0, false);
            if (field[0] == '@')
            {
                // Companions use @Owner#id/Companion {id}:instance; their combat events are not the owner's.
                int slash = field.IndexOf('/');
                int position = field.IndexOf('|');
                if (slash >= 0 && (position < 0 || slash < position)) field = field[(slash + 1)..];
                else
                {
                    int end = field.IndexOfAny('#', '|', ':');
                    return new Actor((end < 0 ? field[1..] : field.Slice(1, end - 1)).ToString(), 0, true);
                }
            }
            int brace = field.IndexOf('{');
            return brace < 0 ? new Actor("", 0, false) : new Actor(field[..brace].Trim().ToString(), (long)FirstId(field), false);
        }
    }

    /// <summary>Freeze the scan boundary even while SWTOR appends to the same open file.</summary>
    private sealed class BoundedReadStream(Stream inner, long remaining) : Stream
    {
        private long _remaining = remaining;
        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = inner.Read(buffer, offset, (int)Math.Min(count, _remaining));
            _remaining -= read;
            return read;
        }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
