using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using NUnit.Framework;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.Timers;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.Timers;
using Timer = SWTORCombatParser.DataStructures.Timer;

namespace SWTORCombatParser_Test;

[TestFixture, NonParallelizable]
public class TimerEditorValidationTests
{
    private readonly Dictionary<FieldInfo, object> _originalFields = new();
    private List<EncounterInfo> _originalEncounters;
    private string _directory;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "orbs-timer-editor-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        Redirect(typeof(Settings), "_appDataPath", _directory);
        Redirect(typeof(Settings), "_settingsPath", Path.Combine(_directory, "settings.json"));
        Redirect(typeof(DefaultOrbsTimersManager), "appDataPath", _directory);
        Redirect(typeof(DefaultOrbsTimersManager), "infoPath", Path.Combine(_directory, "timers.json"));
        Redirect(typeof(DefaultOrbsTimersManager), "activePath", Path.Combine(_directory, "active.json"));
        Redirect(typeof(DefaultOrbsTimersManager), "_currentDefaults", "");
        Redirect(typeof(DefaultOrbsTimersManager), "_defaults", null);
        Redirect(typeof(OrbsVariableManager), "CustomVariables", new Dictionary<string, int>());
        File.WriteAllText(Path.Combine(_directory, "settings.json"), "{\"offline_mode\":true,\"custom_audio_paths\":[]}");
        File.WriteAllText(Path.Combine(_directory, "timers.json"), JsonConvert.SerializeObject(new[]
        {
            new DefaultTimersData { TimerSource = "Shared" }
        }));
        File.WriteAllText(Path.Combine(_directory, "active.json"), "{}");
        _originalEncounters = EncounterLoader.SupportedEncounters;
        EncounterLoader.SupportedEncounters = new List<EncounterInfo>
        {
            new() { Name = "Open World", EncounterType = EncounterType.OpenWorld }
        };
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var (field, value) in _originalFields) field.SetValue(null, value);
        _originalFields.Clear();
        EncounterLoader.SupportedEncounters = _originalEncounters;
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    [Test]
    public void DuplicateNameIsPreservedAndDoesNotSubmitTimer()
    {
        var vm = new ModifyTimerViewModel("Shared") { Name = "Existing", DurationSec = 10 };
        vm.AvailableTimersForCharacter.Add(new Timer { Name = "Existing" });
        var submitted = 0;
        vm.OnNewTimer += (_, _, _) => submitted++;

        Save(vm);

        Assert.Multiple(() =>
        {
            Assert.That(vm.Name, Is.EqualTo("Existing"));
            Assert.That(vm.ValidationMessage, Is.EqualTo("A timer with this name already exists."));
            Assert.That(submitted, Is.Zero);
        });
    }

    [Test]
    public void ValidationAppearsAfterFailureAndClearsAfterSuccessfulRetry()
    {
        var vm = new ModifyTimerViewModel("Shared") { DurationSec = 10 };
        Timer submitted = null;
        vm.OnNewTimer += (timer, _, _) => submitted = timer;
        Assert.That(vm.ValidationMessage, Is.Empty);

        Save(vm);
        Assert.That(vm.ValidationMessage, Is.EqualTo("Enter a timer name."));
        Assert.That(submitted, Is.Null);
        vm.Name = "Opening burst";
        Save(vm);

        Assert.Multiple(() =>
        {
            Assert.That(vm.ValidationMessage, Is.Empty);
            Assert.That(submitted, Is.Not.Null);
            Assert.That(submitted.Name, Is.EqualTo("Opening burst"));
            Assert.That(submitted.DurationSec, Is.EqualTo(10));
        });
    }

    [Test]
    public void IncompleteTriggerExplainsFailureWithoutSubmitting()
    {
        var vm = new ModifyTimerViewModel("Shared") { Name = "Ability timer", SelectedTriggerType = TimerKeyType.AbilityUsed };
        var submitted = 0;
        vm.OnNewTimer += (_, _, _) => submitted++;

        Save(vm);

        Assert.That(vm.ValidationMessage, Is.EqualTo("Complete the required trigger settings."));
        Assert.That(submitted, Is.Zero);
    }

    [Test]
    public void CancellingNewTimerDoesNotReportAnEditedTimer()
    {
        var vm = new ModifyTimerViewModel("Shared");
        var cancelled = 0;
        var submitted = 0;
        vm.OnCancelEdit += _ => cancelled++;
        vm.OnNewTimer += (_, _, _) => submitted++;

        vm.Cancel();

        Assert.That(cancelled, Is.Zero);
        Assert.That(submitted, Is.Zero);
    }

    [Test]
    public void CancellingEditReturnsOriginalTimerWithoutChangingIt()
    {
        var original = ExistingTimer();
        var vm = new ModifyTimerViewModel("Shared");
        vm.Edit(original);
        Timer cancelled = null;
        var submitted = 0;
        vm.OnCancelEdit += timer => cancelled = timer;
        vm.OnNewTimer += (_, _, _) => submitted++;
        vm.Name = "Unsaved change";
        vm.DurationSec = 25;

        vm.Cancel();

        Assert.Multiple(() =>
        {
            Assert.That(cancelled, Is.SameAs(original));
            Assert.That(original.Name, Is.EqualTo("Original timer"));
            Assert.That(original.DurationSec, Is.EqualTo(10));
            Assert.That(submitted, Is.Zero);
        });
    }

    [Test]
    public void SavingEditReportsEditedTimerWithoutCancellingOrMutatingOriginal()
    {
        var original = ExistingTimer();
        var vm = new ModifyTimerViewModel("Shared");
        vm.Edit(original);
        Timer submitted = null;
        var wasEdit = false;
        var wasImport = true;
        var cancelled = 0;
        vm.OnNewTimer += (timer, editing, imported) => { submitted = timer; wasEdit = editing; wasImport = imported; };
        vm.OnCancelEdit += _ => cancelled++;
        vm.Name = "Saved change";
        vm.DurationSec = 25;

        Save(vm);

        Assert.Multiple(() =>
        {
            Assert.That(submitted, Is.Not.Null);
            Assert.That(submitted.Name, Is.EqualTo("Saved change"));
            Assert.That(submitted.Id, Is.EqualTo(original.Id));
            Assert.That(submitted.DurationSec, Is.EqualTo(25));
            Assert.That(wasEdit, Is.True);
            Assert.That(wasImport, Is.False);
            Assert.That(cancelled, Is.Zero);
            Assert.That(original.Name, Is.EqualTo("Original timer"));
            Assert.That(original.DurationSec, Is.EqualTo(10));
        });
    }

    [Test]
    public void EditPreservesDisabledHiddenAndSharedTimerState()
    {
        var original = ExistingTimer();
        original.IsEnabled = false;
        original.IsSubTimer = true;
        original.UseAudio = true;
        original.ShareId = "abc";
        var vm = new ModifyTimerViewModel("Shared");
        vm.Edit(original);
        Timer submitted = null;
        vm.OnNewTimer += (timer, _, _) => submitted = timer;
        Save(vm);
        Assert.Multiple(() =>
        {
            Assert.That(submitted, Is.Not.Null);
            Assert.That(submitted.IsEnabled, Is.False);
            Assert.That(submitted.IsSubTimer, Is.True);
            Assert.That(submitted.UseAudio, Is.True);
            Assert.That(submitted.ShareId, Is.EqualTo("abc"));
        });
    }

    [Test]
    public void EditingCannotRenameToAnotherTimerButCanKeepItsOwnName()
    {
        var original = ExistingTimer();
        var vm = new ModifyTimerViewModel("Shared");
        vm.Edit(original);
        vm.AvailableTimersForCharacter.Add(original);
        vm.AvailableTimersForCharacter.Add(new Timer { Id = "other", Name = "Taken" });
        var submitted = 0;
        vm.OnNewTimer += (_, _, _) => submitted++;
        vm.Name = "Taken";
        Save(vm);
        Assert.That(submitted, Is.Zero);
        Assert.That(vm.ValidationMessage, Is.EqualTo("A timer with this name already exists."));
        vm.Name = original.Name;
        Save(vm);
        Assert.That(submitted, Is.EqualTo(1));
    }

    [TestCase(" ")]
    [TestCase("\t")]
    public void WhitespaceNameDoesNotSubmit(string name)
    {
        var vm = new ModifyTimerViewModel("Shared") { Name = name };
        var submitted = 0;
        vm.OnNewTimer += (_, _, _) => submitted++;
        Save(vm);
        Assert.That(submitted, Is.Zero);
        Assert.That(vm.ValidationMessage, Is.EqualTo("Enter a timer name."));
    }

    private void Redirect(Type type, string name, object value)
    {
        var field = type.GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
        _originalFields.Add(field, field.GetValue(null));
        field.SetValue(null, value);
    }

    private static void Save(ModifyTimerViewModel vm) => typeof(ModifyTimerViewModel)
        .GetMethod("Save", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(vm, new object[] { null });

    private static Timer ExistingTimer() => new()
    {
        Id = Guid.NewGuid().ToString(), Name = "Original timer", TimerSource = "Shared",
        TriggerType = TimerKeyType.CombatStart, DurationSec = 10, SpecificEncounter = "All", SpecificBoss = "All"
    };
}
