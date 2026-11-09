using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SWTORCombatParser.Model.Phases;
using SWTORCombatParser.ViewModels.Phases;
using SWTORCombatParser.ViewModels.Timers;

namespace SWTORCombatParser_Test;

public class PhaseEditorTests
{
    [Test]
    public void StartAndEndConditionsKeepTheirOwnLabelsAndDraftEntries()
    {
        var editor = new PhaseModificationViewModel("Encounter|Boss")
        {
            SelectedPhaseStart = PhaseTrigger.EffectGain,
            SelectedPhaseEnd = PhaseTrigger.AbilityUsage,
            MultiValueOption = "Start effect",
            EndMultiValueOption = "End ability"
        };

        Assert.Multiple(() =>
        {
            Assert.That(editor.ValuePrompt, Is.EqualTo("Effects (names or IDs)"));
            Assert.That(editor.EndValuePrompt, Is.EqualTo("Abilities (names or IDs)"));
            Assert.That(editor.MultiValueOption, Is.EqualTo("Start effect"));
            Assert.That(editor.EndMultiValueOption, Is.EqualTo("End ability"));
        });
        Invoke(editor, "SaveRefreshCommand");
        Assert.Multiple(() =>
        {
            Assert.That(editor.MultiValueOptions.Select(option => option.Name), Is.EqualTo(new[] { "Start effect" }));
            Assert.That(editor.EndMultiValueOption, Is.EqualTo("End ability"));
            Assert.That(editor.EndMultiValueOptions, Is.Empty);
        });
    }

    [Test]
    public void ChangingTheStartConditionKeepsTheEnteredEndThreshold()
    {
        var editor = new PhaseModificationViewModel("Encounter|Boss")
        {
            SelectedPhaseEnd = PhaseTrigger.EntityHP,
            EndValue = "25"
        };
        editor.SelectedPhaseStart = PhaseTrigger.CombatDuration;
        Assert.That(editor.EndValue, Is.EqualTo("25"));
        Assert.That(editor.EndValuePrompt, Is.EqualTo("Health (%)"));
        Assert.That(editor.ValuePrompt, Is.EqualTo("Seconds into combat"));
    }

    [TestCase(PhaseTrigger.EffectGain)]
    [TestCase(PhaseTrigger.AbilityUsage)]
    public void RemovingAnExistingStartConditionDoesNotChangeTheEndList(PhaseTrigger trigger)
    {
        var phase = new Phase
        {
            Name = "Edited phase", PhaseSource = "Encounter|Boss", StartTrigger = trigger, EndTrigger = trigger,
            StartArgs = new PhaseArgs { AbilityIds = new List<string> { "Start" }, EffectIds = new List<string> { "Start" } },
            EndArgs = new PhaseArgs { AbilityIds = new List<string> { "End" }, EffectIds = new List<string> { "End" } }
        };
        var editor = new PhaseModificationViewModel("Encounter|Boss");
        Invoke(editor, "Edit", phase);
        Invoke(editor.MultiValueOptions.Single(), "Remove");

        Assert.That(editor.MultiValueOptions, Is.Empty);
        Assert.That(editor.EndMultiValueOptions.Select(option => option.Name), Is.EqualTo(new[] { "End" }));
    }

    [TestCase(PhaseTrigger.EntityHP)]
    [TestCase(PhaseTrigger.EntitySpawn)]
    [TestCase(PhaseTrigger.EntityDeath)]
    public void EditedEntityConditionsShowAndPreserveAllTargets(PhaseTrigger trigger)
    {
        var phase = new Phase
        {
            Name = "Entity phase", PhaseSource = "Encounter|Boss", StartTrigger = trigger, EndTrigger = trigger,
            StartArgs = new PhaseArgs { EntityIds = new List<long> { 101, 102 }, HPPercentage = 60.5 },
            EndArgs = new PhaseArgs { EntityIds = new List<long> { 201, 202 }, HPPercentage = 20.5 }
        };
        var editor = new PhaseModificationViewModel(phase.PhaseSource);
        Invoke(editor, "Edit", phase);
        Phase saved = null;
        editor.OnNewPhase += value => saved = value;
        Assert.That(editor.MultiTargetOptions.Select(item => item.Name), Is.EqualTo(new[] { "101", "102" }));
        Assert.That(editor.EndMultiTargetOptions.Select(item => item.Name), Is.EqualTo(new[] { "201", "202" }));
        Invoke(editor, "Save");
        Assert.That(saved.StartArgs.EntityIds, Is.EqualTo(new[] { 101L, 102L }));
        Assert.That(saved.EndArgs.EntityIds, Is.EqualTo(new[] { 201L, 202L }));
        if (trigger == PhaseTrigger.EntityHP)
        {
            Assert.That(saved.StartArgs.HPPercentage, Is.EqualTo(60.5));
            Assert.That(saved.EndArgs.HPPercentage, Is.EqualTo(20.5));
        }
        Invoke(editor.MultiTargetOptions.First(), "Remove");
        Invoke(editor.EndMultiTargetOptions.First(), "Remove");
        Invoke(editor, "Save");
        Assert.That(saved.StartArgs.EntityIds, Is.EqualTo(new[] { 102L }));
        Assert.That(saved.EndArgs.EntityIds, Is.EqualTo(new[] { 202L }));
        Assert.That(phase.StartArgs.EntityIds, Is.EqualTo(new[] { 101L, 102L }));
        Assert.That(phase.EndArgs.EntityIds, Is.EqualTo(new[] { 201L, 202L }));
    }

