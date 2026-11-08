using System;
using System.Collections.Generic;
using NUnit.Framework;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.CloudRaiding;

namespace SWTORCombatParser_Test;

public class EncounterTimeTrialRecorderTests
{
    private static readonly DateTime Entered = new(2026, 11, 7, 12, 0, 0);
    private static EncounterInfo Encounter(string difficulty = "Story", string size = "8") => new()
    {
        Name = "Operation", Difficutly = difficulty, NumberOfPlayer = size,
        BossInfos = new List<BossInfo> { new() { EncounterName = "First" }, new() { EncounterName = "Last" } }
    };

    [Test]
    public void OperationRequiresFirstBossAndKeepsWipeTimeInLaterSplit()
    {
        var recorder = new EncounterTimeTrialRecorder();
        var encounter = Encounter();
        Assert.That(recorder.CreateEntry(encounter, "Last", true, Entered, Entered.AddMinutes(1), Entered.AddMinutes(2), "Player"), Is.Null);
        Assert.That(recorder.CreateEntry(encounter, "First", false, Entered, Entered.AddMinutes(2), Entered.AddMinutes(3), "Player"), Is.Null);
        var entry = recorder.CreateEntry(encounter, "First", true, Entered, Entered.AddMinutes(4), Entered.AddMinutes(5), "Player");
        Assert.That(entry!.StartSeconds, Is.EqualTo(240));
        Assert.That(entry.EndSeconds, Is.EqualTo(300));
        Assert.That(entry.EnteredAt, Is.EqualTo(Entered));
        Assert.That(entry.RunId, Has.Length.EqualTo(64));
        var nextBoss = recorder.CreateEntry(encounter, "Last", true, Entered, Entered.AddMinutes(6), Entered.AddMinutes(7), "Player");
        Assert.That(nextBoss!.RunId, Is.EqualTo(entry.RunId), "Uploads from the same visit must share a stable identity.");
        Assert.That(recorder.CreateEntry(encounter, "First", true, Entered, Entered.AddMinutes(4), Entered.AddMinutes(5), "Player"), Is.Null);
        Assert.That(recorder.CreateEntry(encounter, "Last", true, Entered.AddHours(1), Entered.AddHours(1).AddMinutes(1), Entered.AddHours(1).AddMinutes(2), "Player"), Is.Null);
    }

    [TestCase("Veteran", "8", false)]
    [TestCase("Master", "16", false)]
    [TestCase("Master", "4", true)]
    [TestCase("Veteran", "4", true)]
    public void RetainsTimeTrialCohortRules(string difficulty, string size, bool expected)
    {
        var entry = new EncounterTimeTrialRecorder().CreateEntry(Encounter(difficulty, size), "Last", true,
            Entered, Entered.AddMinutes(1), Entered.AddMinutes(2), "Player");
        Assert.That(entry != null, Is.EqualTo(expected));
    }

    [Test]
    public void MissingOrFutureEntryTimeCannotCreateNegativeOrAncientSplits()
    {
        var recorder = new EncounterTimeTrialRecorder();
        Assert.That(recorder.CreateEntry(Encounter(), "First", true, DateTime.MinValue, Entered, Entered.AddSeconds(10), "Player"), Is.Null);
        Assert.That(recorder.CreateEntry(Encounter(), "First", true, Entered.AddSeconds(1), Entered, Entered.AddSeconds(10), "Player"), Is.Null);
    }
}
