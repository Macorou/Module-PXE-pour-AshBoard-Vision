using AshboardVision.Sdk.Testing;
using ProxmoxMonitor.Tests.Support;
using ProxmoxMonitor.Configuration;
using ProxmoxMonitor.Connection;
using System.Text.Json;
namespace ProxmoxMonitor.Tests;
public sealed class SettingsActionsTests
{
    [Fact]
    public async Task Lookup_BeforeDiscovery_LeavesDefaultToSdkAndAllowsRefresh()
    {
        TestableProxmoxModule.Handler.Value=new FakeProxmoxHandler();TestableProxmoxModule.SkipStart.Value=true;
        TestableProxmoxModule? module=null;TestableProxmoxModule.Capture.Value=m=>module=m;
        await using var host=new ModuleTestHost<TestableProxmoxModule>(Path.Combine(AppContext.BaseDirectory,"manifest.json"));
        await host.StartAsync();
        // SDK supplies emptyLabel itself and offers Reload only if results.Count == 0.
        Assert.Empty(await module!.LookupAsync("disk","",CancellationToken.None));
    }
    [Fact]
    public async Task Lookup_AfterDiscovery_ReturnsAllStoragesWithoutDuplicatingSdkDefault()
    {
        var handler=new FakeProxmoxHandler();
        handler.Respond=(r,ct)=>Task.FromResult(FakeProxmoxHandler.JsonResponse(r.RequestUri!.AbsolutePath.EndsWith("/storage")
            ?PveFixtures.Json("""[{"storage":"backup"},{"storage":"local"},{"storage":"local-lvm"},{"storage":"extra"}]""")
            :PveFixtures.ForRoute(r.RequestUri.PathAndQuery),r));
        TestableProxmoxModule.Handler.Value=handler;TestableProxmoxModule.SkipStart.Value=true;
        TestableProxmoxModule? module=null;TestableProxmoxModule.Capture.Value=m=>module=m;
        await using var host=new ModuleTestHost<TestableProxmoxModule>(Path.Combine(AppContext.BaseDirectory,"manifest.json"),PveFixtures.Json("""{"address":"https://pve.example:8006"}"""));
        host.Secrets.Set("token",ConnectionHarness.Token);await host.StartAsync();
        Assert.True((await module!.RunActionAsync("testConnection",CancellationToken.None)).Ok);
        var results=await module.LookupAsync("disk","",CancellationToken.None);
        Assert.Equal(4,results.Count);
        Assert.Equal(new[]{"backup","extra","local","local-lvm"},results.Select(r=>r.Value.GetProperty("id").GetString()));
        Assert.All(results,r=>Assert.Equal("storage",r.Value.GetProperty("kind").GetString()));
    }
    [Fact]
    public async Task Actions_WorkWithoutProductStartAsync()
    {
        var handler=new FakeProxmoxHandler();TestableProxmoxModule.Handler.Value=handler;TestableProxmoxModule.SkipStart.Value=true;
        TestableProxmoxModule? module=null;TestableProxmoxModule.Capture.Value=m=>module=m;
        await using var host=new ModuleTestHost<TestableProxmoxModule>(Path.Combine(AppContext.BaseDirectory,"manifest.json"),PveFixtures.Json("""{"address":"https://pve.example:8006"}"""));
        host.Secrets.Set("token",ConnectionHarness.Token);await host.StartAsync();
        Assert.True((await module!.RunActionAsync("testConnection",CancellationToken.None)).Ok);
        var options=await module.LookupAsync("disk","",CancellationToken.None);
        Assert.DoesNotContain(options,x=>x.Value.GetProperty("kind").GetString()=="system");
        Assert.Contains(options,x=>x.Value.TryGetProperty("id",out var id)&&id.GetString()=="local-lvm");
    }
    [Fact]
    public async Task Lookup_EmptySettings_LeavesSystemDefaultToSdkWithoutCatalog()
    {
        var handler=new FakeProxmoxHandler();TestableProxmoxModule.Handler.Value=handler;TestableProxmoxModule.SkipStart.Value=true;
        TestableProxmoxModule? module=null;TestableProxmoxModule.Capture.Value=m=>module=m;
        await using var host=new ModuleTestHost<TestableProxmoxModule>(Path.Combine(AppContext.BaseDirectory,"manifest.json"));
        await host.StartAsync();var results=await module!.LookupAsync("disk","",CancellationToken.None);
        Assert.Empty(results);Assert.Empty(handler.Requests);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Lookup_EmptySettings_UsesValidatedCatalogOrIgnoresCorruption(bool corrupt)
    {
        var handler=new FakeProxmoxHandler();TestableProxmoxModule.Handler.Value=handler;TestableProxmoxModule.SkipStart.Value=true;
        TestableProxmoxModule? module=null;TestableProxmoxModule.Capture.Value=m=>module=m;
        await using var host=new ModuleTestHost<TestableProxmoxModule>(Path.Combine(AppContext.BaseDirectory,"manifest.json"));
        host.Secrets.Set("token",ConnectionHarness.Token);await host.StartAsync();
        ApiToken.TryParse(ConnectionHarness.Token,out var token,out _);
        var binding=new ConnectionBinding("https://pve.example:8006/",token!.Fingerprint,false,new(TransportTrustKind.System),"pve",Guid.NewGuid());
        Assert.True(new ConnectionStore(host.Secrets).TrySave(binding));
        var data=new StorageCatalogData(binding.Origin,binding.Node,binding.Revision,corrupt?[null!]:[new("data","data")]);
        await File.WriteAllTextAsync(Path.Combine(module!.TestDataDirectory,"storage-catalog.json"),JsonSerializer.Serialize(data));
        var results=await module.LookupAsync("disk","",CancellationToken.None);
        Assert.Equal(corrupt?0:1,results.Count);Assert.Empty(handler.Requests);
    }
}
