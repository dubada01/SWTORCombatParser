using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia.Data;
using Avalonia.Data.Converters;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.Phases;
using SWTORCombatParser.ViewModels.BattleReview;

namespace SWTORCombatParser.Utilities.Converters;

public sealed class EnumDisplayNameConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is SWTORCombatParser.ViewModels.Overviews.SortingOption grouping)
            return grouping switch { SWTORCombatParser.ViewModels.Overviews.SortingOption.BySource => "Source", SWTORCombatParser.ViewModels.Overviews.SortingOption.ByTarget => "Target", _ => "Ability" };
        if (Equals(parameter, "LogReview") && value is DisplayType category && (category == DisplayType.Damage || category == DisplayType.Healing))
            return category.ToString();
        if (value is DisplayType display) return display switch {
            DisplayType.All => "All events", DisplayType.Damage => "Damage dealt", DisplayType.Healing => "Healing done", DisplayType.DamageTaken => "Damage taken",
            DisplayType.HealingReceived => "Healing received", DisplayType.DeathRecap => "Death recap",
            DisplayType.Abilities => "Ability activations", _ => display.ToString()
        };
        if (value is PhaseTrigger phase && parameter is string context && context.StartsWith("Phase"))
            return phase switch
            {
                PhaseTrigger.Default => context == "PhaseEnd" ? "Next phase / combat end" : "No automatic start",
                PhaseTrigger.CombatDuration => context == "PhaseEnd" ? "Elapsed phase time" : "Elapsed combat time",
                PhaseTrigger.EntityHP => "Entity health",
                PhaseTrigger.EntitySpawn => "Entity appears",
                PhaseTrigger.EntityDeath => "Entity dies",
                PhaseTrigger.AbilityUsage => "Ability used",
                PhaseTrigger.AbilityCancel => "Ability cancelled",
                PhaseTrigger.EffectGain => "Effect gained",
                PhaseTrigger.EffectLoss => "Effect lost",
                _ => "Combat starts"
            };
        if (value is ChallengeType challenge && Equals(parameter, "Challenge"))
            return challenge switch
            {
                ChallengeType.DamageOut => "Damage dealt",
                ChallengeType.DamageIn => "Damage taken",
                ChallengeType.AbilityCount => "Ability activations",
                ChallengeType.InterruptCount => "Interrupts",
                ChallengeType.MetricDuringPhase => "Metric during a phase",
                _ => "Effect stacks"
            };
        return Regex.Replace(value?.ToString() ?? "", "(?<=[a-z0-9])(?=[A-Z])", " ");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        // This converter only formats labels. A reverse binding must not replace
        // an enum (or its stored name) with the spaced display string.
        BindingOperations.DoNothing;
}
