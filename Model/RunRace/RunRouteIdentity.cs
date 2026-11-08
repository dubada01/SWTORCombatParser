using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Orbs.RunRace;

// Shared verbatim with orbs-api. Hashes identify observed sequences, not catalog checklists.
public static class RunRouteIdentity
{
    public static string GroupSize(string value) => value.Replace(" Player", "", StringComparison.OrdinalIgnoreCase).Trim();
    public static string Key(IEnumerable<string> bosses) => Hash(string.Join("\u001f", bosses));
    public static string Visit(string player, string encounter, string difficulty, string size, DateTime entered) =>
        Hash(string.Join("\u001f", player.ToUpperInvariant(), encounter, difficulty, GroupSize(size), entered.Ticks));
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}

public sealed record RunRouteDefinition(string Encounter, bool RequireFirstBoss, string CompletionBoss, string[] Bosses)
{
    public bool IsComplete(IEnumerable<string> killed)
    {
        var names = killed.ToArray();
        return names.Length > 0 && names[^1] == CompletionBoss && (!RequireFirstBoss || names[0] == Bosses[0]);
    }
}
