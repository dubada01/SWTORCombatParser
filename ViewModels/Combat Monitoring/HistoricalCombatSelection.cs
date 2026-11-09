using System;
using System.Linq;

namespace SWTORCombatParser.ViewModels.Combat_Monitoring;

public enum HistoricalCombatSelectionStage
{
    Opening,
    LoadingPhases,
    Ready,
    Unavailable
}

public sealed record HistoricalCombatSelection(
    PastCombat Row,
    HistoricalCombatSelectionStage Stage,
    bool ShowProgress = false,
    bool ShowsPreviousResults = false,
    string? Error = null)
{
    public bool IsBusy => Stage is HistoricalCombatSelectionStage.Opening or HistoricalCombatSelectionStage.LoadingPhases;
    public bool HasError => !string.IsNullOrEmpty(Error);
    public string Title
    {
        get
        {
            var name = string.IsNullOrWhiteSpace(Row.CombatLabel) ? "Historical combat" : Row.CombatLabel;
            return name.ReplaceLineEndings(" · ");
        }
    }

    public string Details => string.Join(" · ", new[]
    {
        Row.EncounterInfo?.Name,
        string.Join(" ", new[] { Row.BossDetails.GroupSize, Row.BossDetails.Difficulty }.Where(HasText)),
        Row.CombatStartTime.ToString("g"),
        TimeSpan.FromSeconds(Row.CombatDurationSeconds).ToString(@"h\:mm\:ss")
    }.Where(HasText));

    public string Status => Stage switch
    {
        HistoricalCombatSelectionStage.Opening => ShowsPreviousResults ? "Opening · Previous results shown" : "Opening combat…",
        HistoricalCombatSelectionStage.LoadingPhases => "Preparing phases…",
        HistoricalCombatSelectionStage.Unavailable => ShowsPreviousResults ? "Unavailable · Previous results shown" : "Combat unavailable",
        _ => ""
    };

    private static bool HasText(string? value) => !string.IsNullOrWhiteSpace(value);
}
