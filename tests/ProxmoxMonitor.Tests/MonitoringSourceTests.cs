using Microsoft.Extensions.Time.Testing;
using ProxmoxMonitor.Api;
using ProxmoxMonitor.Connection;
using ProxmoxMonitor.Monitoring;
using ProxmoxMonitor.Tests.Support;
namespace ProxmoxMonitor.Tests;
public sealed class MonitoringSourceTests
{
    [Fact]
    public async Task GlobalBudget_PreservesCompletedCategoriesWhenOneRouteIsCanceled()
    {
        using var h=new ConnectionHarness();
        Assert.True((await h.Coordinator().TestAsync(CancellationToken.None)).Ok);
        var time=new FakeTimeProvider(SnapshotTests.Now);
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Handler.Respond=async(request,ct)=>
        {
            var path=request.RequestUri!.AbsolutePath;
            if(path.EndsWith("/permissions"))await Task.Delay(TimeSpan.FromSeconds(4),time,ct);
            if(path.EndsWith("/qemu")){entered.TrySetResult();await Task.Delay(Timeout.Infinite,ct);}
            return FakeProxmoxHandler.JsonResponse(PveFixtures.ForRoute(request.RequestUri.PathAndQuery),request);
        };
        HttpClient Factory(string? key)=>new(h.Handler,disposeHandler:false);
        var coordinator=new ConnectionCoordinator(h.Settings,h.Store,new CertificateTrust(Factory,h.Secrets,time),h.Catalog,Factory,time);
        var source=new ProxmoxMonitoringSource(coordinator,h.Store,h.Settings);
        var loop=new MonitoringLoop(source,h.Settings,time);
        using var stop=new CancellationTokenSource();var running=loop.RunAsync(stop.Token);
        try
        {
            time.Advance(TimeSpan.FromSeconds(4));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            time.Advance(TimeSpan.FromSeconds(4));
            await PollingTests.Until(()=>loop.Current.Vms.Failure?.Kind==ApiFailureKind.Timeout);
            Assert.Equal(23,loop.Current.Host.Value?.Cpu.Percent);
            Assert.Equal(1,loop.Current.Containers.Value?.Online);
            Assert.Single(loop.Current.Storages.Value!.Items);
            Assert.Null(loop.Current.Host.Failure);
            Assert.Null(loop.Current.Containers.Failure);
            Assert.Null(loop.Current.Storages.Failure);
            Assert.Null(loop.Current.GeneralError);
            Assert.Equal(time.GetUtcNow(),loop.Current.Host.LastSuccess);
            Assert.Equal(time.GetUtcNow(),loop.Current.Containers.LastSuccess);
            Assert.Equal(time.GetUtcNow(),loop.Current.Storages.LastSuccess);
            Assert.Null(loop.Current.Vms.LastSuccess);
        }
        finally {await stop.CancelAsync();await running.WaitAsync(TimeSpan.FromSeconds(2));}
    }
}
