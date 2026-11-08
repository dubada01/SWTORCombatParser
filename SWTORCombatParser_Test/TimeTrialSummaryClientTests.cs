using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using SWTORCombatParser.Model.CloudRaiding;
namespace SWTORCombatParser_Test;

public class TimeTrialSummaryClientTests
{
    private sealed class Handler(Func<HttpRequestMessage,Task<HttpResponseMessage>> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>send(request); }
    private sealed class Clock : TimeProvider
    { public DateTimeOffset Now=DateTimeOffset.UtcNow;public override DateTimeOffset GetUtcNow()=>Now; }
    private static HttpResponseMessage Json(object value)=>new(HttpStatusCode.OK){Content=JsonContent.Create(value)};
    private static Task<TimeTrialSummary> Read(TimeTrialSummaryClient client,string boss="Boss",string difficulty="Story",string api="https://example.test/api")=>
        client.GetAsync(api,boss,"Raid",difficulty,"8");

    [TestCase(0)] [TestCase(1)] [TestCase(9)] [TestCase(10)] [TestCase(11)] [TestCase(20)] [TestCase(101)]
    public void SummaryMatchesLegacyIndependentNearestRanksIncludingTies(int count)
    {
        var entries=Enumerable.Range(0,count).Select(i=>new TimeTrialLeaderboardEntry{StartSeconds=i/3,EndSeconds=1000-i}).Reverse().ToList();
        var summary=TimeTrialSummary.FromEntries(entries);
        int index=Math.Max(0,(int)Math.Ceiling(count*.1)-1);
        Assert.That(summary.SampleCount,Is.EqualTo(count));
        Assert.That(summary.StartSeconds,Is.EqualTo(count==0?0:entries.OrderBy(x=>x.StartSeconds).ElementAt(index).StartSeconds));
        Assert.That(summary.EndSeconds,Is.EqualTo(count==0?0:entries.OrderBy(x=>x.EndSeconds).ElementAt(index).EndSeconds));
    }
    [Test]
    public async Task ConcurrentRequestsShareOneSummaryAndReuseCachedValue()
    {
        int calls=0; var gate=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var http=new HttpClient(new Handler(async r=>{Interlocked.Increment(ref calls);Assert.That(r.RequestUri!.AbsolutePath,Does.EndWith("/summary"));await gate.Task;return Json(new TimeTrialSummary{SampleCount=20,StartSeconds=2,EndSeconds=7});}));
        var client=new TimeTrialSummaryClient(http);
        var tasks=Enumerable.Range(0,20).Select(_=>Read(client)).ToArray();gate.SetResult(true);
        Assert.That((await Task.WhenAll(tasks)).All(x=>x.EndSeconds==7),Is.True);
        Assert.That((await Read(client)).SampleCount,Is.EqualTo(20));Assert.That(calls,Is.EqualTo(1));
    }
    [Test]
    public async Task OnlyMissingEndpointFallsBackAndFiltersAreEncoded()
    {
        var paths=new List<string>();
        using var http=new HttpClient(new Handler(r=>{
            paths.Add(r.RequestUri!.AbsolutePath);
            Assert.That(Uri.UnescapeDataString(r.RequestUri.Query),Does.Contain("bossfightName=Boss # &"));
            return Task.FromResult(paths.Count==1?new HttpResponseMessage(HttpStatusCode.NotFound):Json(new[]{new TimeTrialLeaderboardEntry
                {BossFight="Boss # &",Encounter="Raid",Difficulty="Story",PlayerCount="8",StartSeconds=4,EndSeconds=12}}));}));
        var client=new TimeTrialSummaryClient(http);
        Assert.That((await Read(client,"Boss # &")).EndSeconds,Is.EqualTo(12));
        await Read(client,"Boss # &");
        Assert.That(paths,Has.Count.EqualTo(2));Assert.That(paths[1],Does.EndWith("/getEntriesForBossFull"));
    }
    [Test]
    public async Task CohortsAndServersHaveSeparateCaches()
    {
        int calls=0;using var http=new HttpClient(new Handler(_=>{calls++;return Task.FromResult(Json(new TimeTrialSummary{SampleCount=1,StartSeconds=calls,EndSeconds=calls}));}));
        var client=new TimeTrialSummaryClient(http);
        await Read(client);await Read(client,difficulty:"Veteran");await Read(client,api:"https://other.test/api");await Read(client);
        Assert.That(calls,Is.EqualTo(3));
    }
    [Test]
    public async Task FailuresUseBoundedLastGoodAndDoNotDownloadLegacyLists()
    {
        int calls=0;var clock=new Clock();using var http=new HttpClient(new Handler(_=>Task.FromResult(++calls==1?Json(new TimeTrialSummary{SampleCount=3,StartSeconds=1,EndSeconds=9}):new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))));
        var client=new TimeTrialSummaryClient(http,clock);await Read(client);clock.Now=clock.Now.AddMinutes(11);
        Assert.That((await Read(client)).EndSeconds,Is.EqualTo(9));await Read(client);Assert.That(calls,Is.EqualTo(2));
        clock.Now=clock.Now.AddHours(1);Assert.That((await Read(client)).SampleCount,Is.Zero);Assert.That(calls,Is.EqualTo(3));
    }
    [Test]
    public async Task MalformedSummariesAreNotAcceptedOrRetriedImmediately()
    {
        int calls=0;using var http=new HttpClient(new Handler(_=>{calls++;return Task.FromResult(Json(new{sampleCount=10}));}));
        var client=new TimeTrialSummaryClient(http);Assert.That((await Read(client)).SampleCount,Is.Zero);await Read(client);Assert.That(calls,Is.EqualTo(1));
    }
    [Test]
    public async Task InvalidConfigurationCompletesWithoutLeavingAStuckFlight()
    {
        using var http=new HttpClient(new Handler(_=>throw new InvalidOperationException("unexpected request")));
        var client=new TimeTrialSummaryClient(http);
        var result=await Read(client,api:"relative").WaitAsync(TimeSpan.FromSeconds(1));
        Assert.That(result.SampleCount,Is.Zero);
        Assert.That((await Read(client,api:"relative")).SampleCount,Is.Zero);
    }
    [Test]
    public async Task AtMostFourDifferentCohortsUseTheNetworkAtOnce()
    {
        int active=0,maximum=0;var gate=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var http=new HttpClient(new Handler(async _=>{var n=Interlocked.Increment(ref active);maximum=Math.Max(maximum,n);await gate.Task;Interlocked.Decrement(ref active);return Json(new TimeTrialSummary());}));
        var client=new TimeTrialSummaryClient(http);var tasks=Enumerable.Range(0,12).Select(i=>Read(client,"Boss"+i)).ToArray();
        Assert.That(active,Is.EqualTo(4));gate.SetResult(true);await Task.WhenAll(tasks);Assert.That(maximum,Is.EqualTo(4));
    }

    [TestCase(-1, 0, 0)]
    [TestCase(2, -1, 30)]
    [TestCase(2, 30, 10)]
    [TestCase(2, 0, 0)]
    [TestCase(0, 0, 30)]
    public async Task InvalidTimingSummariesNeverBecomeVisibleOrTriggerLegacyDownloads(int count, int start, int end)
    {
        int calls=0;
        using var http=new HttpClient(new Handler(request=>
        {
            calls++;
            Assert.That(request.RequestUri!.AbsolutePath,Does.EndWith("/summary"));
            return Task.FromResult(Json(new TimeTrialSummary {SampleCount=count,StartSeconds=start,EndSeconds=end}));
        }));
        var client=new TimeTrialSummaryClient(http);
        Assert.That((await Read(client)).SampleCount,Is.Zero);
        Assert.That((await Read(client)).SampleCount,Is.Zero);
        Assert.That(calls,Is.EqualTo(1));
    }

    [Test]
    public async Task LegacyFallbackFiltersPrefixMatchesMalformedFieldsAndInvalidTimesBeforePercentiles()
    {
        static Dictionary<string,object> Entry(int start, int end) => new()
        {
            ["bossFight"]="Boss",["encounter"]="Raid",["difficulty"]="Story",["playerCount"]="8",
            ["startSeconds"]=start,["endSeconds"]=end
        };
        var rows=Enumerable.Range(10,10).Select(value=>(object)Entry(value,value+20)).ToList();
        foreach(var field in new[]{"bossFight","encounter","difficulty","playerCount"})
        {
            var foreign=Entry(0,1);
            foreign[field]=(string)foreign[field]+" extra";
            rows.Add(foreign);
            var missing=Entry(0,1);
            missing.Remove(field);
            rows.Add(missing);
        }
        foreach(var field in new[]{"startSeconds","endSeconds"})
        {
            var missing=Entry(0,1);
            missing.Remove(field);
            rows.Add(missing);
        }
        rows.Add(Entry(-10,1));
        rows.Add(Entry(20,1));
        rows.Add(Entry(0,0));
        rows.Add(null);
        int calls=0;
        using var http=new HttpClient(new Handler(_=>Task.FromResult(++calls==1
            ?new HttpResponseMessage(HttpStatusCode.NotFound):Json(rows))));
        var summary=await Read(new TimeTrialSummaryClient(http));
        Assert.Multiple(()=>
        {
            Assert.That(summary.SampleCount,Is.EqualTo(10));
            Assert.That(summary.StartSeconds,Is.EqualTo(10));
            Assert.That(summary.EndSeconds,Is.EqualTo(30));
            Assert.That(calls,Is.EqualTo(2));
        });
    }

    [Test]
    public async Task CancellingOneConsumerDoesNotCancelTheSharedLeaderboardRequest()
    {
        int calls=0;
        var gate=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var http=new HttpClient(new Handler(async _=>
        {
            Interlocked.Increment(ref calls);
            await gate.Task;
            return Json(new TimeTrialSummary {SampleCount=20,StartSeconds=10,EndSeconds=30});
        }));
        var client=new TimeTrialSummaryClient(http);
        using var cancellation=new CancellationTokenSource();
        var cancelled=client.GetAsync("https://example.test/api","Boss","Raid","Story","8",cancellation.Token);
        var retained=Read(client);
        cancellation.Cancel();
        Assert.CatchAsync<OperationCanceledException>(async()=>await cancelled);
        gate.SetResult(true);
        Assert.That((await retained).EndSeconds,Is.EqualTo(30));
        Assert.That(calls,Is.EqualTo(1));
    }
}
