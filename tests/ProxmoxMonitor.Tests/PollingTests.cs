using Microsoft.Extensions.Time.Testing;
using ProxmoxMonitor.Api;
using ProxmoxMonitor.Configuration;
using ProxmoxMonitor.Monitoring;
using ProxmoxMonitor.Tests.Support;
namespace ProxmoxMonitor.Tests;
public sealed class PollingTests
{
    private static ProxmoxSettings Settings(){SettingsReader.TryRead(PveFixtures.Json("""{"address":"https://pve.example:8006"}"""),out var s,out _);return s!;}
    private sealed class Source : IMonitoringSource
    {
        public Guid? BindingRevision{get;set;}=SnapshotTests.Revision;
        internal Func<CancellationToken,Task<ProbeBatch>> Read=ct=>Task.FromResult(SnapshotTests.Batch());
        internal int Calls;
        public Task<ProbeBatch> CollectAsync(CancellationToken ct){Interlocked.Increment(ref Calls);return Read(ct);}
    }
    internal static async Task Until(Func<bool> condition)
    {
        for(var i=0;i<500&&!condition();i++)await Task.Delay(5);
        Assert.True(condition(),"L’état attendu n’est pas arrivé.");
    }
    [Fact]
    public async Task Poll_WaitsAfterCompletionAndNeverOverlaps()
    {
        var time=new FakeTimeProvider(SnapshotTests.Now);using var cts=new CancellationTokenSource();
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var source=new Source();
        source.Read=async ct=>{await release.Task.WaitAsync(ct);return SnapshotTests.Batch();};
        var loop=new MonitoringLoop(source,Settings(),time);var running=loop.RunAsync(cts.Token);
        await Until(()=>source.Calls==1);time.Advance(TimeSpan.FromSeconds(3));Assert.Equal(1,source.Calls);
        release.SetResult();await Until(()=>loop.Current.Host.Value is not null);
        time.Advance(TimeSpan.FromSeconds(9));Assert.Equal(1,source.Calls);
        time.Advance(TimeSpan.FromSeconds(1));await Until(()=>source.Calls==2);
        await cts.CancelAsync();await running.WaitAsync(TimeSpan.FromSeconds(2));
    }
    [Fact]
    public async Task Collection_StopsAtEightSeconds()
    {
        var time=new FakeTimeProvider(SnapshotTests.Now);using var cts=new CancellationTokenSource();var source=new Source();
        source.Read=async ct=>{await Task.Delay(Timeout.Infinite,ct);return SnapshotTests.Batch();};
        var loop=new MonitoringLoop(source,Settings(),time);var running=loop.RunAsync(cts.Token);
        await Until(()=>source.Calls==1);time.Advance(TimeSpan.FromSeconds(8));
        await Until(()=>loop.Current.Host.Failure?.Kind==ApiFailureKind.Timeout);
        await cts.CancelAsync();await running.WaitAsync(TimeSpan.FromSeconds(2));
    }
    [Theory]
    [InlineData(10,30)]
    [InlineData(60,120)]
    public void Freshness_UsesTheLargerLimit(int refresh,int limit)
    {
        Assert.False(FreshnessPolicy.IsExpired(SnapshotTests.Now,SnapshotTests.Now.AddSeconds(limit),TimeSpan.FromSeconds(refresh)));
        Assert.True(FreshnessPolicy.IsExpired(SnapshotTests.Now,SnapshotTests.Now.AddSeconds(limit).AddMilliseconds(1),TimeSpan.FromSeconds(refresh)));
        Assert.True(FreshnessPolicy.IsExpired(null,SnapshotTests.Now,TimeSpan.FromSeconds(refresh)));
    }
    [Fact]
    public async Task FatalFailure_WaitsForNewBindingRevision()
    {
        var time=new FakeTimeProvider(SnapshotTests.Now);using var cts=new CancellationTokenSource();var source=new Source();
        source.Read=ct=>Task.FromResult(SnapshotTests.Batch() with {TerminalFailure=new(ApiFailureKind.Unauthorized,"Jeton refusé.")});
        var loop=new MonitoringLoop(source,Settings(),time);var running=loop.RunAsync(cts.Token);
        await Until(()=>loop.Current.GeneralError is not null);time.Advance(TimeSpan.FromSeconds(60));Assert.Equal(1,source.Calls);
        source.BindingRevision=Guid.NewGuid();source.Read=ct=>Task.FromResult(SnapshotTests.Batch() with {Revision=source.BindingRevision.Value});
        time.Advance(TimeSpan.FromSeconds(10));await Until(()=>loop.Current.Host.Value is not null);Assert.Equal(2,source.Calls);
        await cts.CancelAsync();await running.WaitAsync(TimeSpan.FromSeconds(2));
    }
    [Fact]
    public async Task OldRevision_IsNotPublished()
    {
        var time=new FakeTimeProvider(SnapshotTests.Now);using var cts=new CancellationTokenSource();var source=new Source();
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        source.Read=async ct=>{if(source.Calls==1){entered.SetResult();await release.Task.WaitAsync(ct);return SnapshotTests.Batch();}await Task.Delay(Timeout.Infinite,ct);return SnapshotTests.Batch();};
        var loop=new MonitoringLoop(source,Settings(),time);var running=loop.RunAsync(cts.Token);await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        source.BindingRevision=Guid.NewGuid();release.SetResult();await Until(()=>source.Calls==2);
        Assert.Null(loop.Current.Host.Value);await cts.CancelAsync();await running.WaitAsync(TimeSpan.FromSeconds(2));
    }
    [Fact]
    public async Task TransientFailure_BackoffCapsAtSixtySeconds()
    {
        var time=new FakeTimeProvider(SnapshotTests.Now);using var cts=new CancellationTokenSource();var source=new Source();
        var failure=ApiReadResult.Fail(ApiFailureKind.Network,"Réseau indisponible.");source.Read=ct=>Task.FromResult(SnapshotTests.Batch() with {Host=failure});
        var loop=new MonitoringLoop(source,Settings(),time);int published=0;loop.SnapshotChanged+=_=>Interlocked.Increment(ref published);
        var running=loop.RunAsync(cts.Token);await Until(()=>Volatile.Read(ref published)==1);
        time.Advance(TimeSpan.FromSeconds(19));Assert.Equal(1,source.Calls);time.Advance(TimeSpan.FromSeconds(1));await Until(()=>Volatile.Read(ref published)==2);Assert.Equal(2,source.Calls);
        time.Advance(TimeSpan.FromSeconds(40));await Until(()=>Volatile.Read(ref published)==3);Assert.Equal(3,source.Calls);
        source.Read=ct=>Task.FromResult(SnapshotTests.Batch());time.Advance(TimeSpan.FromSeconds(60));await Until(()=>Volatile.Read(ref published)==4);Assert.Null(loop.Current.Host.Failure);
        time.Advance(TimeSpan.FromSeconds(10));await Until(()=>Volatile.Read(ref published)==5);Assert.Equal(5,source.Calls);await cts.CancelAsync();await running.WaitAsync(TimeSpan.FromSeconds(2));
    }
}
