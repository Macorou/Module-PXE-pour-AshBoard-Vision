using System.Net;
using System.Security.Authentication;
using AshboardVision.Sdk.Testing;
using ProxmoxMonitor.Connection;
using ProxmoxMonitor.Configuration;
using ProxmoxMonitor.Tests.Support;
namespace ProxmoxMonitor.Tests;
public sealed class CertificateTests
{
    [Fact]
    public async Task PinWriteRefused_NeverAuthenticates()
    {
        using var h=new ConnectionHarness();h.Settings=h.Settings with {AllowSelfSigned=true};for(var i=0;i<63;i++)h.Secrets.Set("filler"+i,"x");
        h.ClientFactory=key=>new HttpClient(new SimulatedTls(h.Handler,key,h.Secrets),false);
        Assert.False((await h.Coordinator().TestAsync(CancellationToken.None)).Ok);
        Assert.DoesNotContain(h.Handler.Requests,r=>r.HasAuthorization);Assert.Null(h.Store.ReadBinding());
    }
    [Fact]
    public async Task SelfSignedPin_IsPersistedAndForgetInvalidatesBinding()
    {
        using var h=new ConnectionHarness();h.Settings=h.Settings with {AllowSelfSigned=true};
        h.ClientFactory=key=>new HttpClient(new SimulatedTls(h.Handler,key,h.Secrets),false);
        var coordinator=h.Coordinator();Assert.True((await coordinator.TestAsync(CancellationToken.None)).Ok);
        var binding=h.Store.ReadBinding();Assert.Equal(TransportTrustKind.Pinned,binding!.Trust.Kind);
        Assert.Equal("test-pin",binding.Trust.Fingerprint);
        Assert.True(coordinator.ForgetCertificate().Ok);Assert.Null(h.Store.ReadBinding());
    }
    [Fact]
    public async Task ValidCertificate_DoesNotNeedPin()
    {
        using var h=new ConnectionHarness();Assert.True((await h.Coordinator().TestAsync(CancellationToken.None)).Ok);
        Assert.Equal(TransportTrustKind.System,h.Store.ReadBinding()!.Trust.Kind);
    }
    [Fact]
    public async Task PeriodicRead_DoesNotLearnMissingPin()
    {
        using var h=new ConnectionHarness();h.Settings=h.Settings with {AllowSelfSigned=true};h.ClientFactory=key=>new HttpClient(new SimulatedTls(h.Handler,key,h.Secrets),false);
        var coordinator=h.Coordinator();Assert.True((await coordinator.TestAsync(CancellationToken.None)).Ok);
        h.Secrets.Set("cert:"+CertificateTrust.KeyFor(h.Settings.Origin),null);
        var count=h.Handler.Requests.Count;Assert.Null(coordinator.OpenActive(out _));Assert.Equal(count,h.Handler.Requests.Count);
    }
    private sealed class SimulatedTls : HttpMessageHandler
    {
        private readonly HttpMessageInvoker _invoker;private readonly string? _key;private readonly FakeSecrets _secrets;
        internal SimulatedTls(FakeProxmoxHandler handler,string? key,FakeSecrets secrets){_invoker=new(handler,false);_key=key;_secrets=secrets;}
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken ct)
        {
            if(_key is null)throw new HttpRequestException("Untrusted test certificate",new AuthenticationException());
            var old=_secrets.Get("cert:"+_key);if(old is not null&&old!="test-pin")throw new HttpRequestException("Changed test certificate",new AuthenticationException());
            _secrets.Set("cert:"+_key,"test-pin");return _invoker.SendAsync(r,ct);
        }
    }
}
