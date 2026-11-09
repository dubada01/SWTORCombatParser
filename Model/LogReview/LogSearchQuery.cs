using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;

namespace SWTORCombatParser.Model.LogReview;

/// <summary>Compiled once per search; matching does not allocate a list of fields per event.</summary>
public sealed class LogSearchQuery
{
    private readonly List<List<Func<ParsedLogEntry, double, bool>>> _groups = new();
    private readonly Regex? _regex;

    public LogSearchQuery(string text, bool regex = false)
    {
        if (regex && !string.IsNullOrWhiteSpace(text))
        {
            try { _regex = new Regex(text, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(20)); }
            catch (ArgumentException) { throw new FormatException("Invalid regular expression. Check brackets, parentheses and escapes."); }
            return;
        }
        var group = new List<Func<ParsedLogEntry, double, bool>>();
        _groups.Add(group);
        foreach (var token in Tokens(text ?? ""))
        {
            if (token.Alternative)
            {
                if (group.Count == 0) throw new FormatException("Add a search term before |.");
                group = new(); _groups.Add(group); continue;
            }
            var exclude = token.Text.StartsWith('-') && token.Text.Length > 1;
            var term = exclude ? token.Text[1..] : token.Text;
            var predicate = Compile(term, token.Colon - (exclude ? 1 : 0));
            group.Add(exclude ? (log, seconds) => !predicate(log, seconds) : predicate);
        }
        if (_groups.Count > 1 && group.Count == 0) throw new FormatException("Add a search term after |.");
    }

    public bool Matches(ParsedLogEntry log, double seconds)
    {
        if (_regex != null)
            return MatchFields(log, s => _regex.IsMatch(s));
        foreach (var group in _groups)
        {
            var matches = true;
            foreach (var term in group)
                if (!term(log, seconds)) { matches = false; break; }
            if (matches) return true;
        }
        return false;
    }

    public static string Quote(string text) => "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    private static IEnumerable<(string Text, int Colon, bool Alternative)> Tokens(string text)
    {
        var token = new StringBuilder();
        var quoted = false;
        var colon = -1;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (quoted && ch == '\\' && i + 1 < text.Length && text[i + 1] is '"' or '\\') { token.Append(text[++i]); continue; }
            if (ch == '"') { quoted = !quoted; continue; }
            if (!quoted && (char.IsWhiteSpace(ch) || ch == '|'))
            {
                if (token.Length > 0) { yield return (token.ToString(), colon, false); token.Clear(); colon = -1; }
                if (ch == '|') yield return ("|", -1, true);
            }
            else { if (!quoted && ch == ':' && colon < 0) colon = token.Length; token.Append(ch); }
        }
        if (quoted) throw new FormatException("Close the quoted phrase with a double quote.");
        if (token.Length > 0) yield return (token.ToString(), colon, false);
    }

    private static Func<ParsedLogEntry, double, bool> Compile(string term, int colon)
    {
        var field = colon > 0 ? term[..colon].ToLowerInvariant() : "";
        var value = colon > 0 ? term[(colon + 1)..] : term;
        if (value.Length == 0) throw new FormatException("Enter a value after " + field + ":.");
        switch (field)
        {
            case "source": return (l, _) => Contains(l.Source.Name, value) || IdMatches(l.Source.LogId, value);
            case "target": return (l, _) => Contains(l.Target.Name, value) || IdMatches(l.Target.LogId, value);
            case "ability": return (l, _) => Contains(l.Ability, value) || IdMatches(l.AbilityId, value);
            case "effect": return (l, _) => Contains(l.Effect.EffectName, value) || IdMatches(l.Effect.EffectId, value);
            case "type": return (l, _) => Contains(Kind(l), value) || Contains(l.Effect.EffectType.ToString(), value) || Contains(l.Value.ValueType.ToString(), value);
            case "crit":
                if (!bool.TryParse(value, out var crit)) throw new FormatException("Use crit:true or crit:false.");
                return (l, _) => l.Value.WasCrit == crit;
            case "after":
            case "before":
                if (!TrySeconds(value, out var time)) throw new FormatException("Use seconds or mm:ss.fff after " + field + ":.");
                return field == "after" ? (_, seconds) => seconds >= time : (_, seconds) => seconds <= time;
            case "value":
                var op = value.StartsWith(">=") || value.StartsWith("<=") ? value[..2] : value[0] is '>' or '<' or '=' ? value[..1] : "=";
                var number = value[0] is '>' or '<' or '=' ? value[op.Length..] : value;
                if (!double.TryParse(number, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var amount) || !double.IsFinite(amount))
                    throw new FormatException("Use a number, for example value:>=10000 (effective amount).");
                return (l, _) => op switch { ">=" => l.Value.EffectiveDblValue >= amount, "<=" => l.Value.EffectiveDblValue <= amount,
                    ">" => l.Value.EffectiveDblValue > amount, "<" => l.Value.EffectiveDblValue < amount, _ => l.Value.EffectiveDblValue == amount };
            case "":
                var numeric = ulong.TryParse(value, out _);
                return (l, _) => Contains(l.Source.Name, value) || Contains(l.Target.Name, value) || Contains(l.Ability, value) ||
                    Contains(l.Effect.EffectName, value) || Contains(Kind(l), value) || Contains(l.Value.ValueType.ToString(), value) ||
                    (numeric && (IdMatches(l.AbilityId, value) || IdMatches(l.Effect.EffectId, value) || IdMatches(l.Source.LogId, value) || IdMatches(l.Target.LogId, value)));
            default: throw new FormatException("Unknown field '" + field + "'. Use source, target, ability, effect, type, value, crit, after or before.");
        }
    }

    public static bool TrySeconds(string value, out double seconds)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds)) return double.IsFinite(seconds);
        var parts = value.Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var minutes) ||
            !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var remainder) || remainder < 0 || remainder >= 60) return false;
        seconds = minutes * 60 + (value.StartsWith('-') ? -remainder : remainder);
        return double.IsFinite(seconds);
    }

    public static string Kind(ParsedLogEntry log)
    {
        var id = log.Effect.EffectId;
        if (id == _7_0LogParsing.DeathCombatId) return "Death";
        if (id == _7_0LogParsing.RevivedCombatId) return "Revive";
        if (id == _7_0LogParsing._damageEffectId) return "Damage";
        if (id == _7_0LogParsing._healEffectId) return "Healing";
        if (id == _7_0LogParsing.AbilityActivateId) return "Activation";
        return log.Effect.EffectType.ToString();
    }

    private static bool Contains(string? text, string value) => text?.Contains(value, StringComparison.OrdinalIgnoreCase) == true;
    private static bool IdMatches<T>(T id, string text) where T : IFormattable => id.ToString(null, CultureInfo.InvariantCulture) == text;
    private static bool MatchFields(ParsedLogEntry l, Func<string, bool> match) =>
        match(l.Source.Name ?? "") || match(l.Target.Name ?? "") || match(l.Ability ?? "") || match(l.Effect.EffectName ?? "") ||
        match(Kind(l)) || match(l.Value.ValueType.ToString()) || match(l.AbilityId.ToString(CultureInfo.InvariantCulture)) ||
        match(l.Effect.EffectId.ToString(CultureInfo.InvariantCulture)) || match(l.Source.LogId.ToString(CultureInfo.InvariantCulture)) ||
        match(l.Target.LogId.ToString(CultureInfo.InvariantCulture));
}
