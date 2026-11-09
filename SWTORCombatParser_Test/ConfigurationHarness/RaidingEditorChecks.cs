using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.VisualTree;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.Phases;
using SWTORCombatParser.ViewModels.Challenges;
using SWTORCombatParser.ViewModels.Phases;
using SWTORCombatParser.ViewModels.Timers;
using SWTORCombatParser.Views.Challenges;
using SWTORCombatParser.Views.Phases;
using SWTORCombatParser.Views.Timers;

public sealed partial class ConfigurationHarnessApp
{
    private static async Task CheckRaidingEditorLayouts(Window main)
    {
        const string source = "The Dread Fortress|Gate Commander Draxus";
        foreach (var (type, mode) in new[] {
            (ChallengeType.DamageOut, "Total damage"),
            (ChallengeType.DamageIn, "Total damage taken"),
            (ChallengeType.EffectStacks, "Maximum stacks") })
        {
            var model = new ChallengeModificationViewModel(source) { Name = "Result setting", SelectedChallengeType = type, Value = "123456789" };
            var window = new ChallengeModificationView(model);
            window.Show(main);
            await Task.Delay(60);
            await Choose(window.FindControl<ComboBox>("ResultMode")!, mode);
            Challenge? saved = null;
            model.OnNewChallenge += (challenge, _) => saved = challenge;
            await InvokeButton(ActionButton(window, "Save challenge"));
            Program.Check(saved != null && (type == ChallengeType.EffectStacks ? saved.UseMaxValue : saved.UseRawValues),
                type + " result selector saves the selected aggregation");
            var reopenedModel = new ChallengeModificationViewModel(source);
            reopenedModel.Edit(saved!);
            var reopened = new ChallengeModificationView(reopenedModel);
            reopened.Show(main);
            await Task.Delay(60);
            Program.Check(Equals(reopened.FindControl<ComboBox>("ResultMode")!.SelectedItem, mode),
                type + " result selection survives reopening the editor");
            reopened.Close();
        }
        foreach (var type in Enum.GetValues<ChallengeType>())
        {
            var model = new ChallengeModificationViewModel(source) {
                Name = type == ChallengeType.MetricDuringPhase ? "Burn phase damage" : "Raid mechanic",
                SelectedChallengeType = type, Value = type == ChallengeType.EffectStacks ? "1234567890123456" : "Corrosive Dart",
                SelectedTarget = "Gate Commander Draxus", SelectedSource = "Gate Commander Draxus", SelectedMetric = "Effective DPS"
            };
            var window = new ChallengeModificationView(model);
            await Capture(window, main, "challenge-layout-" + type);
            Program.Check(type != ChallengeType.InterruptCount || (!model.HasValue && !model.HasTarget && !model.HasResultMode),
                type + " only exposes supported challenge filters");
            if (type == ChallengeType.AbilityCount)
                Program.Check(!model.HasResultMode, "Ability activation count does not offer an unsupported rate");
            if (type == ChallengeType.EffectStacks)
                Program.Check(!model.HasSource && model.ValueWatermark == "Numeric effect ID", "Effect stacks request an effect ID without an ignored source filter");
        }

        var phaseModel = new PhaseModificationViewModel(source) { Name = "Opening" };
        var phaseWindow = new PhaseModificationView(phaseModel);
        phaseWindow.Show(main);
        await Task.Delay(60);
        Program.Check(phaseModel.SelectedPhaseStart == PhaseTrigger.CombatStart &&
            phaseWindow.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "Next phase / combat end"),
            "New phase displays a usable start and explicit automatic end boundary");
        phaseWindow.Close();
        await Capture(new PhaseModificationView(new PhaseModificationViewModel(source) {
            Name = "Burn phase", SelectedPhaseStart = PhaseTrigger.EntityHP, Value = "20",
            SelectedPhaseEnd = PhaseTrigger.CombatDuration, EndValue = "30"
        }), main, "phase-health-duration");

        var timerModel = new ModifyTimerViewModel("Shared") { Name = "Mechanic cooldown", SelectedTriggerType = TimerKeyType.EffectGained, Effect = "Corrosive Dart", DurationSec = 18 };
        var timer = new TimerModificationWindow(timerModel);
        timer.Show(main);
        await Task.Delay(60);
        var behavior = timer.FindControl<Expander>("BehaviorOptions")!;
        Program.Check(!behavior.IsExpanded, "New timers keep advanced refresh and cancellation collapsed");
        Program.Check(behavior.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "Refresh & cancellation"),
            "Timer behavior expander renders its label with the app's toggle styles");
        var toggle = behavior.GetVisualDescendants().OfType<ToggleButton>().First();
        ((IToggleProvider)ControlAutomationPeer.CreatePeerForElement(toggle)!).Toggle();
        await Task.Delay(60);
        Program.Check(behavior.IsExpanded, "Timer behavior opens through its header toggle");
        var ignore = timer.GetVisualDescendants().OfType<CheckBox>().Single(box => Equals(box.Content, "Ignore triggers while active"));
        await Toggle(ignore);
        SWTORCombatParser.DataStructures.Timer? savedTimer = null;
        timerModel.OnNewTimer += (value, _, _) => savedTimer = value;
        await InvokeButton(ActionButton(timer, "Save"));
        Program.Check(savedTimer?.DontRefresh == true, "Expanded timer behavior saves through its original binding");
        var editModel = new ModifyTimerViewModel("Shared");
        editModel.Edit(savedTimer!);
        var edit = new TimerModificationWindow(editModel);
        edit.Show(main);
        await Task.Delay(60);
        Program.Check(edit.FindControl<Expander>("BehaviorOptions")!.IsExpanded, "Existing timer behavior opens automatically when editing");
        edit.Close();
    }
}
