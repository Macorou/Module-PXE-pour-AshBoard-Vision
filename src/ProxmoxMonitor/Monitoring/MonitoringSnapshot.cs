using System.Collections.Immutable;
using ProxmoxMonitor.Api;
namespace ProxmoxMonitor.Monitoring;
public enum MetricSeverity { Normal,Warning,Critical }
public enum GuestState { Running,Stopped,Paused,Suspended,Unknown }
internal enum GuestKind { Vm,Container }
internal sealed record Metric(double? Percent,long? UsedBytes=null,long? TotalBytes=null)
{ internal static Metric Unavailable {get;}=new(Percent:null); }
internal sealed record HostData(string Node,Metric Cpu,Metric Ram,Metric SystemDisk,long? UptimeSeconds);
internal sealed record GuestInfo(long Id,GuestKind Kind,string Name,GuestState State,Metric Cpu,Metric Ram);
internal sealed record GuestSet(ImmutableArray<GuestInfo> Guests,int? Online,int Total);
internal sealed record StorageInfo(string Id,string Name,string Origin,bool Enabled,bool Active,Metric Usage);
internal sealed record StorageSet(ImmutableArray<StorageInfo> Items);
internal sealed record TimedData<T>(T? Value,DateTimeOffset? LastSuccess,ApiFailure? Failure) where T:class;
internal sealed record ProbeBatch(string Origin,string Node,Guid Revision,ApiReadResult Host,ApiReadResult Vms,ApiReadResult Containers,ApiReadResult Storages,CoverageResult Coverage,ApiFailure? TerminalFailure=null);
internal sealed record MonitoringSnapshot(string Origin,Guid Revision,TimedData<HostData> Host,TimedData<GuestSet> Vms,TimedData<GuestSet> Containers,TimedData<StorageSet> Storages,ApiFailure? GeneralError)
{
    internal static MonitoringSnapshot Empty {get;}=new("",Guid.Empty,new(null,null,null),new(null,null,null),new(null,null,null),new(null,null,null),null);
}
