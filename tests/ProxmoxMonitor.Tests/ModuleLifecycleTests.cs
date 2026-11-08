using AshboardVision.Sdk.Testing;
using ProxmoxMonitor.Tests.Support;
using Trofeo.ModuleKit;
namespace ProxmoxMonitor.Tests;
public sealed class ModuleLifecycleTests
{
    [Fact]
    public async Task DeniedNetwork_DoesNotRequest()
    {
        var handler=new FakeProxmoxHandler();TestableProxmoxModule.Handler.Value=handler;TestableProxmoxModule.SkipStart.Value=false;
        await using var host=new ModuleTestHost<TestableProxmoxModule>(Path.Combine(AppContext.BaseDirectory,"manifest.json"),PveFixtures.Json("""{"address":"https://pve.example:8006"}"""),[]);
        await host.StartAsync();Assert.Equal(ModuleHealth.Offline,host.Health);Assert.Contains("permission",host.StatusMessage,StringComparison.OrdinalIgnoreCase);Assert.Empty(handler.Requests);
    }
    [Fact]
    public async Task Start_DoesNotWaitForNetworkAndDisposeCancelsPendingRequest()
    {
        var handler=new FakeProxmoxHandler();TestableProxmoxModule.Handler.Value=handler;TestableProxmoxModule.SkipStart.Value=true;
        TestableProxmoxModule? module=null;TestableProxmoxModule.Capture.Value=m=>module=m;
        await using var host=new ModuleTestHost<TestableProxmoxModule>(Path.Combine(AppContext.BaseDirectory,"manifest.json"),PveFixtures.Json("""{"address":"https://pve.example:8006"}"""));
        host.Secrets.Set("token",ConnectionHarness.Token);await host.StartAsync();Assert.True((await module!.RunActionAsync("testConnection",CancellationToken.None)).Ok);
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var canceled=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        handler.Respond=async(r,ct)=>{if(r.RequestUri!.AbsolutePath.EndsWith("/status")){entered.TrySetResult();try{await Task.Delay(Timeout.Infinite,ct);}catch(OperationCanceledException){canceled.TrySetResult();throw;}}return FakeProxmoxHandler.JsonResponse(PveFixtures.ForRoute(r.RequestUri.PathAndQuery),r);};
        var start=module.StartProduction(CancellationToken.None);Assert.True(start.IsCompletedSuccessfully);await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await host.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(4));await canceled.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }
}
