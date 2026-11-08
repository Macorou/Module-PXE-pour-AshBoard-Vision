using System.Collections.Immutable;
using ProxmoxMonitor.Configuration;
using ProxmoxMonitor.Monitoring;
namespace ProxmoxMonitor.Rendering;
internal sealed record MetricView(string Label,Metric Value,bool Stale=false);
internal sealed record ModulePresentation
{
    internal string Node{get;init;}="Proxmox";
    internal MetricView Cpu{get;init;}=new("CPU",Metric.Unavailable);
    internal MetricView Ram{get;init;}=new("RAM",Metric.Unavailable);
    internal MetricView Disk{get;init;}=new("Disque",Metric.Unavailable);
    internal MetricView SystemDisk{get;init;}=new("Système",Metric.Unavailable);
    internal string VmCountText{get;init;}="Inconnu";
    internal string ContainerCountText{get;init;}="Inconnu";
    internal ImmutableArray<GuestInfo> Guests{get;init;}=[];
    internal ImmutableArray<StorageInfo> Storages{get;init;}=[];
    internal bool VmStale{get;init;}
    internal bool ContainerStale{get;init;}
    internal bool StorageStale{get;init;}
    internal string HealthText{get;init;}="Connexion…";
    internal string FooterText{get;init;}="En attente des données";
    internal long? UptimeSeconds{get;init;}
    internal static ModulePresentation Create(MonitoringSnapshot snapshot,ProxmoxSettings settings,DateTimeOffset now)
    {
        T? Visible<T>(TimedData<T> data) where T:class=>snapshot.GeneralError is null&&!FreshnessPolicy.IsExpired(data.LastSuccess,now,settings.RefreshInterval)?data.Value:null;
        var host=Visible(snapshot.Host);var vms=Visible(snapshot.Vms);var containers=Visible(snapshot.Containers);var storages=Visible(snapshot.Storages);
        var hostStale=snapshot.Host.Failure is not null&&host is not null;
        var system=new MetricView("Système Proxmox",host?.SystemDisk??Metric.Unavailable,hostStale);
        var disk=system with{Label="Disque · Système"};
        if(settings.Disk.Kind=="storage")
        {
            var sameOrigin=settings.Disk.Origin==settings.Origin.BaseUri.AbsoluteUri;
            var selected=sameOrigin?storages?.Items.FirstOrDefault(s=>s.Id==settings.Disk.Id):null;
            disk=new(sameOrigin?"Disque · "+settings.Disk.Id:"Disque à sélectionner",selected?.Usage??Metric.Unavailable,snapshot.Storages.Failure is not null&&selected is not null);
        }
        var errors=new[]{snapshot.GeneralError,snapshot.Host.Failure,snapshot.Vms.Failure,snapshot.Containers.Failure,snapshot.Storages.Failure};
        var old=hostStale||snapshot.Vms.Failure is not null&&vms is not null||snapshot.Containers.Failure is not null&&containers is not null||snapshot.Storages.Failure is not null&&storages is not null;
        var datedCategories=new[]{
            (Visible:host is not null,Time:snapshot.Host.LastSuccess,Stale:hostStale),
            (Visible:vms is not null,Time:snapshot.Vms.LastSuccess,Stale:snapshot.Vms.Failure is not null),
            (Visible:containers is not null,Time:snapshot.Containers.LastSuccess,Stale:snapshot.Containers.Failure is not null),
            (Visible:storages is not null,Time:snapshot.Storages.LastSuccess,Stale:snapshot.Storages.Failure is not null)};
        var last=old?datedCategories.Where(c=>c.Visible&&c.Stale).Min(c=>c.Time):datedCategories.Where(c=>c.Visible).Select(c=>c.Time).DefaultIfEmpty().Max();
        var noData=host is null&&vms is null&&containers is null;
        var health=noData?(snapshot.Revision==Guid.Empty&&snapshot.GeneralError is null?"Connexion…":"Indisponible"):errors.Any(e=>e is not null)?"Partiel":"En ligne";
        var footer=noData?"Vérifie la connexion dans les paramètres":(old?"Mesures anciennes · ":"Actualisé · ")+(last?.ToOffset(now.Offset).ToString("HH:mm:ss")??"—");
        return new()
        {
            Node=snapshot.Host.Value?.Node??settings.Origin.BaseUri.Host,
            Cpu=new("CPU",host?.Cpu??Metric.Unavailable,hostStale),Ram=new("RAM",host?.Ram??Metric.Unavailable,hostStale),Disk=disk,SystemDisk=system,
            VmCountText=DisplayFormatting.Count(vms),ContainerCountText=DisplayFormatting.Count(containers),
            Guests=(vms?.Guests??[]).Concat(containers?.Guests??[]).ToImmutableArray(),Storages=storages?.Items??[],
            VmStale=snapshot.Vms.Failure is not null,ContainerStale=snapshot.Containers.Failure is not null,StorageStale=snapshot.Storages.Failure is not null,
            HealthText=health,FooterText=footer,UptimeSeconds=host?.UptimeSeconds
        };
    }
}
