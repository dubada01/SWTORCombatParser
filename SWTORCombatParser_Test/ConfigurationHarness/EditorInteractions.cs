using Avalonia;
using Avalonia.Controls;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SWTORCombatParser.Model.RunRace;
using SWTORCombatParser.Model.SessionReview;
using SWTORCombatParser.ViewModels;
using SWTORCombatParser.ViewModels.Combat_Monitoring;
using SWTORCombatParser.ViewModels.Overlays;
using SWTORCombatParser.ViewModels.Phases;
using SWTORCombatParser.Views.Timers;
using SWTORCombatParser.Views.Challenges;
using SWTORCombatParser.Views.Phases;
using SWTORCombatParser.ViewModels.Timers;
using SWTORCombatParser.ViewModels.Challenges;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.Timers;
using SWTORCombatParser.Model.Phases;
using SWTORCombatParser.Model.Challenge;
using SWTORCombatParser.Utilities.Encounter_Selection;
using SWTORCombatParser.Model.Overlays;
using System.Text.Json;

public sealed partial class ConfigurationHarnessApp
{
    private static async Task PrepareInteractive(Window main)
    {
        main.Title = "Orbs UI test session";
        main.Width = 460;
        main.Height = 220;
        main.Classes.Add("OrbsDialog");
        var monitor = new CombatMonitorViewModel();
        var session = new SessionReviewService(monitor);
        var race = new RunRaceService(new RunGhostStore(Path.Combine(Program.Output, "ghosts.json")), isOffline: () => true);
        var overlays = new OverlayViewModel(session, race);
        if (Program.Field(overlays._timersViewModel, "_loadDefaultsTask") is Task load) await load;
        var owner = (MainWindowViewModel)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(MainWindowViewModel));
        var timers = new TimersCreationView { DataContext = overlays._timersViewModel };
        var challenges = new ChallengeSetupView { DataContext = overlays._challengesViewModel };
        typeof(MainWindowViewModel).GetField("_overlayViewModel", Program.Private)!.SetValue(owner, overlays);
        typeof(MainWindowViewModel).GetField("_timersView", Program.Private)!.SetValue(owner, timers);
        typeof(MainWindowViewModel).GetField("_challengeView", Program.Private)!.SetValue(owner, challenges);
        typeof(MainWindowViewModel).GetField("_phaseBarViewModel", Program.Private)!.SetValue(owner, new PhaseBarViewModel());
        timers.Closing += (_, args) => { args.Cancel = true; timers.Hide(); };
        challenges.Closing += (_, args) => { args.Cancel = true; challenges.Hide(); };
        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = "Isolated test profile", FontSize = 16 });
        foreach (var (title, command) in new[] { ("Timers", owner.ShowTimerWindowCommand), ("Phases", owner.OpenPhaseConfigCommand), ("Challenges", owner.ShowChallengeWindowCommand) })
            panel.Children.Add(new Button { Content = title, Command = command });
        main.Content = panel;
        main.Closed += (_, _) => Environment.Exit(0);
        main.Activate();
    }

    private static async Task InvokeButton(Button button)
    {
        if (ControlAutomationPeer.CreatePeerForElement(button) is not IInvokeProvider action)
            throw new InvalidOperationException("Button has no accessible Invoke action: " + button.Content);
        action.Invoke();
        await Task.Delay(60);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
    }

    private static Button ActionButton(Control root, string caption) => root.GetVisualDescendants().OfType<Button>()
        .Single(button => button.Content is string text && text == caption && button.IsVisible);
    private static T OpenEditor<T>() where T : Window => ((IClassicDesktopStyleApplicationLifetime)Current!.ApplicationLifetime!).Windows
        .OfType<T>().Single(window => window.IsVisible);
    private static async Task Choose(ComboBox combo, object item)
    {
        combo.IsDropDownOpen = true;
        await Task.Delay(20);
        combo.SelectedItem = item;
        combo.IsDropDownOpen = false;
        await Task.Delay(20);
    }
    private static async Task Toggle(CheckBox box)
    {
        ((IToggleProvider)ControlAutomationPeer.CreatePeerForElement(box)!).Toggle();
        await Task.Delay(40);
    }
    private static async Task CheckEditorWorkflows(OverlayViewModel overlays, Window main)
    {
        var errors = new List<string>();
        var checks = new List<string>();
        void Verify(bool valid, string message) { (valid ? checks : errors).Add(message); }
        async Task Case(string name, Func<Task> run)
        {
            try { await run(); }
            catch (Exception error) { errors.Add(name + ": " + error); }
            finally
            {
                foreach (var window in ((IClassicDesktopStyleApplicationLifetime)Current!.ApplicationLifetime!).Windows.ToArray())
                    if (window != main && (window is TimerModificationWindow or PhaseModificationView or ChallengeModificationView)) window.Close();
            }
        }
        await Case("Timer workflow", async () =>
        {
            var model = overlays._timersViewModel;
            model.SelectedTimerSourceType = TimerType.Discipline;
            model.SelectedDisciplineSource = "Shared";
            var list = new TimersCreationView { DataContext = model };
            list.Show(main);
            await Task.Delay(60);
            await InvokeButton(ActionButton(list, "New timer"));
            var editor = OpenEditor<TimerModificationWindow>();
            var draft = (ModifyTimerViewModel)editor.DataContext!;
            Verify(ReferenceEquals(editor.Owner, list), "Timer editor is modal to its parent timer manager");
            var triggers = editor.GetVisualDescendants().OfType<ComboBox>().Single(combo => ReferenceEquals(combo.ItemsSource, draft.AvailableTriggerTypes));
            foreach (var trigger in draft.AvailableTriggerTypes.ToArray())
            {
                await Choose(triggers, trigger);
                Verify(draft.SelectedTriggerType.Equals(trigger), "Timer trigger dropdown selects " + trigger);
            }
            await Choose(triggers, TimerKeyType.CombatStart);
            editor.FindControl<TextBox>("TimerName")!.Text = "Interaction timer";
            await InvokeButton(ActionButton(editor, "Save"));
            Verify(!editor.IsVisible, "Timer Save button closes a valid new timer");
            var row = model.TimerRows.Single(item => item.Name == "Interaction timer");
            var id = row.SourceTimer.Id;
            var enabled = list.GetVisualDescendants().OfType<CheckBox>().Single(box => ReferenceEquals(box.DataContext, row));
            await Toggle(enabled);
            Verify(!DefaultOrbsTimersManager.GetDefaults("Shared").Timers.Single(timer => timer.Id == id).IsEnabled,
                "New timer Enabled checkbox persists immediately");
            await InvokeButton(list.GetVisualDescendants().OfType<Button>().Single(button => ReferenceEquals(button.DataContext, row) && ToolTip.GetTip(button)?.ToString() == "Toggle timer audio"));
            await InvokeButton(list.GetVisualDescendants().OfType<Button>().Single(button => ReferenceEquals(button.DataContext, row) && ToolTip.GetTip(button)?.ToString() == "Toggle timer visibility"));
            Verify(row.SourceTimer.UseAudio && row.SourceTimer.IsSubTimer, "Timer row audio and visibility buttons update the timer");
            var edit = list.GetVisualDescendants().OfType<Button>().Single(button => ReferenceEquals(button.DataContext, row) && (ToolTip.GetTip(button)?.ToString() == "Edit timer"));
            await InvokeButton(edit);
            editor = OpenEditor<TimerModificationWindow>();
            editor.FindControl<TextBox>("TimerName")!.Text = "Interaction timer edited";
            await InvokeButton(ActionButton(editor, "Save"));
            var saved = DefaultOrbsTimersManager.GetDefaults("Shared").Timers.Single(timer => timer.Id == id);
            Verify(saved.Name == "Interaction timer edited" && !saved.IsEnabled, "Timer edit preserves its identity and disabled state");
            Verify(saved.UseAudio && saved.IsSubTimer, "Editing a hidden timer preserves visibility and audio settings");
            row = model.TimerRows.Single(item => item.SourceTimer.Id == id);
            edit = list.GetVisualDescendants().OfType<Button>().Single(button => ReferenceEquals(button.DataContext, row) && (ToolTip.GetTip(button)?.ToString() == "Edit timer"));
            await InvokeButton(edit);
            editor = OpenEditor<TimerModificationWindow>();
            editor.FindControl<TextBox>("TimerName")!.Text = "Cancelled timer change";
            await InvokeButton(ActionButton(editor, "Cancel"));
            Verify(model.TimerRows.Count(item => item.SourceTimer.Id == id) == 1 &&
                model.TimerRows.Single(item => item.SourceTimer.Id == id).Name == "Interaction timer edited", "Timer Cancel restores exactly one unchanged row");
            row = model.TimerRows.Single(item => item.SourceTimer.Id == id);
            enabled = list.GetVisualDescendants().OfType<CheckBox>().Single(box => ReferenceEquals(box.DataContext, row));
            await Toggle(enabled);
            Verify(DefaultOrbsTimersManager.GetDefaults("Shared").Timers.Single(timer => timer.Id == id).IsEnabled == row.IsEnabled,
                "Timer Enabled checkbox persists after Cancel");
            list.Close();
            var reopened = new TimersCreationView { DataContext = model };
            reopened.Show(main);
            await Task.Delay(60);
            Verify(model.TimerRows.Count(item => item.SourceTimer.Id == id) == 1, "Reopening timer manager retains the saved row exactly once");
            row = model.TimerRows.Single(item => item.SourceTimer.Id == id);
            await InvokeButton(reopened.GetVisualDescendants().OfType<Button>().Single(button => ReferenceEquals(button.DataContext, row) && ToolTip.GetTip(button)?.ToString() == "Duplicate timer"));
            var copy = model.TimerRows.Single(item => item.Name == "Interaction timer edited (COPY)");
            Verify(copy.SourceTimer.Id != id && DefaultOrbsTimersManager.GetDefaults("Shared").Timers.Any(timer => timer.Id == copy.SourceTimer.Id), "Duplicate timer creates and persists a separate identity");
            await InvokeButton(reopened.GetVisualDescendants().OfType<Button>().Single(button => ReferenceEquals(button.DataContext, copy) && ToolTip.GetTip(button)?.ToString() == "Delete timer"));
            Verify(!model.TimerRows.Contains(copy) && !DefaultOrbsTimersManager.GetDefaults("Shared").Timers.Any(timer => timer.Id == copy.SourceTimer.Id), "Deleting the test duplicate removes only its row and saved entry");
            reopened.Close();
        });
        await Case("Phase workflow", async () =>
        {
            var model = new PhaseListViewModel();
            SelectFixtureEncounter(model.EncounterSelectionView);
            var list = new PhaseListView(model);
            list.Show(main);
            await Task.Delay(60);
            await InvokeButton(ActionButton(list, "Add phase"));
            var editor = OpenEditor<PhaseModificationView>();
            var draft = (PhaseModificationViewModel)editor.DataContext!;
            Verify(ReferenceEquals(editor.Owner, list), "Phase editor is modal to its parent phase manager");
            var conditions = editor.GetVisualDescendants().OfType<ComboBox>().ToArray();
            foreach (var trigger in draft.AvailablePhaseTypes)
            {
                await Choose(conditions[0], trigger);
                await Choose(conditions[1], trigger);
                Verify(draft.SelectedPhaseStart == trigger && draft.SelectedPhaseEnd == trigger, "Phase start/end dropdowns select " + trigger);
            }
            await Choose(conditions[0], PhaseTrigger.CombatDuration);
            await Choose(conditions[1], PhaseTrigger.Default);
            editor.FindControl<TextBox>("TimerName")!.Text = "Interaction phase";
            await InvokeButton(ActionButton(editor, "Save phase"));
            Verify(!editor.IsVisible, "Phase Save button closes a valid new phase");
            var row = model.PhaseRows.Single(item => item.Name == "Interaction phase");
            var id = row.SourcePhase.Id;
            await InvokeButton(list.GetVisualDescendants().OfType<Button>().Single(button => ReferenceEquals(button.DataContext, row) && Equals(button.Content, "Edit")));
            editor = OpenEditor<PhaseModificationView>();
            editor.FindControl<TextBox>("TimerName")!.Text = "Interaction phase edited";
            await InvokeButton(ActionButton(editor, "Save phase"));
            Verify(DefaultPhaseManager.GetExisitingPhases().Single(phase => phase.Id == id).Name == "Interaction phase edited", "Phase edit persists with the same identity");
            row = model.PhaseRows.Single(item => item.SourcePhase.Id == id);
            await InvokeButton(list.GetVisualDescendants().OfType<Button>().Single(button => ReferenceEquals(button.DataContext, row) && Equals(button.Content, "Edit")));
            editor = OpenEditor<PhaseModificationView>();
            editor.FindControl<TextBox>("TimerName")!.Text = "Cancelled phase change";
            editor.Close();
            Verify(model.PhaseRows.Count(item => item.SourcePhase.Id == id) == 1 && model.PhaseRows.Single(item => item.SourcePhase.Id == id).Name == "Interaction phase edited", "Native phase close restores exactly one unchanged row");
            list.Close();
            var reloaded = new PhaseListViewModel();
            SelectFixtureEncounter(reloaded.EncounterSelectionView);
            Verify(reloaded.PhaseRows.Count(item => item.SourcePhase.Id == id) == 1, "Reopening phases reloads the saved edit exactly once");
            var reopened = new PhaseListView(reloaded);
            reopened.Show(main);
            await Task.Delay(60);
            row = reloaded.PhaseRows.Single(item => item.SourcePhase.Id == id);
            await InvokeButton(reopened.GetVisualDescendants().OfType<Button>().Single(button => ReferenceEquals(button.DataContext, row) && Equals(button.Content, "Edit")));
            editor = OpenEditor<PhaseModificationView>();
            Verify(editor.FindControl<TextBox>("TimerName")!.Text == "Interaction phase edited", "Phase editor restores the saved name after manager reopen");
            await InvokeButton(ActionButton(editor, "Cancel"));
            row = reloaded.PhaseRows.Single(item => item.SourcePhase.Id == id);
            await InvokeButton(reopened.GetVisualDescendants().OfType<Button>().Single(button => ReferenceEquals(button.DataContext, row) && ToolTip.GetTip(button)?.ToString() == "Delete phase"));
            Verify(!reloaded.PhaseRows.Contains(row) && !DefaultPhaseManager.GetExisitingPhases().Any(phase => phase.Id == id), "Deleting the test phase updates the list and persisted phases");
            reopened.Close();
        });
        await Case("Challenge workflow", async () =>
        {
            var model = overlays._challengesViewModel;
            SelectFixtureEncounter(model.EncounterSelectionView);
            var list = new ChallengeSetupView { DataContext = model };
            list.Show(main);
            await Task.Delay(60);
            await InvokeButton(ActionButton(list, "Add challenge"));
            var editor = OpenEditor<ChallengeModificationView>();
            var draft = (ChallengeModificationViewModel)editor.DataContext!;
            Verify(ReferenceEquals(editor.Owner, list), "Challenge editor is modal to its parent challenge manager");
            var types = editor.GetVisualDescendants().OfType<ComboBox>().First();
            foreach (var type in draft.AvailableChallengeTypes)
            {
                await Choose(types, type);
                Verify(draft.SelectedChallengeType == type, "Challenge dropdown selects " + type);
            }
            await Choose(types, ChallengeType.DamageOut);
            editor.FindControl<TextBox>("TimerName")!.Text = "Interaction challenge";
            await InvokeButton(ActionButton(editor, "Save challenge"));
            Verify(!editor.IsVisible, "Challenge Save button closes a valid new challenge");
            var row = model.ChallengeRows.Single(item => item.Name == "Interaction challenge");
            var id = row.SourceChallenge.Id;
            await Toggle(list.GetVisualDescendants().OfType<CheckBox>().Single(box => ReferenceEquals(box.DataContext, row)));
            Verify(!DefaultChallengeManager.GetAllDefaults().SelectMany(group => group.Challenges).Single(challenge => challenge.Id == id).IsEnabled,
                "New challenge Active checkbox persists immediately");
            await InvokeButton(list.GetVisualDescendants().OfType<Button>().Single(button => ReferenceEquals(button.DataContext, row) && Equals(button.Content, "Edit")));
            editor = OpenEditor<ChallengeModificationView>();
            editor.FindControl<TextBox>("TimerName")!.Text = "Interaction challenge edited";
            await InvokeButton(ActionButton(editor, "Save challenge"));
            var saved = DefaultChallengeManager.GetAllDefaults().SelectMany(group => group.Challenges).Single(challenge => challenge.Name == "Interaction challenge edited");
            Verify(saved.Id == id && !saved.IsEnabled, "Challenge edit preserves its identity and disabled state");
            id = saved.Id;
            row = model.ChallengeRows.Single(item => item.SourceChallenge.Id == id);
            await InvokeButton(list.GetVisualDescendants().OfType<Button>().Single(button => ReferenceEquals(button.DataContext, row) && Equals(button.Content, "Edit")));
            editor = OpenEditor<ChallengeModificationView>();
            editor.FindControl<TextBox>("TimerName")!.Text = "Cancelled challenge change";
            await InvokeButton(ActionButton(editor, "Cancel"));
            Verify(model.ChallengeRows.Count(item => item.SourceChallenge.Id == id) == 1 && model.ChallengeRows.Single(item => item.SourceChallenge.Id == id).Name == "Interaction challenge edited", "Challenge Cancel restores exactly one unchanged row");
            row = model.ChallengeRows.Single(item => item.SourceChallenge.Id == id);
            await Toggle(list.GetVisualDescendants().OfType<CheckBox>().Single(box => ReferenceEquals(box.DataContext, row)));
            Verify(DefaultChallengeManager.GetAllDefaults().SelectMany(group => group.Challenges).Single(challenge => challenge.Id == id).IsEnabled == row.IsEnabled,
                "Challenge Active checkbox persists after Cancel");
            list.Close();
            model.RefreshEncounterSelection();
            Verify(model.ChallengeRows.Count(item => item.SourceChallenge.Id == id) == 1, "Reopening challenges reloads the saved edit exactly once");
            var reopened = new ChallengeSetupView { DataContext = model };
            reopened.Show(main);
            await Task.Delay(60);
            row = model.ChallengeRows.Single(item => item.SourceChallenge.Id == id);
            await InvokeButton(reopened.GetVisualDescendants().OfType<Button>().Single(button => ReferenceEquals(button.DataContext, row) && Equals(button.Content, "Edit")));
            editor = OpenEditor<ChallengeModificationView>();
            Verify(editor.FindControl<TextBox>("TimerName")!.Text == "Interaction challenge edited", "Challenge editor restores the saved name after manager reopen");
            editor.Close();
            await Task.Delay(60);
            row = model.ChallengeRows.Single(item => item.SourceChallenge.Id == id);
            await InvokeButton(reopened.GetVisualDescendants().OfType<Button>().Single(button => ReferenceEquals(button.DataContext, row) && ToolTip.GetTip(button)?.ToString() == "Delete challenge"));
            Verify(!model.ChallengeRows.Contains(row) && !DefaultChallengeManager.GetAllDefaults().SelectMany(group => group.Challenges).Any(challenge => challenge.Id == id), "Deleting the test challenge updates the list and saved challenges");
            reopened.Close();
        });
        await Case("Timer tabs and conditions", () => CheckTimerTabsAndConditions(main, Verify));
        await Case("Invalid editor entries", () => CheckInvalidEntries(main, Verify));
        await Case("Encounter dropdowns", () => CheckEncounterDropdowns(main, overlays, Verify));
        await Case("Manager menu reopen", () => CheckManagerMenus(main, overlays, Verify));
        File.WriteAllText(Path.Combine(Program.Output, "editor-workflows.json"), JsonSerializer.Serialize(new { Checks = checks, Failures = errors }, new JsonSerializerOptions { WriteIndented = true }));
        Program.Checks.AddRange(checks);
        if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
    }
}
