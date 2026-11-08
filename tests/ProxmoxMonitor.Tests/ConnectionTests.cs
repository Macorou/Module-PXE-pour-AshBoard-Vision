using ProxmoxMonitor.Connection;
using ProxmoxMonitor.Configuration;
using ProxmoxMonitor.Tests.Support;
namespace ProxmoxMonitor.Tests;
public sealed class ConnectionTests
{
    [Fact]
    public async Task SuccessfulTest_ReplacesRevisionAndDiscoversStorage()
    {
        using var h=new ConnectionHarness();var coordinator=h.Coordinator();
        Assert.True((await coordinator.TestAsync(CancellationToken.None)).Ok);
        var binding=h.Store.ReadBinding();Assert.NotNull(binding);
        Assert.Equal("pve",binding.Node);Assert.DoesNotContain("fake-secret",h.Secrets.Get("connection"));
        Assert.Equal("local-lvm",Assert.Single(h.Catalog.Read(binding)!.Storages).Id);
        Assert.True((await coordinator.TestAsync(CancellationToken.None)).Ok);
        Assert.NotEqual(binding.Revision,h.Store.ReadBinding()!.Revision);
    }
    [Fact]
    public async Task ChangedOriginOrToken_RequiresTest()
    {
        using var h=new ConnectionHarness();var coordinator=h.Coordinator();
        Assert.True((await coordinator.TestAsync(CancellationToken.None)).Ok);
        ConnectionOrigin.TryParse("https://other.example",out var other,out _);
        var requests=h.Handler.Requests.Count;
        Assert.Null(h.Coordinator(h.Settings with {Origin=other!}).OpenActive(out _));
        h.Secrets.Set("token","monitor@pve!screen=replacement");
        Assert.Null(coordinator.OpenActive(out _));Assert.Equal(requests,h.Handler.Requests.Count);
    }
    [Fact]
    public async Task ChangedMode_RequiresTest()
    {
        using var h=new ConnectionHarness();Assert.True((await h.Coordinator().TestAsync(CancellationToken.None)).Ok);
        Assert.Null(h.Coordinator(h.Settings with {AllowSelfSigned=true}).OpenActive(out _));
    }
    [Fact]
    public async Task QuotaRefused_DoesNotCommit()
    {
        using var h=new ConnectionHarness();for(var i=0;i<63;i++)h.Secrets.Set("filler"+i,"x");
        Assert.False((await h.Coordinator().TestAsync(CancellationToken.None)).Ok);
        Assert.Null(h.Store.ReadBinding());
    }
    [Theory]
    [InlineData("[]")]
    [InlineData("""[{"node":"pve"},{"node":"other"}]""")]
    public async Task NoNodeOrMultipleNodes_AreRejected(string nodes)
    {
        using var h=new ConnectionHarness();
        h.Handler.Respond=(r,ct)=>Task.FromResult(FakeProxmoxHandler.JsonResponse(r.RequestUri!.AbsolutePath.EndsWith("/nodes")?PveFixtures.Json(nodes):PveFixtures.ForRoute(r.RequestUri.PathAndQuery),r));
        Assert.False((await h.Coordinator().TestAsync(CancellationToken.None)).Ok);
        Assert.Null(h.Store.ReadBinding());
        Assert.DoesNotContain(h.Handler.Requests,r=>r.Uri.AbsolutePath.EndsWith("/qemu"));
    }
    [Fact]
    public async Task TokenChangedDuringTest_DoesNotCommit()
    {
        using var h=new ConnectionHarness();var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Handler.Respond=async (r,ct)=>{if(r.Headers.Contains("Authorization")&&r.RequestUri!.AbsolutePath.EndsWith("/version")){entered.TrySetResult();await release.Task.WaitAsync(ct);}return FakeProxmoxHandler.JsonResponse(PveFixtures.ForRoute(r.RequestUri!.PathAndQuery),r);};
        var pending=h.Coordinator().TestAsync(CancellationToken.None);await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        h.Secrets.Set("token","monitor@pve!screen=changed");release.SetResult();
        Assert.False((await pending).Ok);Assert.Null(h.Store.ReadBinding());
    }
    [Fact]
    public async Task TokenChangedDuringTransportProbe_DoesNotAuthenticate()
    {
        using var h=new ConnectionHarness();
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Handler.Respond=async(request,ct)=>
        {
            if(!request.Headers.Contains("Authorization"))
            {entered.TrySetResult();await release.Task.WaitAsync(ct);}
            return FakeProxmoxHandler.JsonResponse(PveFixtures.ForRoute(request.RequestUri!.PathAndQuery),request);
        };
        var pending=h.Coordinator().TestAsync(CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        h.Secrets.Set("token","other@pve!screen=other-server-secret");release.SetResult();
        Assert.False((await pending.WaitAsync(TimeSpan.FromSeconds(2))).Ok);
        Assert.DoesNotContain(h.Handler.Requests,r=>r.HasAuthorization);
        Assert.Null(h.Store.ReadBinding());
    }
    [Fact]
    public async Task TokenChangedWhileTestWaits_DoesNotSendTheReplacementOrCommit()
    {
        using var h=new ConnectionHarness();var coordinator=h.Coordinator();
        Assert.True((await coordinator.TestAsync(CancellationToken.None)).Ok);
        var revision=h.Store.ReadBinding()!.Revision;
        using var lease=coordinator.OpenActive(out _);Assert.NotNull(lease);
        var count=h.Handler.Requests.Count;
        var pending=coordinator.TestAsync(CancellationToken.None);
        Assert.False(pending.IsCompleted);
        h.Secrets.Set("token","other@pve!screen=other-server-secret");
        lease.Dispose();
        var result=await pending.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(result.Ok);
        Assert.Equal(count,h.Handler.Requests.Count);
        Assert.Equal(revision,h.Store.ReadBinding()!.Revision);
    }
    [Fact]
    public async Task ConcurrentTests_AreSerialized()
    {
        using var h=new ConnectionHarness();var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);int active=0,max=0;
        h.Handler.Respond=async(r,ct)=>{if(r.Headers.Contains("Authorization")&&r.RequestUri!.AbsolutePath.EndsWith("/version")){var count=Interlocked.Increment(ref active);max=Math.Max(max,count);entered.TrySetResult();await release.Task.WaitAsync(ct);Interlocked.Decrement(ref active);}return FakeProxmoxHandler.JsonResponse(PveFixtures.ForRoute(r.RequestUri!.PathAndQuery),r);};
        var first=h.Coordinator().TestAsync(CancellationToken.None);await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var second=h.Coordinator().TestAsync(CancellationToken.None);Assert.Equal(1,active);release.SetResult();
        var results=await Task.WhenAll(first,second).WaitAsync(TimeSpan.FromSeconds(3));Assert.All(results,r=>Assert.True(r.Ok));Assert.Equal(1,max);
    }
    [Fact]
    public async Task LeaseBecomesObsoleteWhenTokenChanges()
    {
        using var h=new ConnectionHarness();var coordinator=h.Coordinator();Assert.True((await coordinator.TestAsync(CancellationToken.None)).Ok);
        using var lease=coordinator.OpenActive(out _);Assert.NotNull(lease);Assert.True(lease.IsCurrent());
        h.Secrets.Set("token","monitor@pve!screen=changed");Assert.False(lease.IsCurrent());
    }
}
