using ProxmoxMonitor.Configuration;
using ProxmoxMonitor.Connection;
using ProxmoxMonitor.Api;
namespace ProxmoxMonitor.Monitoring;
internal interface IMonitoringSource
{
    Guid? BindingRevision{get;}
    Task<ProbeBatch> CollectAsync(CancellationToken ct);
}
internal sealed class ProxmoxMonitoringSource(ConnectionCoordinator coordinator,ConnectionStore store,ProxmoxSettings settings) : IMonitoringSource
{
    public Guid? BindingRevision=>store.ReadBinding()?.Revision;
    public async Task<ProbeBatch> CollectAsync(CancellationToken ct)
    {
        using var lease=coordinator.OpenActive(out var problem,ct);
        if(lease is null)return Failed(problem??new(ApiFailureKind.NotConfigured,"Teste la connexion."),BindingRevision??Guid.Empty);
        try
        {
            var permissionsRead=lease.Client.GetAsync("access/permissions",ct);
            var nodesRead=lease.Client.GetAsync("nodes",ct);
            await Task.WhenAll(permissionsRead,nodesRead).ConfigureAwait(false);
            var permissions=ConnectionCoordinator.Require(permissionsRead.Result);
            var node=ConnectionCoordinator.ReadSingleNode(ConnectionCoordinator.Require(nodesRead.Result));
            if(node!=lease.Binding.Node)return Failed(new(ApiFailureKind.NotConfigured,"L’hôte a changé. Teste à nouveau la connexion."),lease.Binding.Revision);
            if(!AccessCoverage.CanReadAcls(permissions))return Failed(new(ApiFailureKind.Forbidden,"Droits d’audit insuffisants. Vérifie PVEAuditor, puis teste à nouveau."),lease.Binding.Revision);
            var acls=ConnectionCoordinator.Require(await lease.Client.GetAsync("access/acl",ct).ConfigureAwait(false));
            var coverage=AccessCoverage.Evaluate(permissions,acls,node);
            if(!coverage.Complete)return Failed(new(ApiFailureKind.Forbidden,coverage.Reason!),lease.Binding.Revision);
            var path="nodes/"+Uri.EscapeDataString(node)+"/";
            var reads=new[]{lease.Client.GetAsync(path+"status",ct),lease.Client.GetAsync(path+"qemu?full=1",ct),lease.Client.GetAsync(path+"lxc",ct),lease.Client.GetAsync(path+"storage",ct)};
            ApiReadResult[] results;
            try {results=await Task.WhenAll(reads).ConfigureAwait(false);}
            catch(OperationCanceledException) when(ct.IsCancellationRequested)
            {
                // All category tasks have settled. Keep successful replies when
                // the shared budget cancels a slower route; the loop still
                // discards this batch if application shutdown caused cancellation.
                results=reads.Select(read=>read.IsCompletedSuccessfully?read.Result:
                    ApiReadResult.Fail(ApiFailureKind.Timeout,"Délai de collecte dépassé pour cette catégorie. Nouvel essai automatique.")).ToArray();
            }
            if(!lease.IsCurrent())return Failed(new(ApiFailureKind.NotConfigured,"La connexion a changé. Teste à nouveau les paramètres."),lease.Binding.Revision);
            return new(settings.Origin.BaseUri.AbsoluteUri,node,lease.Binding.Revision,results[0],results[1],results[2],results[3],coverage);
        }
        catch(ConnectionProblem ex){return Failed(ex.Failure,lease.Binding.Revision);}
    }
    private ProbeBatch Failed(ApiFailure failure,Guid revision)
    {
        var result=new ApiReadResult(null,failure);
        return new(settings.Origin.BaseUri.AbsoluteUri,"",revision,result,result,result,result,new(true,null,[]),failure.IsTerminal?failure:null);
    }
}
