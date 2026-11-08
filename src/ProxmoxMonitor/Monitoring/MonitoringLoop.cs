using ProxmoxMonitor.Configuration;
using Trofeo.ModuleKit;
using ProxmoxMonitor.Api;
namespace ProxmoxMonitor.Monitoring;
internal static class FreshnessPolicy
{
    internal static bool IsExpired(DateTimeOffset? lastSuccess,DateTimeOffset now,TimeSpan refresh)=>lastSuccess is null ||
        now-lastSuccess.Value>TimeSpan.FromSeconds(Math.Max(30,refresh.TotalSeconds*2));
}
internal sealed class MonitoringLoop(IMonitoringSource source,ProxmoxSettings settings,TimeProvider time)
{
    private MonitoringSnapshot _current=MonitoringSnapshot.Empty;
    private int _running;
    internal MonitoringSnapshot Current=>Volatile.Read(ref _current);
    internal event Action<MonitoringSnapshot>? SnapshotChanged;
    internal event Action<ModuleHealth,string?>? StatusChanged;
    internal async Task RunAsync(CancellationToken ct)
    {
        if(Interlocked.Exchange(ref _running,1)!=0)throw new InvalidOperationException("La collecte est déjà démarrée.");
        var failures=0;
        try
        {
            while(!ct.IsCancellationRequested)
            {
                var revision=source.BindingRevision;
                ProbeBatch batch;
                using(var budget=new CancellationTokenSource(TimeSpan.FromSeconds(8),time))
                using(var linked=CancellationTokenSource.CreateLinkedTokenSource(ct,budget.Token))
                {
                    try {batch=await source.CollectAsync(linked.Token).ConfigureAwait(false);}
                    catch(OperationCanceledException) when(!ct.IsCancellationRequested){batch=Failed(new(ApiFailureKind.Timeout,"Actualisation trop longue. Nouvel essai automatique."),revision);}
                    catch(Exception ex) when(ex is not (OperationCanceledException or OutOfMemoryException))
                    {batch=Failed(new(ApiFailureKind.Protocol,"Erreur de collecte. Teste à nouveau la connexion."),revision);}
                }
                ct.ThrowIfCancellationRequested();
                if(source.BindingRevision!=revision)continue;
                var snapshot=SnapshotBuilder.Build(batch,Current,time.GetUtcNow());
                var errors=new[]{snapshot.GeneralError,snapshot.Host.Failure,snapshot.Vms.Failure,snapshot.Containers.Failure,snapshot.Storages.Failure};
                var error=errors.FirstOrDefault(e=>e is not null);
                var terminal=snapshot.GeneralError?.IsTerminal==true;
                var transient=errors.Any(e=>e?.Kind is ApiFailureKind.Network or ApiFailureKind.Timeout);
                failures=transient?Math.Min(failures+1,6):0;
                var seconds=transient?Math.Min(60,settings.RefreshInterval.TotalSeconds*Math.Pow(2,failures)):settings.RefreshInterval.TotalSeconds;
                var pause=Task.Delay(TimeSpan.FromSeconds(terminal?settings.RefreshInterval.TotalSeconds:seconds),time,ct);
                Volatile.Write(ref _current,snapshot);
                SnapshotChanged?.Invoke(snapshot);
                StatusChanged?.Invoke(error is null?ModuleHealth.Online:error.Kind==ApiFailureKind.NotConfigured?ModuleHealth.NotConfigured:ModuleHealth.Offline,error?.Message);
                await pause.ConfigureAwait(false);
                if(terminal)
                    while(source.BindingRevision==revision)
                        await Task.Delay(settings.RefreshInterval,time,ct).ConfigureAwait(false);
            }
        }
        catch(OperationCanceledException) when(ct.IsCancellationRequested) { }
    }
    private ProbeBatch Failed(ApiFailure failure,Guid? revision)
    {
        var result=new ApiReadResult(null,failure);
        return new(settings.Origin.BaseUri.AbsoluteUri,"",revision??Guid.Empty,result,result,result,result,new(true,null,[]),failure.IsTerminal?failure:null);
    }
}
