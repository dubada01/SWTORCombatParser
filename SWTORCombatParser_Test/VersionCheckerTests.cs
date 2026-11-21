using System;
using NUnit.Framework;
using SWTORCombatParser.Model.CloudRaiding;

namespace SWTORCombatParser_Test;

public class VersionCheckerTests
{
    [TestCase("2.4.2", "2.4.0", false)]
    [TestCase("2.4.0", "2.4.1", false)]
    [TestCase("2.4.1.0", "2.4.2.0", false)]
    [TestCase("2.4.2.0", "2.4.0.0", false)]
    [TestCase("2.5.0", "2.4.2.0", true)]
    [TestCase("3.0.0", "2.99.2", true)]
    [TestCase("2.3.2", "2.4.0", false)]
    [TestCase("1.99.2", "2.0.0", false)]
    [TestCase("0.0", "2.4.0", false)]
    public void UpdateChecksCompareReleaseWithoutPlatformOrPadding(string available, string current, bool expected)
    {
        Assert.That(VersionChecker.IsNewerRelease(new Version(available), new Version(current)), Is.EqualTo(expected));
    }
}
