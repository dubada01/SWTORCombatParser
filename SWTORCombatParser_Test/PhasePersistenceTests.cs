using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using NUnit.Framework;
using SWTORCombatParser.DataStructures.Phases;
using SWTORCombatParser.Model.Phases;

namespace SWTORCombatParser_Test;

[NonParallelizable]
public class PhasePersistenceTests
{
    private string _directory = null!;
    private string _path = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "OrbsPhasePersistenceTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _path = Path.Combine(_directory, "phase_info.json");
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_directory, recursive: true);

    [TestCase("")]
    [TestCase(" \n\t")]
    [TestCase("null")]
    [TestCase("[{\"Id\":")]
    [TestCase("{}")]
    public void DamagedDocumentIsBackedUpAndRecoversWithoutThrowing(string original)
    {
        File.WriteAllText(_path, original);
        Assert.That(DefaultPhaseManager.ReadPhases(_path), Is.Empty);
        Assert.Multiple(() =>
        {
            Assert.That(File.ReadAllText(Directory.GetFiles(_directory, "*.bak").Single()), Is.EqualTo(original));
            Assert.That(File.ReadAllText(_path), Is.EqualTo("[]"));
            Assert.That(DefaultPhaseManager.ReadPhases(_path), Is.Empty);
            Assert.That(Directory.GetFiles(_directory, "*.bak"), Has.Length.EqualTo(1));
        });
    }

    [Test]
    public void InvalidEntriesDoNotDiscardValidCustomPhases()
    {
        var custom = Definition("Custom phase");
        var original = "[null,{\"Name\":\"No source\"},{\"Id\":\"not-a-guid\"}," + JsonConvert.SerializeObject(custom) + "]";
        File.WriteAllText(_path, original);
        var restored = DefaultPhaseManager.ReadPhases(_path).Single();
        Assert.Multiple(() =>
        {
            Assert.That(restored.Id, Is.EqualTo(custom.Id));
            Assert.That(restored.SourcePt2, Is.EqualTo("Boss"));
            Assert.That(restored.StartArgs.CombatDuration, Is.EqualTo(17));
            Assert.That(File.ReadAllText(Directory.GetFiles(_directory, "*.bak").Single()), Is.EqualTo(original));
            Assert.That(DefaultPhaseManager.ReadPhases(_path).Single().Id, Is.EqualTo(custom.Id));
        });
    }

    [Test]
    public void WholeEncounterSourceIsSupported()
    {
        var phase = Definition("Encounter phase");
        phase.PhaseSource = "Encounter";
        DefaultPhaseManager.WritePhases(_path, new[] { phase });
        Assert.That(DefaultPhaseManager.ReadPhases(_path).Single().SourcePt2, Is.EqualTo("Encounter"));
        Assert.That(Directory.GetFiles(_directory, "*.bak"), Is.Empty);
    }

    [Test]
    public void FailedSaveLeavesTheExistingDocumentIntact()
    {
        DefaultPhaseManager.WritePhases(_path, new[] { Definition("Saved") });
        var original = File.ReadAllBytes(_path);
        Assert.Throws<InvalidOperationException>(() => DefaultPhaseManager.WritePhases(_path, InterruptedUpdate()));
        Assert.That(File.ReadAllBytes(_path), Is.EqualTo(original));
        Assert.That(Directory.GetFiles(_directory, "*.tmp"), Is.Empty);
    }

    [Test]
    public void BuiltinRefreshRecoversTheReportedEmptyFileAndPreservesCustomDefinitions()
    {
        var phaseField = typeof(DefaultPhaseManager).GetField("_phaseFile", BindingFlags.Static | BindingFlags.NonPublic)!;
        var originalPath = phaseField.GetValue(null);
        phaseField.SetValue(null, _path);
        try
        {
            File.WriteAllText(_path, string.Empty);
            DefaultPhaseLoader.LoadBuiltinPhases();
            var builtins = DefaultPhaseManager.GetExisitingPhases().ToArray();
            Assert.That(builtins, Is.Not.Empty, "Startup must restore the packaged phases after the first crash.");
            var custom = Definition("User-created phase");
            DefaultPhaseManager.AddOrUpdatePhase(custom);
            DefaultPhaseLoader.LoadBuiltinPhases();
            var afterReload = DefaultPhaseManager.GetExisitingPhases().ToArray();
            Assert.That(afterReload, Has.Length.EqualTo(builtins.Length + 1));
            Assert.That(afterReload.Single(p => p.Id == custom.Id).Name, Is.EqualTo(custom.Name));
            Assert.That(Directory.GetFiles(_directory, "*.tmp"), Is.Empty);
            Assert.That(Directory.GetFiles(_directory, "*.bak"), Has.Length.EqualTo(1));
        }
        finally { phaseField.SetValue(null, originalPath); }
    }

    private static IEnumerable<Phase> InterruptedUpdate()
    {
        yield return Definition("Uncommitted");
        throw new InvalidOperationException("Interrupted while producing an update.");
    }

    private static Phase Definition(string name) => new()
    {
        Id = Guid.NewGuid(), Name = name, PhaseSource = "Encounter|Boss",
        StartTrigger = PhaseTrigger.CombatDuration, StartArgs = new PhaseArgs { CombatDuration = 17 },
        EndTrigger = PhaseTrigger.CombatDuration, EndArgs = new PhaseArgs { CombatDuration = 30 }
    };
}
