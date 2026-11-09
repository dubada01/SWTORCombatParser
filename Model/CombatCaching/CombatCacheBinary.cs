using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.ClassInfos;

namespace SWTORCombatParser.Model.CombatCaching;

// Disposable, versioned cache, not a saved-session format. Fixed-width event
// records avoid parsing millions of JSON property names and duplicate strings.
// Rare entity/class definitions keep their normal JSON contract; each file owns
// its reference tables and strings (nothing is interned for the process lifetime).
internal static class CombatCacheBinary
{
    private const ulong Header = 0x344548434143524FUL; // ORCACHE4
    private const int Footer = 0x454E4421;
    private static readonly JsonSerializerOptions Definitions = new() { IncludeFields = true, IgnoreReadOnlyProperties = true };

    internal static void Write(Stream stream, Guid owner, IReadOnlyList<ParsedLogEntry> logs)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(Header); writer.Write(owner.ToByteArray()); writer.Write(logs.Count);
        var fields = new Writer(writer);
        foreach (var log in logs) fields.Log(log);
        writer.Write(Footer);
    }

    internal static List<ParsedLogEntry> Read(Stream stream, Guid owner, System.Threading.CancellationToken token = default)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        if (reader.ReadUInt64() != Header || new Guid(ReadBytes(reader, 16)) != owner)
            throw new InvalidDataException("The combat cache does not belong to the current parsed log state. Reload the source log.");
        var count = reader.ReadInt32();
        if (count <= 0 || count > 10_000_000) throw new InvalidDataException("Invalid cached event count.");
        // Do not reserve an arbitrarily large array before verifying the records.
        var logs = new List<ParsedLogEntry>(Math.Min(count, 65536));
        var fields = new Reader(reader);
        for (var index = 0; index < count; index++)
        {
            if ((index & 1023) == 0) token.ThrowIfCancellationRequested();
            logs.Add(fields.Log());
        }
        if (reader.ReadInt32() != Footer || reader.BaseStream.ReadByte() != -1)
            throw new InvalidDataException("The combat cache is incomplete or contains unexpected data.");
        return logs;
    }

    private static byte[] ReadBytes(BinaryReader reader, int count)
    {
        var bytes = reader.ReadBytes(count);
        if (bytes.Length != count) throw new EndOfStreamException("The combat cache is incomplete.");
        return bytes;
    }

    private sealed class Writer(BinaryWriter writer)
    {
        private readonly Dictionary<string, int> _strings = new(StringComparer.Ordinal);
        private readonly Dictionary<Entity, int> _entities = new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<SWTORClass, int> _classes = new(ReferenceEqualityComparer.Instance);

        private void Text(string? text)
        {
            if (text == null) { writer.Write(-1); return; }
            if (_strings.TryGetValue(text, out var index)) { writer.Write(index); return; }
            writer.Write(-_strings.Count - 2);
            _strings.Add(text, _strings.Count);
            var bytes = Encoding.UTF8.GetBytes(text);
            writer.Write(bytes.Length); writer.Write(bytes);
        }

        private void Definition<T>(T? value, Dictionary<T, int> references) where T : class
        {
            if (value == null) { writer.Write(-1); return; }
            if (references.TryGetValue(value, out var index)) { writer.Write(index); return; }
            writer.Write(-references.Count - 2); references.Add(value, references.Count);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value, Definitions);
            writer.Write(bytes.Length); writer.Write(bytes);
        }

        private void Info(EntityInfo info)
        {
            Definition(info.Entity, _entities); Definition(info.Class, _classes);
            writer.Write(info.MaxHP); writer.Write(info.CurrentHP); writer.Write(info.IsAlive);
            writer.Write(info.Position != null);
            if (info.Position == null) return;
            writer.Write(info.Position.X); writer.Write(info.Position.Y);
            writer.Write(info.Position.Z); writer.Write(info.Position.Facing);
        }

        private void Amount(Value? value, int depth = 0)
        {
            if (depth > 8) throw new InvalidDataException("Invalid nested combat value.");
            writer.Write(value != null);
            if (value == null) return;
            writer.Write(value.DblValue); writer.Write(value.EffectiveDblValue); writer.Write(value.MitigatedDblValue);
            Text(value.StrValue); Text(value.DisplayValue); Text(value.ModifierDisplayValue); Text(value.ModifierType);
            writer.Write((int)value.ValueType); writer.Write(value.ValueTypeId); writer.Write(value.WasCrit);
            Amount(value.Modifier, depth + 1);
        }

        internal void Log(ParsedLogEntry log)
        {
            writer.Write((int)log.Error); writer.Write(log.RaidLogId); Text(log.LogName); writer.Write(log.LogBytes);
            writer.Write(log.LogLineNumber); Text(log.LogLocation); writer.Write(log.LogLocationId); writer.Write(log.LogDifficultyId);
            writer.Write(log.TimeStamp.ToBinary()); writer.Write(log.SecondsSinceCombatStart);
            Info(log.SourceInfo); Info(log.TargetInfo); Text(log.Ability); writer.Write(log.AbilityId);
            writer.Write((int)log.Effect.EffectType); Text(log.Effect.EffectName);
            writer.Write(log.Effect.EffectId); writer.Write(log.Effect.SecondEffectId);
            Amount(log.Value); writer.Write(log.Threat);
        }
    }

    private sealed class Reader(BinaryReader reader)
    {
        private readonly List<string> _strings = new();
        private readonly List<Entity> _entities = new();
        private readonly List<SWTORClass> _classes = new();

        private byte[] Payload()
        {
            var count = reader.ReadInt32();
            if (count < 0 || count > 16 * 1024 * 1024) throw new InvalidDataException("Invalid cached definition length.");
            return ReadBytes(reader, count);
        }

        private int ReferenceIndex(int count)
        {
            var index = reader.ReadInt32();
            if (index == -1 || index >= 0 && index < count) return index;
            if (index != -count - 2) throw new InvalidDataException("Invalid combat cache reference.");
            return -2;
        }
        private string? Text()
        {
            var index = ReferenceIndex(_strings.Count);
            if (index == -1) return null;
            if (index >= 0) return _strings[index];
            var value = Encoding.UTF8.GetString(Payload()); _strings.Add(value); return value;
        }
        private T? Definition<T>(List<T> values) where T : class
        {
            var index = ReferenceIndex(values.Count);
            if (index == -1) return null;
            if (index >= 0) return values[index];
            var value = JsonSerializer.Deserialize<T>(Payload(), Definitions) ?? throw new InvalidDataException("Missing cached definition.");
            values.Add(value); return value;
        }

        private EntityInfo Info()
        {
            var entity = Definition(_entities) ?? throw new InvalidDataException("Missing cached entity.");
            var info = new EntityInfo { Entity = entity, Class = Definition(_classes), MaxHP = reader.ReadUInt32(),
                CurrentHP = reader.ReadUInt32(), IsAlive = reader.ReadBoolean() };
            info.Position = reader.ReadBoolean() ? new PositionData { X = reader.ReadSingle(), Y = reader.ReadSingle(),
                Z = reader.ReadSingle(), Facing = reader.ReadSingle() } : null;
            return info;
        }

        private Value? Amount(int depth = 0)
        {
            if (depth > 8) throw new InvalidDataException("Invalid nested combat value.");
            if (!reader.ReadBoolean()) return null;
            var value = new Value { DblValue = reader.ReadDouble(), EffectiveDblValue = reader.ReadDouble(),
                MitigatedDblValue = reader.ReadDouble(), StrValue = Text(), DisplayValue = Text(),
                ModifierDisplayValue = Text(), ModifierType = Text(), ValueType = (DamageType)reader.ReadInt32(),
                ValueTypeId = reader.ReadInt64(), WasCrit = reader.ReadBoolean() };
            value.Modifier = Amount(depth + 1); return value;
        }

        internal ParsedLogEntry Log() => new()
        {
            Error = (ErrorType)reader.ReadInt32(), RaidLogId = reader.ReadInt64(), LogName = Text(), LogBytes = reader.ReadInt32(),
            LogLineNumber = reader.ReadInt64(), LogLocation = Text(), LogLocationId = reader.ReadUInt64(), LogDifficultyId = reader.ReadUInt64(),
            TimeStamp = DateTime.FromBinary(reader.ReadInt64()), SecondsSinceCombatStart = reader.ReadDouble(),
            SourceInfo = Info(), TargetInfo = Info(), Ability = Text(), AbilityId = reader.ReadUInt64(),
            Effect = new Effect { EffectType = (EffectType)reader.ReadInt32(), EffectName = Text(),
                EffectId = reader.ReadUInt64(), SecondEffectId = reader.ReadUInt64() },
            Value = Amount() ?? throw new InvalidDataException("Missing cached value."), Threat = reader.ReadDouble()
        };
    }
}
