using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using SWTORCombatParser.Utilities;

namespace SWTORCombatParser.Model.Overlays;

public static class RaidFrameNameMatcher
{
    public static string Normalize(string name) => string.IsNullOrWhiteSpace(name) ? "" :
        new string(name.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark &&
                        c != '\'' && c != '\u2019' && c != '-' && !char.IsWhiteSpace(c)).ToArray()).ToLowerInvariant();

    // An ambiguous guess must never put another player's timers on a frame.
    public static string? FindBestMatch(string name, IEnumerable<string> candidates, int maximumDistance = 3)
    {
        var normalized = Normalize(name);
        if (normalized.Length == 0 || maximumDistance < 0) return null;
        var distinctCandidates = candidates.Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        // Accents and punctuation can distinguish real players. Preserve an exact identity
        // before relaxing the comparison for OCR (for example Aria and Ária in one raid).
        var exact = distinctCandidates.FirstOrDefault(c => string.Equals(c, name, StringComparison.OrdinalIgnoreCase));
        if (exact != null) return exact;
        var matches = distinctCandidates
            .Select(c => new { Name = c, Distance = LevenshteinDistance.Compute(Normalize(c), normalized) })
            .OrderBy(c => c.Distance).Take(2).ToList();
        if (matches.Count == 0 || matches[0].Distance > maximumDistance ||
            (matches.Count > 1 && matches[0].Distance == matches[1].Distance)) return null;
        return matches[0].Name;
    }
}