    [TestCase("invalid")]
    [TestCase("NaN")]
    [TestCase("Infinity")]
    public void InvalidNumericInputExplainsFailureAndDoesNotSave(string input)
    {
        var editor = new PhaseModificationViewModel("Encounter|Boss") { SelectedPhaseEnd = PhaseTrigger.EntityHP, EndValue = input };
        var saved = 0;
        editor.OnNewPhase += _ => saved++;
        Invoke(editor, "Save");
        Assert.That(saved, Is.Zero);
        Assert.That(editor.EndValue, Is.EqualTo(input));
        Assert.That(editor.ValidationMessage, Is.EqualTo("Enter a valid number for the end condition."));
    }

    [Test]
    public void InvalidEntityIdCanBeCorrectedWithoutLosingTheDraft()
    {
        var editor = new PhaseModificationViewModel("Encounter|Boss") { Name = "Effect phase", SelectedPhaseStart = PhaseTrigger.EffectGain, SelectedTarget = "Draxus" };
        var saved = 0;
        editor.OnNewPhase += _ => saved++;
        Invoke(editor, "Save");
        Assert.That(saved, Is.Zero);
        Assert.That(editor.SelectedTarget, Is.EqualTo("Draxus"));
        Assert.That(editor.ValidationMessage, Is.EqualTo("Enter valid entity IDs for the start condition."));
        editor.SelectedTarget = "123";
        Invoke(editor, "Save");
        Assert.That(saved, Is.EqualTo(1));
        Assert.That(editor.ValidationMessage, Is.Empty);
    }

    [Test]
    public void HiddenFieldsDoNotOverrideTheCurrentCondition()
    {
        var editor = new PhaseModificationViewModel("Encounter|Boss")
        {
            Name = "Changed condition", SelectedPhaseStart = PhaseTrigger.EntityHP, Value = "invalid", SelectedPhaseEnd = PhaseTrigger.EntityDeath
        };
        editor.MultiTargetOptions.Add(new RefreshOptionViewModel { Name = "invalid hidden target" });
        editor.EndMultiTargetOptions.Add(new RefreshOptionViewModel { Name = "invalid hidden target" });
        editor.SelectedPhaseStart = PhaseTrigger.EffectGain;
        editor.SelectedTarget = "123";
        editor.SelectedPhaseEnd = PhaseTrigger.AbilityUsage;
        editor.EndSelectedTarget = "456";
        Phase saved = null;
        editor.OnNewPhase += value => saved = value;
        Invoke(editor, "Save");
        Assert.That(saved, Is.Not.Null);
        Assert.That(saved.StartArgs.EntityIds, Is.EqualTo(new[] { 123L }));
        Assert.That(saved.EndArgs.EntityIds, Is.EqualTo(new[] { 456L }));
        Assert.That(saved.StartArgs.HPPercentage, Is.Zero);
        Assert.That(editor.ValidationMessage, Is.Empty);
    }

    [TestCase(null)]
    [TestCase(" ")]
    public void MissingNameCanBeCorrectedWithoutClosingOrLosingThePhase(string name)
    {
        var editor = new PhaseModificationViewModel("Encounter|Boss") { Name = name };
        var saved = 0;
        editor.OnNewPhase += _ => saved++;
        Invoke(editor, "Save");
        Assert.That(saved, Is.Zero);
        Assert.That(editor.ValidationMessage, Is.EqualTo("Enter a phase name."));
        editor.Name = "Opening";
        Invoke(editor, "Save");
        Assert.That(saved, Is.EqualTo(1));
        Assert.That(editor.ValidationMessage, Is.Empty);
    }

    [Test]
    public void NewPhaseStartsWithCombatAndUsesTheNextPhaseAsItsEnd()
    {
        var editor = new PhaseModificationViewModel("Encounter|Boss");
        Assert.That(editor.SelectedPhaseStart, Is.EqualTo(PhaseTrigger.CombatStart));
        Assert.That(editor.SelectedPhaseEnd, Is.EqualTo(PhaseTrigger.Default));
        editor.SelectedPhaseEnd = PhaseTrigger.CombatDuration;
        Assert.That(editor.EndValuePrompt, Is.EqualTo("Seconds into phase"));
    }

    private static void Invoke(object instance, string method, params object[] arguments) =>
        instance.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(instance, arguments);
}
