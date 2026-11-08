using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Utilities;
using Orbs.RunRace;

namespace SWTORCombatParser.Model.CloudRaiding;

/// <summary>Preserves time-trial submissions without coupling timers or local review to a window.</summary>
public sealed class EncounterTimeTrialRecorder
{
    public static EncounterTimeTrialRecorder Shared { get; } = new();
    private readonly object _gate = new();
    private readonly HashSet<(string Boss, DateTime Start)> _submitted = new();
    private (DateTime Entered, string Name, string Difficulty, string Size) _session;
    private bool _tracking;

    public TimeTrialLeaderboardEntry? CreateEntry(EncounterInfo encounter, string boss, bool killed,
        DateTime entered, DateTime start, DateTime end, string player)
    {
        lock (_gate)
        {
            var session = (entered, encounter.Name, encounter.Difficutly, encounter.NumberOfPlayer);
            if (_session != session)
            {
                _session = session;
                _tracking = false;
                _submitted.Clear();
            }
            if (entered == DateTime.MinValue || entered > start || end < start || encounter.IsOpenWorld ||
                !encounter.IsBossEncounter || (encounter.NumberOfPlayer != "4" && encounter.Difficutly != "Story") ||
                !encounter.BossInfos.Any(b => b.EncounterName == boss))
                return null;

            // Operations must have observed their first boss; flashpoints may be joined in progress.
            _tracking |= encounter.NumberOfPlayer == "4" || encounter.BossInfos[0].EncounterName == boss;
            if (!_tracking || !killed || string.IsNullOrWhiteSpace(player) || !_submitted.Add((boss, start)))
                return null;

            return new TimeTrialLeaderboardEntry
            {
                RunId = RunRouteIdentity.Visit(player, encounter.Name, encounter.Difficutly, encounter.NumberOfPlayer, entered),
                EnteredAt = entered,
                BossFight = boss, Encounter = encounter.Name, Difficulty = encounter.Difficutly,
                PlayerCount = encounter.NumberOfPlayer, PlayerName = player, Timestamp = end,
                StartSeconds = (int)(start - entered).TotalSeconds,
                EndSeconds = (int)(end - entered).TotalSeconds
            };
        }
    }

    public async Task RecordAsync(Combat combat)
    {
        if (combat.IsReplay) return;
        try
        {
            var state = CombatLogStateBuilder.CurrentState;
            var entered = state.EncounterEnteredInfo.Where(e => e.Key <= combat.StartTime)
                .OrderByDescending(e => e.Key).FirstOrDefault();
            if (!ReferenceEquals(entered.Value, combat.ParentEncounter))
                return;
            var entry = CreateEntry(combat.ParentEncounter, combat.EncounterBossDifficultyParts.Item1,
                combat.WasBossKilled, EncounterSessionGrouping.VisitStart(state.EncounterEnteredInfo, combat.StartTime),
                combat.StartTime, combat.EndTime, state.LocalPlayer?.Name ?? "");
            if (entry != null)
                await API_Connection.AddNewTimeTrialEntry(entry).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logging.LogError("Could not submit encounter time trial: " + ex.Message);
        }
    }
}
