using System.Text.Json;
using AshboardVision.Sdk.Testing;
using ProxmoxMonitor.Configuration;
using ProxmoxMonitor.Connection;
using ProxmoxMonitor.Tests.Support;
namespace ProxmoxMonitor.Tests;
public sealed class SdkTransportTests
{
    private static string Manifest(params int[] ports)
    {
        var file=Path.Combine(Path.GetTempPath(),"proxmox-sdk-test-"+Guid.NewGuid().ToString("N")+".json");
        File.WriteAllText(file,JsonSerializer.Serialize(new {permissions=new[]{"ce-pc"},settings=ports.Select((port,index)=>new {kind="port",key="port"+index,label="Test",@default=port}).ToArray()}));
        return file;
    }
    [Fact]
    public async Task SdkPin_PreflightPersistsAndRejectsReplacement()
    {
        await using var server=new LocalTlsServer();var manifest=Manifest(server.Port);
        try
        {
            SdkTransportProbe? probe=null;SdkTransportProbe.Capture.Value=p=>probe=p;
            await using var host=new ModuleTestHost<SdkTransportProbe>(manifest);await host.StartAsync();
            var origin=new ConnectionOrigin(new Uri($"https://localhost:{server.Port}/"));
            var trust=new CertificateTrust(key=>probe!.CreateClient(key),host.Secrets,TimeProvider.System);
            TransportTrust verified;
            try {verified=await trust.VerifyAsync(origin,true,CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(8));}
            catch(Exception ex){throw new Xunit.Sdk.XunitException(ex.Message+" | serveur TLS : "+string.Join("; ",server.Errors));}
            Assert.Equal(TransportTrustKind.Pinned,verified.Kind);Assert.Equal(64,verified.Fingerprint!.Length);
            Assert.All(server.AuthorizationSeen,x=>Assert.False(x));
            server.RotateCertificate();
            await Assert.ThrowsAsync<ConnectionProblem>(()=>trust.VerifyAsync(origin,true,CancellationToken.None));
        }
        finally{File.Delete(manifest);}
    }
    [Fact]
    public async Task SdkRedirect_DoesNotRetransmitAuthorization()
    {
        await using var first=new LocalTlsServer();await using var target=new LocalTlsServer(first.Certificate);
        var manifest=Manifest(first.Port,target.Port);
        try
        {
            SdkTransportProbe? probe=null;SdkTransportProbe.Capture.Value=p=>probe=p;
            await using var host=new ModuleTestHost<SdkTransportProbe>(manifest);await host.StartAsync();
            first.RedirectTo=$"https://localhost:{target.Port}/api2/json/version";
            using var client=probe!.CreateClient("redirect-test");
            using var request=new HttpRequestMessage(HttpMethod.Get,$"https://localhost:{first.Port}/api2/json/version");request.Headers.TryAddWithoutValidation("Authorization","PVEAPIToken=monitor@pve!screen=fake-secret");
            using var response=await client.SendAsync(request).WaitAsync(TimeSpan.FromSeconds(8));
            Assert.True(response.IsSuccessStatusCode);Assert.True(Assert.Single(first.AuthorizationSeen));Assert.False(Assert.Single(target.AuthorizationSeen));
        }
        finally{File.Delete(manifest);}
    }
}
