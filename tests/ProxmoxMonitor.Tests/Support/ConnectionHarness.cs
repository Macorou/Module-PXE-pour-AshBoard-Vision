using AshboardVision.Sdk.Testing;
using ProxmoxMonitor.Configuration;
using ProxmoxMonitor.Connection;
using System.Text.Json;
namespace ProxmoxMonitor.Tests.Support;
internal sealed class ConnectionHarness : IDisposable
{
    internal const string Token="monitor@pve!screen=fake-secret";
    internal readonly string DirectoryPath=Path.Combine(Path.GetTempPath(),"proxmox-monitor-test-"+Guid.NewGuid().ToString("N"));
    internal FakeSecrets Secrets {get;}=new();
    internal FakeProxmoxHandler Handler {get;}=new();
    internal ProxmoxSettings Settings {get;set;}
    internal Func<string?,HttpClient>? ClientFactory {get;set;}
    internal ConnectionStore Store => new(Secrets);
    internal StorageCatalog Catalog => new(DirectoryPath);
    internal ConnectionHarness()
    {
        SettingsReader.TryRead(PveFixtures.Json("""{"address":"https://pve.example:8006"}"""),out var settings,out _);
        Settings=settings!; Secrets.Set("token",Token);
    }
    internal ConnectionCoordinator Coordinator(ProxmoxSettings? settings=null)
    {
        HttpClient Factory(string? key) => ClientFactory?.Invoke(key) ?? new HttpClient(Handler,disposeHandler:false);
        return new(settings??Settings,Store,new CertificateTrust(Factory,Secrets,TimeProvider.System),Catalog,Factory,TimeProvider.System);
    }
    public void Dispose() { Handler.Dispose();if (Directory.Exists(DirectoryPath)) Directory.Delete(DirectoryPath,true); }
}
