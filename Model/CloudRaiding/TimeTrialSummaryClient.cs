using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace SWTORCombatParser.Model.CloudRaiding;

public sealed class TimeTrialSummary
{
    [JsonRequired] public int SampleCount { get; set; }
    [JsonRequired] public int StartSeconds { get; set; }
    [JsonRequired] public int EndSeconds { get; set; }

    // Same nearest-rank definition as PostgreSQL percentile_disc(0.1).
    // Start/end are independent distributions; preserve ties and their frequency.
    public static TimeTrialSummary FromEntries(IReadOnlyCollection<TimeTrialLeaderboardEntry> entries)
    {
        var valid=entries.Where(entry=>entry is not null && entry.StartSeconds>=0 &&
            entry.EndSeconds>=entry.StartSeconds && entry.EndSeconds>0).ToArray();
        if (valid.Length==0) return new();
        int index=(int)Math.Ceiling(valid.Length*.1)-1;
        return new() { SampleCount=valid.Length,
            StartSeconds=valid.Select(x=>x.StartSeconds).OrderBy(x=>x).ElementAt(index),
            EndSeconds=valid.Select(x=>x.EndSeconds).OrderBy(x=>x).ElementAt(index) };
    }
}

public sealed class TimeTrialSummaryClient
{
    private sealed record Key(string Api,string Boss,string Encounter,string Difficulty,string Players);
    private sealed record Saved(TimeTrialSummary Value,DateTimeOffset FreshUntil,DateTimeOffset ValidUntil);
    private readonly HttpClient _http;
    private readonly TimeProvider _clock;
    private readonly object _sync=new();
    private readonly Dictionary<Key,Saved> _cache=new();
    private readonly Dictionary<Key,Task<TimeTrialSummary>> _flights=new();
    private readonly SemaphoreSlim _slots=new(4);
    public TimeTrialSummaryClient(HttpClient http,TimeProvider? clock=null) { _http=http; _clock=clock??TimeProvider.System; }

    public Task<TimeTrialSummary> GetAsync(string api,string boss,string encounter,string difficulty,string players,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // A view abandoning a request must not cancel a shared request used by another view.
        return GetAsync(api,boss,encounter,difficulty,players).WaitAsync(cancellationToken);
    }

    public Task<TimeTrialSummary> GetAsync(string api,string boss,string encounter,string difficulty,string players)
    {
        if (new[]{api,boss,encounter,difficulty,players}.Any(string.IsNullOrWhiteSpace))
            return Task.FromResult(new TimeTrialSummary());
        var key=new Key(api.TrimEnd('/'),boss,encounter,difficulty,players);
        lock (_sync)
        {
            var now=_clock.GetUtcNow();
            if (_cache.TryGetValue(key,out var saved) && saved.FreshUntil>now && saved.ValidUntil>now) return Task.FromResult(saved.Value);
            if (_flights.TryGetValue(key,out var flight)) return flight;
            if (_flights.Count>=128) return Task.FromResult(new TimeTrialSummary());
            var completion=new TaskCompletionSource<TimeTrialSummary>(TaskCreationOptions.RunContinuationsAsynchronously);
            _flights[key]=completion.Task;
            _=LoadAsync(key,completion);
            return completion.Task;
        }
    }
    private async Task LoadAsync(Key key,TaskCompletionSource<TimeTrialSummary> completion)
    {
        TimeTrialSummary value=new(); bool success=false, acquired=false;
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(8));
        try
        {
            await _slots.WaitAsync(timeout.Token).ConfigureAwait(false); acquired=true;
            var query="?bossfightName="+Uri.EscapeDataString(key.Boss)+"&encounter="+Uri.EscapeDataString(key.Encounter)+
                "&difficulty="+Uri.EscapeDataString(key.Difficulty)+"&playerCount="+Uri.EscapeDataString(key.Players);
            using var response=await _http.GetAsync(key.Api+"/trial_leaderboard/summary"+query,timeout.Token).ConfigureAwait(false);
            if(response.StatusCode==HttpStatusCode.NotFound)
            {
                // Only an older server triggers the expensive fallback, never a transient outage.
                using var legacy=await _http.GetAsync(key.Api+"/trial_leaderboard/getEntriesForBossFull"+query,timeout.Token).ConfigureAwait(false);
                legacy.EnsureSuccessStatusCode();
                var entries=await legacy.Content.ReadFromJsonAsync<List<RawTimeTrialEntry?>>(cancellationToken:timeout.Token).ConfigureAwait(false)
                    ?? throw new JsonException("Missing time-trial entries.");
                // The legacy endpoint may use prefix matching. Require the exact requested cohort,
                // and distinguish missing numeric fields from a valid zero-second starting split.
                value=TimeTrialSummary.FromEntries(entries.Where(entry=>entry is not null &&
                    entry.StartSeconds.HasValue && entry.EndSeconds.HasValue &&
                    string.Equals(entry.BossFight,key.Boss,StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(entry.Encounter,key.Encounter,StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(entry.Difficulty,key.Difficulty,StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(entry.PlayerCount,key.Players,StringComparison.OrdinalIgnoreCase))
                    .Select(entry=>new TimeTrialLeaderboardEntry
                    { StartSeconds=entry!.StartSeconds!.Value,EndSeconds=entry.EndSeconds!.Value }).ToArray());
            }
            else
            {
                response.EnsureSuccessStatusCode();
                value=await response.Content.ReadFromJsonAsync<TimeTrialSummary>(cancellationToken:timeout.Token).ConfigureAwait(false)
                    ?? throw new JsonException("Missing time-trial summary.");
                if(value.SampleCount<0 || value.StartSeconds<0 || value.EndSeconds<value.StartSeconds ||
                    (value.SampleCount>0 && value.EndSeconds==0) ||
                    (value.SampleCount==0 && (value.StartSeconds!=0 || value.EndSeconds!=0)))
                    throw new JsonException("Invalid time-trial summary.");
            }
            success=true;
        }
        catch(Exception e) when(e is HttpRequestException or OperationCanceledException or JsonException or NotSupportedException or ArgumentException or InvalidOperationException)
        { /* Retry briefly; retain only same-key last-good data, without starting a legacy download storm. */ }
        finally { if(acquired) _slots.Release(); }
        lock(_sync)
        {
            var now=_clock.GetUtcNow(); var validUntil=now.AddHours(1);
            if(!success)
            {
                if(_cache.TryGetValue(key,out var old) && old.ValidUntil>now) { value=old.Value;validUntil=old.ValidUntil; }
                else { value=new();validUntil=now.AddSeconds(10); }
            }
            if(_cache.Count>=128)
                foreach(var oldest in _cache.OrderBy(x=>x.Value.FreshUntil).Take(32).Select(x=>x.Key).ToArray()) _cache.Remove(oldest);
            _cache[key]=new(value,now.AddSeconds(success?600:10),validUntil);
            _flights.Remove(key);
        }
        completion.TrySetResult(value);
    }

    private sealed class RawTimeTrialEntry
    {
        public RawTimeTrialEntry() { }
        public string? BossFight { get; set; }
        public string? Encounter { get; set; }
        public string? Difficulty { get; set; }
        public string? PlayerCount { get; set; }
        public int? StartSeconds { get; set; }
        public int? EndSeconds { get; set; }
    }
}
