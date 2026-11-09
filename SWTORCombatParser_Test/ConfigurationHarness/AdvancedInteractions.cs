using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.ViewModels.Timers;
using SWTORCombatParser.ViewModels.Phases;
using SWTORCombatParser.ViewModels.Challenges;
using SWTORCombatParser.ViewModels.Overlays;
using SWTORCombatParser.ViewModels;
using System.Windows.Input;
using SWTORCombatParser.Views.Timers;
using SWTORCombatParser.Views.Phases;
using SWTORCombatParser.Views.Challenges;
using SWTORCombatParser.Utilities.Encounter_Selection;
using Timer = SWTORCombatParser.DataStructures.Timer;

public sealed partial class ConfigurationHarnessApp
{
    private static async Task CheckTimerTabsAndConditions(Window main, Action<bool, string> verify)
    {
        var draft = new ModifyTimerViewModel("Shared");
        var editor = new TimerModificationWindow(draft);
        editor.Show(main);
        await Task.Delay(60);
        await InvokeButton(ActionButton(editor, "Save"));
        verify(editor.IsVisible && !string.IsNullOrEmpty(draft.ValidationMessage), "Blank timer stays open with a validation message");
        editor.FindControl<TextBox>("TimerName")!.Text = "Compound interaction timer";
        await Toggle(editor.FindControl<CheckBox>("VariableCheck")!);
        var tabs = editor.FindControl<TabStrip>("EffectsTabStrip")!;
        verify(tabs.SelectedIndex == 1 && editor.FindControl<ContentControl>("TabContent")!.Content is VariablesTabContent,
            "Modify variables opens the Variables tab");
        var variables = (VariablesTabContent)editor.FindControl<ContentControl>("TabContent")!.Content!;
        variables.GetVisualDescendants().OfType<TextBox>().First().Text = "Interaction counter";
        await InvokeButton(ActionButton(variables, "Add"));
        verify(draft.SelectedModifyVariable == "Interaction counter", "Adding a variable selects it in the editor");
        var actions = variables.GetVisualDescendants().OfType<ComboBox>().Last();
        foreach (var action in draft.AvailableActions)
        {
            await Choose(actions, action);
            verify(draft.SelectedAction == action, "Variable action dropdown selects " + action);
        }
        await Toggle(variables.GetVisualDescendants().OfType<CheckBox>().Single());
        tabs.SelectedIndex = 0;
        await Task.Delay(40);
        var visuals = (VisualsTabContent)editor.FindControl<ContentControl>("TabContent")!.Content!;
        var duration = visuals.GetVisualDescendants().OfType<TextBox>().First();
        duration.Text = "18";
        tabs.SelectedIndex = 1;
        await Task.Delay(40);
        verify(draft.DurationSec == 18 && draft.SelectedModifyVariable == "Interaction counter", "Appearance and Variables retain values while switching tabs");
        await Toggle(editor.FindControl<CheckBox>("VariableCheck")!);
        verify(tabs.SelectedIndex == 0 && editor.FindControl<ContentControl>("TabContent")!.Content is VisualsTabContent,
            "Disabling Modify variables returns to Appearance");

        var triggers = editor.GetVisualDescendants().OfType<ComboBox>().First();
        await Choose(triggers, TimerKeyType.And);
        foreach (var caption in new[] { "Condition A", "Condition B" })
        {
            await InvokeButton(ActionButton(editor, caption));
            var condition = ((IClassicDesktopStyleApplicationLifetime)Current!.ApplicationLifetime!).Windows
                .OfType<TimerModificationWindow>().Single(window => window.IsVisible && window != editor);
            verify(ReferenceEquals(condition.Owner, editor), caption + " is modal to its timer editor");
            await InvokeButton(ActionButton(condition, "Save"));
            verify(!condition.IsVisible && editor.IsVisible, caption + " saves without closing its timer editor");
        }
        await InvokeButton(ActionButton(editor, "Condition A"));
        var reopened = ((IClassicDesktopStyleApplicationLifetime)Current!.ApplicationLifetime!).Windows
            .OfType<TimerModificationWindow>().Single(window => window.IsVisible && window != editor);
        var conditionDraft = (ModifyTimerViewModel)reopened.DataContext!;
        verify(conditionDraft.SelectedTriggerType == TimerKeyType.CombatStart, "Reopening Condition A restores the saved trigger");
        await Choose(reopened.GetVisualDescendants().OfType<ComboBox>().First(), TimerKeyType.FightDuration);
        await InvokeButton(ActionButton(reopened, "Cancel"));
        Timer? saved = null;
        draft.OnNewTimer += (timer, _, _) => saved = timer;
        await InvokeButton(ActionButton(editor, "Save"));
        verify(saved?.Clause1?.TriggerType == TimerKeyType.CombatStart && saved?.Clause2 != null,
            "Cancelling a condition edit preserves both saved conditions");
    }

