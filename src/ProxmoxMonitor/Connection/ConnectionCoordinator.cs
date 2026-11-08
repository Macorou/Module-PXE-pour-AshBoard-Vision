using ProxmoxMonitor.Api;
using ProxmoxMonitor.Configuration;
using Trofeo.ModuleKit;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace ProxmoxMonitor.Connection;
internal sealed class ConnectionCoordinator(ProxmoxSettings settings,ConnectionStore store,CertificateTrust trust,StorageCatalog catalog,Func<string?,HttpClient> createClient,TimeProvider time)
{
    // Keep pin deletion/relearning outside any active TLS handshake, including other tiles.
    private static readonly SemaphoreSlim Gate=new(1,1);
    internal async Task<ActionResult> TestAsync(CancellationToken ct)
    {
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(20),time);
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(ct,deadline.Token);
        var entered=false;
        try
        {
            var token=store.ReadToken();
            if(token is null)return new(false,"Indique le jeton API complet, puis teste à nouveau.");
            await Gate.WaitAsync(linked.Token).ConfigureAwait(false);entered=true;
            if(store.ReadToken()?.Fingerprint!=token.Fingerprint)return new(false,"Le jeton a changé pendant le test. Recommence le test.");
            var transport=await trust.VerifyAsync(settings.Origin,settings.AllowSelfSigned,linked.Token).ConfigureAwait(false);
            if(store.ReadToken()?.Fingerprint!=token.Fingerprint)return new(false,"Le jeton a changé pendant le test. Recommence le test.");
            using var http=createClient(transport.Kind==TransportTrustKind.Pinned?CertificateTrust.KeyFor(settings.Origin):null);
            var api=new ProxmoxApiClient(http,settings.Origin,token,time);
            Require(await api.GetAsync("version",linked.Token).ConfigureAwait(false));
            var permissions=Require(await api.GetAsync("access/permissions",linked.Token).ConfigureAwait(false));
            if(!AccessCoverage.CanReadAcls(permissions))return new(false,"Accorde PVEAuditor au compte et au jeton avec propagation, puis teste à nouveau.");
            var node=ReadSingleNode(Require(await api.GetAsync("nodes",linked.Token).ConfigureAwait(false)));
            var acls=Require(await api.GetAsync("access/acl",linked.Token).ConfigureAwait(false));
            var coverage=AccessCoverage.Evaluate(permissions,acls,node);
            if(!coverage.Complete)return new(false,coverage.Reason!);
            var storages=Require(await api.GetAsync("nodes/"+Uri.EscapeDataString(node)+"/storage",linked.Token).ConfigureAwait(false));
            if(storages.ValueKind!=JsonValueKind.Array)return new(false,"Liste des stockages invalide.");
            if(store.ReadToken()?.Fingerprint!=token.Fingerprint)return new(false,"Le jeton a changé pendant le test. Recommence le test.");
            var previous=store.ReadBinding();
            var binding=new ConnectionBinding(settings.Origin.BaseUri.AbsoluteUri,token.Fingerprint,settings.AllowSelfSigned,transport,node,Guid.NewGuid());
            var items=storages.EnumerateArray().Where(s=>s.ValueKind==JsonValueKind.Object&&s.TryGetProperty("storage",out var id)&&id.ValueKind==JsonValueKind.String)
                .Select(s=>s.GetProperty("storage").GetString()!).Where(id=>id.Length is >0 and <=128&&!id.Any(char.IsControl)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
                .Select(id=>new CatalogStorage(id,id)).ToArray();
            await catalog.SaveAsync(new(binding.Origin,node,binding.Revision,items),linked.Token).ConfigureAwait(false);
            if(!store.TrySave(binding))return new(false,"La connexion n’a pas pu être enregistrée dans le coffre. Vérifie son espace disponible.");
            if(store.ReadToken()?.Fingerprint!=token.Fingerprint)
            {
                if(previous is not null)store.TrySave(previous);else store.ClearBinding();
                return new(false,"Le jeton a changé pendant le test. Recommence le test.");
            }
            return new(true,$"Connexion validée : {node}, {items.Length} stockage(s). Tu peux enregistrer les paramètres.");
        }
        catch(ConnectionProblem ex){return new(false,ex.Failure.Message);}
        catch(OperationCanceledException) when(!ct.IsCancellationRequested){return new(false,"Test interrompu : délai dépassé. Vérifie l’adresse et le réseau.");}
        catch(HttpRequestException){return new(false,"Impossible de joindre Proxmox. Vérifie l’adresse et les permissions réseau.");}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){return new(false,"Impossible d’enregistrer le catalogue local. Vérifie les fichiers du module.");}
        finally {if(entered)Gate.Release();}
    }
    internal ActionResult ForgetCertificate()
    {
        Gate.Wait();
        try
        {
            var binding=store.ReadBinding();
            if(binding?.Origin==settings.Origin.BaseUri.AbsoluteUri)store.ClearBinding();
            trust.Forget(settings.Origin);
            return new(true,"Empreinte oubliée. Lance un nouveau test de connexion.");
        }
        finally {Gate.Release();}
    }
    internal ConnectionLease? OpenActive(out ApiFailure? failure,CancellationToken ct=default)
    {
        Gate.Wait(ct);
        var transferred=false;
        try
        {
            var binding=store.ReadBinding();var token=store.ReadToken();
            if(binding is null||token is null||!Matches(binding,token)||!trust.IsCurrent(settings.Origin,binding.Trust))
            {failure=new(ApiFailureKind.NotConfigured,"Teste la connexion au serveur dans les paramètres.");return null;}
            var http=createClient(binding.Trust.Kind==TransportTrustKind.Pinned?CertificateTrust.KeyFor(settings.Origin):null);
            var lease=new ConnectionLease(new ProxmoxApiClient(http,settings.Origin,token,time),binding,http,
                ()=>store.ReadBinding()?.Revision==binding.Revision&&store.ReadToken()?.Fingerprint==binding.TokenFingerprint&&trust.IsCurrent(settings.Origin,binding.Trust),()=>Gate.Release());
            transferred=true;failure=null;return lease;
        }
        finally {if(!transferred)Gate.Release();}
    }
    private bool Matches(ConnectionBinding binding,ApiToken token)=>binding.Origin==settings.Origin.BaseUri.AbsoluteUri && binding.TokenFingerprint==token.Fingerprint && binding.AllowSelfSigned==settings.AllowSelfSigned;
    internal static JsonElement Require(ApiReadResult result)=>result.Data is {} data?data:throw new ConnectionProblem(result.Failure??new(ApiFailureKind.Protocol,"Réponse API invalide."));
    internal static string ReadSingleNode(JsonElement nodes)
    {
        if(nodes.ValueKind!=JsonValueKind.Array||nodes.GetArrayLength()!=1)
            throw new ConnectionProblem(new(ApiFailureKind.NotConfigured,"Cette version demande un serveur Proxmox autonome avec un seul nœud visible."));
        var value=nodes[0];
        if(value.ValueKind!=JsonValueKind.Object||!value.TryGetProperty("node",out var field)||field.ValueKind!=JsonValueKind.String||
            field.GetString() is not {} node||!Regex.IsMatch(node,"^[A-Za-z0-9][A-Za-z0-9.-]{0,62}$",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100)))
            throw new ConnectionProblem(new(ApiFailureKind.Protocol,"Nom de nœud Proxmox invalide."));
        return node;
    }
}
internal sealed class ConnectionLease(ProxmoxApiClient client,ConnectionBinding binding,HttpClient http,Func<bool> current,Action release) : IDisposable
{
    private int _disposed;
    internal ProxmoxApiClient Client=>client;
    internal ConnectionBinding Binding=>binding;
    internal bool IsCurrent()=>Volatile.Read(ref _disposed)==0 && current();
    public void Dispose(){if(Interlocked.Exchange(ref _disposed,1)!=0)return;try{http.Dispose();}finally{release();}}
}
