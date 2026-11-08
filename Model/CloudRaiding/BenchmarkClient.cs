using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using SWTORCombatParser.Utilities;

namespace SWTORCombatParser.Model.CloudRaiding;

public sealed class BenchmarkMetric
{
    public int AlgorithmVersion { get; set; }
    public bool Available { get; set; }
    public string Status { get; set; } = "pending";
    public int SampleCount { get; set; }
    public DateTimeOffset? ComputedAt { get; set; }
    public double[] Thresholds { get; set; } = Array.Empty<double>();
    public int? Rank(double score)
    {
        if(!Available || AlgorithmVersion!=2 || SampleCount<=0 || Thresholds is null || Thresholds.Length!=100 || !double.IsFinite(score)) return null;
        int rank=0; double previous=double.NegativeInfinity;
        foreach(var value in Thresholds)
        {
            if(!double.IsFinite(value) || value<previous) return null;
            if(value<=score) rank++;
            previous=value;
        }
        return rank;
    }
}

public sealed class BenchmarkBundle
{
    public string Version { get; set; } = "";
    public Dictionary<string,BenchmarkMetric> Metrics { get; set; } = new();
}

public static partial class API_Connection
{
    private sealed record BenchmarkKey(string Api,string Version,string Boss,string Encounter,string Variant,string Filter);
    private sealed record SavedBenchmark(BenchmarkBundle? Value,DateTimeOffset Expires);
    private static readonly ConcurrentDictionary<BenchmarkKey,SavedBenchmark> BenchmarkCache=new();
    private static readonly ConcurrentDictionary<BenchmarkKey,Lazy<Task<BenchmarkBundle?>>> BenchmarkFlights=new();
    private static readonly SemaphoreSlim BenchmarkRequests=new(4);

    public static async Task<BenchmarkBundle?> GetBenchmarkBundle(string boss,string encounter,string variant,string filter)
    {
        if(Settings.ReadSettingOfType<bool>("offline_mode")) return new BenchmarkBundle();
        var key=new BenchmarkKey(_apiPath,Leaderboards._leaderboardVersion,boss,encounter,variant,filter);
        if(BenchmarkCache.TryGetValue(key,out var saved) && saved.Expires>DateTimeOffset.UtcNow) return saved.Value;
        var flight=BenchmarkFlights.GetOrAdd(key,k=>new Lazy<Task<BenchmarkBundle?>>(()=>LoadBenchmark(k),LazyThreadSafetyMode.ExecutionAndPublication));
        try { return await flight.Value.ConfigureAwait(false); }
        finally { ((ICollection<KeyValuePair<BenchmarkKey,Lazy<Task<BenchmarkBundle?>>>>)BenchmarkFlights).Remove(new(key,flight)); }
    }

    private static async Task<BenchmarkBundle?> LoadBenchmark(BenchmarkKey key)
    {
        BenchmarkBundle? value;
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(8));
        bool acquired=false;
        try
        {
            await BenchmarkRequests.WaitAsync(timeout.Token).ConfigureAwait(false); acquired=true;
            using var response=await _client.PostAsJsonAsync(key.Api+"/leaderboard/benchmarks",
                new {key.Boss,key.Encounter,key.Variant,key.Filter},timeout.Token).ConfigureAwait(false);
            if(response.StatusCode==HttpStatusCode.NotFound) value=null; // older server: use its established routes
            else
            {
                response.EnsureSuccessStatusCode();
                value=await response.Content.ReadFromJsonAsync<BenchmarkBundle>(cancellationToken:timeout.Token).ConfigureAwait(false) ?? new();
            }
        }
        catch(Exception e) when(e is System.Net.Http.HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            // Same-key last-good data is preferable to inventing a rank during an outage.
            value=BenchmarkCache.TryGetValue(key,out var old)?old.Value:new BenchmarkBundle();
            Remember(key,value,10);
            return value;
        }
        finally { if(acquired) BenchmarkRequests.Release(); }
        var pending=value is not null && (value.Metrics.Count==0 || value.Metrics.Values.Any(m=>m.Status=="pending"));
        Remember(key,value,pending?3:300);
        return value;
    }

    private static void Remember(BenchmarkKey key,BenchmarkBundle? value,int seconds)
    {
        if(BenchmarkCache.Count>=128)
            foreach(var expired in BenchmarkCache.OrderBy(x=>x.Value.Expires).Take(32)) BenchmarkCache.TryRemove(expired.Key,out _);
        BenchmarkCache[key]=new(value,DateTimeOffset.UtcNow.AddSeconds(seconds));
    }

    private static readonly TimeTrialSummaryClient TimeTrialSummaries = new(_client);
    public static Task<TimeTrialSummary> GetTimeTrialSummary(string boss,string encounter,string difficulty,string players)
    {
        if(Settings.ReadSettingOfType<bool>("offline_mode")) return Task.FromResult(new TimeTrialSummary());
        return TimeTrialSummaries.GetAsync(_apiPath,boss,encounter,difficulty,players);
    }
}