    private static async Task CheckInvalidEntries(Window main, Action<bool, string> verify)
    {
        var phaseDraft = new PhaseModificationViewModel("The Dread Fortress|Gate Commander Draxus");
        var phase = new PhaseModificationView(phaseDraft);
        phase.Show(main);
        await Task.Delay(40);
        await InvokeButton(ActionButton(phase, "Save phase"));
        verify(phase.IsVisible, "Blank phase name cannot be saved");
        Program.Snapshot(phase, "phase-validation");
        if (phase.IsVisible)
        {
            phase.FindControl<TextBox>("TimerName")!.Text = "Corrected phase";
            await InvokeButton(ActionButton(phase, "Save phase"));
            verify(!phase.IsVisible && string.IsNullOrEmpty(phaseDraft.ValidationMessage), "Correcting the phase name saves and clears validation");
        }
        phase.Close();

        var challengeDraft = new ChallengeModificationViewModel("The Dread Fortress|Gate Commander Draxus");
        var challenge = new ChallengeModificationView(challengeDraft);
        challenge.Show(main);
        await Task.Delay(40);
        await InvokeButton(ActionButton(challenge, "Save challenge"));
        verify(challenge.IsVisible, "Blank challenge name cannot be saved");
        Program.Snapshot(challenge, "challenge-validation");
        if (challenge.IsVisible)
        {
            challenge.FindControl<TextBox>("TimerName")!.Text = "Corrected challenge";
            await InvokeButton(ActionButton(challenge, "Save challenge"));
            verify(!challenge.IsVisible && string.IsNullOrEmpty(challengeDraft.ValidationMessage), "Correcting the challenge name saves and clears validation");
        }
        challenge.Close();

        challengeDraft = new ChallengeModificationViewModel("The Dread Fortress|Gate Commander Draxus");
        challenge = new ChallengeModificationView(challengeDraft);
        challenge.Show(main);
        await Task.Delay(40);
        challenge.FindControl<TextBox>("TimerName")!.Text = "Phase metric";
        await Choose(challenge.GetVisualDescendants().OfType<ComboBox>().First(), ChallengeType.MetricDuringPhase);
        var metric = challenge.GetVisualDescendants().OfType<ComboBox>().Last();
        await Choose(metric, "None");
        await InvokeButton(ActionButton(challenge, "Save challenge"));
        verify(challenge.IsVisible && challengeDraft.SelectedPhase != null, "Phase challenge requires a usable metric");
        if (challenge.IsVisible)
        {
            await Choose(metric, "Effective DPS");
            await InvokeButton(ActionButton(challenge, "Save challenge"));
            verify(!challenge.IsVisible, "Phase and metric dropdowns produce a valid saved challenge");
        }
        challenge.Close();

        challengeDraft = new ChallengeModificationViewModel("The Dread Fortress|Nefra");
        challenge = new ChallengeModificationView(challengeDraft);
        challenge.Show(main);
        await Task.Delay(40);
        challenge.FindControl<TextBox>("TimerName")!.Text = "Missing phase";
        await Choose(challenge.GetVisualDescendants().OfType<ComboBox>().First(), ChallengeType.MetricDuringPhase);
        await InvokeButton(ActionButton(challenge, "Save challenge"));
        verify(challenge.IsVisible, "Metric during phase cannot save without a selected phase");
        challenge.Close();

        var timerDraft = new ModifyTimerViewModel("Shared");
        var timer = new TimerModificationWindow(timerDraft);
        timer.Show(main);
        await Task.Delay(40);
        timer.FindControl<TextBox>("TimerName")!.Text = "Invalid duration";
        var visuals = (VisualsTabContent)timer.FindControl<ContentControl>("TabContent")!.Content!;
        visuals.GetVisualDescendants().OfType<TextBox>().First().Text = "not a number";
        await InvokeButton(ActionButton(timer, "Save"));
        verify(timer.IsVisible, "Invalid timer duration cannot silently save a previous numeric value");
        verify(timer.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "Enter a valid number.") &&
            !timer.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text?.Contains("InvalidCastException") == true),
            "Invalid timer numbers show a concise field error instead of an exception");
        Program.Snapshot(timer, "timer-validation");
        if (timer.IsVisible)
        {
            visuals.GetVisualDescendants().OfType<TextBox>().First().Text = "15";
            await InvokeButton(ActionButton(timer, "Save"));
            verify(!timer.IsVisible && timerDraft.DurationSec == 15, "Correcting invalid numeric text saves the new duration");
        }
        timer.Close();
    }

    private static async Task CheckManagerMenus(Window main, OverlayViewModel overlays, Action<bool, string> verify)
    {
        var owner = (MainWindowViewModel)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(MainWindowViewModel));
        var challenge = new ChallengeSetupView { DataContext = overlays._challengesViewModel };
        typeof(MainWindowViewModel).GetField("_overlayViewModel", Program.Private)!.SetValue(owner, overlays);
        typeof(MainWindowViewModel).GetField("_challengeView", Program.Private)!.SetValue(owner, challenge);
        typeof(MainWindowViewModel).GetField("_phaseBarViewModel", Program.Private)!.SetValue(owner, new PhaseBarViewModel());
        void HideOnClose(object? sender, WindowClosingEventArgs args) { args.Cancel = true; challenge.Hide(); }
        challenge.Closing += HideOnClose;
        try
        {
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                ((ICommand)owner.ShowChallengeWindowCommand).Execute(null);
                await Task.Delay(50);
                verify(challenge.IsVisible && challenge.DataContext == overlays._challengesViewModel, "Challenges menu opens its cached window, attempt " + attempt);
                challenge.Close();
                verify(!challenge.IsVisible, "Challenges native close hides its cached window, attempt " + attempt);
                ((ICommand)owner.OpenPhaseConfigCommand).Execute(null);
                await Task.Delay(50);
                var phase = OpenEditor<PhaseListView>();
                verify(phase.DataContext is PhaseListViewModel model && !string.IsNullOrEmpty(model.SelectedSource), "Phases menu reopens with a valid encounter, attempt " + attempt);
                phase.Close();
            }
        }
        finally { challenge.Closing -= HideOnClose; challenge.Close(); }
    }

    private static async Task CheckEncounterDropdowns(Window main, OverlayViewModel overlays, Action<bool, string> verify)
    {
        var phaseModel = new PhaseListViewModel();
        var managers = new (Window Window, EncounterSelectionView Selection, Func<string> Source)[] {
            (new PhaseListView(phaseModel), phaseModel.EncounterSelectionView, () => phaseModel.SelectedSource),
            (new ChallengeSetupView { DataContext = overlays._challengesViewModel }, overlays._challengesViewModel.EncounterSelectionView, () => overlays._challengesViewModel.SelectedSource)
        };
        foreach (var (window, selection, source) in managers)
        {
            window.Show(main);
            await Task.Delay(40);
            var model = (EncounterSelectionViewModel)selection.DataContext!;
            var combos = selection.GetVisualDescendants().OfType<ComboBox>().ToArray();
            foreach (var encounterName in new[] { "The Dread Fortress", "The Dread Palace", "The Dread Fortress" })
            {
                var encounter = model.AvailableEncounters.Single(item => item.Name == encounterName);
                await Choose(combos[0], encounter);
                verify(model.AvailableBosses.Contains(model.SelectedBoss) && source() == encounter.Name + "|" + model.SelectedBoss,
                    window.Title + " encounter dropdown selects a valid default boss in " + encounterName);
                foreach (var boss in model.AvailableBosses.Take(2).ToArray())
                {
                    await Choose(combos[1], boss);
                    verify(source() == encounter.Name + "|" + boss, window.Title + " boss dropdown selects " + boss);
                }
            }
            window.Close();
        }
    }
}
