using ProxmoxMonitor.Configuration;
using Trofeo.ModuleKit;
using ProxmoxMonitor.Api;
using System.Security.Authentication;
namespace ProxmoxMonitor.Connection;
internal sealed class CertificateTrust(Func<string?,HttpClient> createClient,ISecretStore secrets,TimeProvider time)
{
    internal static string KeyFor(ConnectionOrigin origin)=>"pve-"+Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(origin.BaseUri.AbsoluteUri)));
    internal async Task<TransportTrust> VerifyAsync(ConnectionOrigin origin,bool allowSelfSigned,CancellationToken ct)
    {
        try {await ProbeAsync(origin,null,ct).ConfigureAwait(false);return new(TransportTrustKind.System);}
        catch(HttpRequestException ex) when(IsCertificateFailure(ex))
        {
            if(!allowSelfSigned)throw CertificateProblem();
        }
        try {await ProbeAsync(origin,KeyFor(origin),ct).ConfigureAwait(false);}
        catch(HttpRequestException ex) when(IsCertificateFailure(ex)){throw CertificateProblem();}
        var pin=secrets.Get("cert:"+KeyFor(origin));
        if(string.IsNullOrWhiteSpace(pin))throw new ConnectionProblem(new(ApiFailureKind.Certificate,"Le certificat n’a pas pu être mémorisé. Vérifie le coffre du module puis teste à nouveau."));
        return new(TransportTrustKind.Pinned,pin);
    }
    private async Task ProbeAsync(ConnectionOrigin origin,string? key,CancellationToken ct)
    {
        using var client=createClient(key);
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(5),time);
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(ct,deadline.Token);
        var uri=new Uri(origin.BaseUri,"api2/json/version");
        using var request=new HttpRequestMessage(HttpMethod.Get,uri);
        using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,linked.Token).ConfigureAwait(false);
        if(response.RequestMessage?.RequestUri?.AbsoluteUri!=uri.AbsoluteUri)
            throw new ConnectionProblem(new(ApiFailureKind.Protocol,"Indique l’adresse directe de l’API, sans redirection."));
    }
    private static bool IsCertificateFailure(Exception exception)
    {
        for(Exception? cause=exception;cause is not null;cause=cause.InnerException)if(cause is AuthenticationException)return true;
        return false;
    }
    private static ConnectionProblem CertificateProblem()=>new(new(ApiFailureKind.Certificate,"Certificat refusé : vérifie le mode auto-signé ou oublie une ancienne empreinte, puis teste à nouveau."));
    internal void Forget(ConnectionOrigin origin)=>secrets.Set("cert:"+KeyFor(origin),null);
    internal bool IsCurrent(ConnectionOrigin origin,TransportTrust trust)=>trust.Kind==TransportTrustKind.System ||
        trust.Kind==TransportTrustKind.Pinned && !string.IsNullOrWhiteSpace(trust.Fingerprint) && secrets.Get("cert:"+KeyFor(origin))==trust.Fingerprint;
}
