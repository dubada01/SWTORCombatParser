using System;
using NUnit.Framework;
using SWTORCombatParser.Model.Overlays;

namespace SWTORCombatParser_Test;

public class RaidFrameNameMatcherTests
{
    [TestCase(null, "")]
    [TestCase(" \t\n", "")]
    [TestCase("\u00c9l'ana-Sky Walker", "elanaskywalker")]
    [TestCase("E\u0301lana", "elana")]
    [TestCase("SKY WALKER", "skywalker")]
    [TestCase("O\u2019Rian", "orian")]
    public void NormalizationHandlesOcrFormatting(string input, string expected)
    {
        Assert.That(RaidFrameNameMatcher.Normalize(input), Is.EqualTo(expected));
    }

    [TestCase("Aria", "\u00c1ria", "Aria")]
    [TestCase("\u00c1ria", "Aria", "\u00c1ria")]
    [TestCase("O'Rian", "Orian", "O'Rian")]
    [TestCase("Orian", "O'Rian", "Orian")]
    public void ExactIdentityWinsWhenDistinctPlayersNormalizeToTheSameText(string query, string other, string expected)
    {
        Assert.That(RaidFrameNameMatcher.FindBestMatch(query, new[] { other, expected }), Is.EqualTo(expected));
        Assert.That(RaidFrameNameMatcher.FindBestMatch(query, new[] { expected, other }), Is.EqualTo(expected));
    }

    [Test]
    public void AmbiguousNormalizedIdentityCannotChooseOnePlayersTimers()
    {
        Assert.That(RaidFrameNameMatcher.FindBestMatch("A-ria", new[] { "Aria", "\u00c1ria" }), Is.Null);
    }

    [Test]
    public void EquallyCloseFuzzyMatchesAreRejectedEvenWithRelaxedAccuracy()
    {
        Assert.That(RaidFrameNameMatcher.FindBestMatch("Mira", new[] { "Kira", "Tira" }), Is.Null);
        Assert.That(RaidFrameNameMatcher.FindBestMatch("Mira", new[] { "Kira", "Tira" }, int.MaxValue), Is.Null);
    }

    [Test]
    public void UniqueCloseOcrMatchKeepsOriginalPlayerName()
    {
        Assert.That(RaidFrameNameMatcher.FindBestMatch("Elaana", new[] { "\u00c9lana", "SomeoneElse" }, 1),
            Is.EqualTo("\u00c9lana"));
    }

    [Test]
    public void DistanceLimitCanRequireAnExactNormalizedMatch()
    {
        Assert.That(RaidFrameNameMatcher.FindBestMatch("Elana", new[] { "\u00c9lana" }, 0), Is.EqualTo("\u00c9lana"));
        Assert.That(RaidFrameNameMatcher.FindBestMatch("Elaana", new[] { "\u00c9lana" }, 0), Is.Null);
    }

    [Test]
    public void DuplicateCaseVariantsDoNotCreateFalseAmbiguity()
    {
        Assert.That(RaidFrameNameMatcher.FindBestMatch("Aria", new[] { "ARIA", "aria", "Aria" }),
            Is.EqualTo("ARIA"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  ")]
    public void EmptyPlayerNameHasNoMatch(string query)
    {
        Assert.That(RaidFrameNameMatcher.FindBestMatch(query, new[] { "Aria" }), Is.Null);
    }

    [Test]
    public void MissingAndEmptyCandidatesHaveNoMatch()
    {
        Assert.That(RaidFrameNameMatcher.FindBestMatch("Aria", Array.Empty<string>()), Is.Null);
        Assert.That(RaidFrameNameMatcher.FindBestMatch("Aria", new string[] { null, "", " " }), Is.Null);
    }
}
