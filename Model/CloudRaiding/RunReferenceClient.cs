using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Orbs.RunRace;
using SWTORCombatParser.Model.RunRace;
using SWTORCombatParser.Utilities;

namespace SWTORCombatParser.Model.CloudRaiding;

public sealed record RunReferenceRoute(string Key, string[] Bosses, int SampleCount, RunGhost? PersonalBest, RunGhost? Latest);
public sealed record RunReferences(IReadOnlyList<RunReferenceRoute> Routes, bool Ready = true);

/// <summary>Small precomputed references, never a download of all players' raw boss entries.</summary>
public sealed class RunReferenceClient(HttpClient http)
{
    public async Task<RunReferences?> GetAsync(string api, RunCohort cohort, string player, DateTime entered, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(player)) return null;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        string Escape(string value) => Uri.EscapeDataString(value);
        var query = $"?encounter={Escape(cohort.Encounter)}&difficulty={Escape(cohort.Difficulty)}&playerCount={Escape(cohort.GroupSize)}" +
            $"&playerName={Escape(player)}&excludeEntered={Escape(entered.ToString("O", CultureInfo.InvariantCulture))}";
        try
        {
            using var response = await http.GetAsync(api.TrimEnd('/') + "/trial_leaderboard/runs" + query, timeout.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var references = await response.Content.ReadFromJsonAsync<RunReferences>(cancellationToken: timeout.Token).ConfigureAwait(false);
            if (references?.Routes == null || references.Routes.Count > 128) return null;
            bool Valid(RunGhost? run, string key) => run is not null && RunGhostStore.IsValid(run) && run.IsComplete &&
                run.Cohort == cohort && string.Equals(run.Player, player, StringComparison.OrdinalIgnoreCase) &&
                Math.Abs((run.Entered - entered).TotalSeconds) >= 1 && run.RouteKey == key &&
                (RunRouteCatalog.Find(cohort.Encounter)?.IsComplete(run.Splits.OrderBy(s => s.EndSeconds).Select(s => s.Boss)) ?? true);
            return references with { Routes = references.Routes.Where(r => r is not null && r.Bosses is { Length: > 0 and <= 64 } &&
                    r.Bosses.All(b => !string.IsNullOrWhiteSpace(b)) && r.Bosses.Distinct().Count() == r.Bosses.Length &&
                    r.SampleCount > 0 && r.Key == RunRouteIdentity.Key(r.Bosses))
                .Select(r => r with { PersonalBest = Valid(r.PersonalBest, r.Key) ? r.PersonalBest : null,
                    Latest = Valid(r.Latest, r.Key) ? r.Latest : null }).ToArray() };
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException or NotSupportedException)
        {
            token.ThrowIfCancellationRequested();
            return null;
        }
    }
}

public static partial class API_Connection
{
    private static readonly RunReferenceClient RunReferencesClient = new(_client);
    public static Task<RunReferences?> GetRunReferences(RunCohort cohort, string player, DateTime entered, CancellationToken token) =>
        Settings.ReadSettingOfType<bool>("offline_mode") ? Task.FromResult<RunReferences?>(null)
            : RunReferencesClient.GetAsync(_apiPath, cohort, player, entered, token);
}
