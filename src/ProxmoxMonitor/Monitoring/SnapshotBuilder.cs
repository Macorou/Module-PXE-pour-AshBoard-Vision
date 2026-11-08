using ProxmoxMonitor.Api;
using System.Collections.Immutable;
using System.Text.Json;
namespace ProxmoxMonitor.Monitoring;
internal static class SnapshotBuilder
{
    internal static MonitoringSnapshot Build(ProbeBatch batch,MonitoringSnapshot? previous,DateTimeOffset now)
    {
        if(previous?.Revision!=batch.Revision || previous.Origin!=batch.Origin)previous=null;
        var terminal=batch.TerminalFailure ?? (!batch.Coverage.Complete?new ApiFailure(ApiFailureKind.Forbidden,batch.Coverage.Reason??"Droits incomplets."):null) ??
            new[]{batch.Host.Failure,batch.Vms.Failure,batch.Containers.Failure,batch.Storages.Failure}.FirstOrDefault(f=>f?.Kind is ApiFailureKind.Unauthorized or ApiFailureKind.Forbidden or ApiFailureKind.Certificate or ApiFailureKind.NotConfigured);
        if(terminal is not null)return new(batch.Origin,batch.Revision,new(null,null,terminal),new(null,null,terminal),new(null,null,terminal),new(null,null,terminal),terminal);
        return new(batch.Origin,batch.Revision,
            Read(batch.Host,previous?.Host,now,json=>ReadHost(json,batch.Node)),
            Read(batch.Vms,previous?.Vms,now,json=>ReadGuests(json,GuestKind.Vm)),
            Read(batch.Containers,previous?.Containers,now,json=>ReadGuests(json,GuestKind.Container)),
            Read(batch.Storages,previous?.Storages,now,json=>ReadStorages(json,batch.Origin)),null);
    }
    private static TimedData<T> Read<T>(ApiReadResult result,TimedData<T>? previous,DateTimeOffset now,Func<JsonElement,T> parse) where T:class
    {
        var failure=result.Failure;
        if(failure is null&&result.Data is {} json)
        {
            try{return new(parse(json),now,null);}
            catch(InvalidDataException){failure=new(ApiFailureKind.Protocol,"Données incomplètes dans la réponse de Proxmox.");}
        }
        return new(previous?.Value,previous?.LastSuccess,failure??new(ApiFailureKind.Protocol,"Données indisponibles."));
    }
    private static HostData ReadHost(JsonElement json,string node)
    {
        RequireKind(json,JsonValueKind.Object);
        var uptime=Long(json,"uptime");
        return new(node,Cpu(Number(json,"cpu")),Usage(Long(Field(json,"memory"),"used"),Long(Field(json,"memory"),"total")),
            Usage(Long(Field(json,"rootfs"),"used"),Long(Field(json,"rootfs"),"total")),uptime>=0&&uptime<=TimeSpan.MaxValue.TotalSeconds?uptime:null);
    }
    private static GuestSet ReadGuests(JsonElement json,GuestKind kind)
    {
        RequireKind(json,JsonValueKind.Array);
        var guests=new List<GuestInfo>();var ids=new HashSet<long>();
        foreach(var row in json.EnumerateArray())
        {
            RequireKind(row,JsonValueKind.Object);
            if(Long(row,"vmid") is not {} id||id<=0||!ids.Add(id))throw new InvalidDataException();
            if(Flag(row,"template")==true)continue;
            var status=Text(row,"status");var qmp=Text(row,"qmpstatus");
            var state=status switch
            {
                "stopped"=>GuestState.Stopped,
                "running" when kind==GuestKind.Container||qmp is null or "running"=>GuestState.Running,
                "running" when qmp=="paused"=>GuestState.Paused,
                "running" when qmp=="suspended"=>GuestState.Suspended,
                _=>GuestState.Unknown
            };
            var name=Text(row,"name");
            name=string.IsNullOrWhiteSpace(name)?(kind==GuestKind.Vm?"VM ":"CT ")+id:Clean(name);
            var running=state==GuestState.Running;
            guests.Add(new(id,kind,name,state,running?Cpu(Number(row,"cpu")):Metric.Unavailable,
                running?Usage(Long(row,"mem"),Long(row,"maxmem")):Metric.Unavailable));
        }
        var ordered=guests.OrderBy(g=>g.Id).ToImmutableArray();
        return new(ordered,ordered.Any(g=>g.State==GuestState.Unknown)?null:ordered.Count(g=>g.State==GuestState.Running),ordered.Length);
    }
    private static StorageSet ReadStorages(JsonElement json,string origin)
    {
        RequireKind(json,JsonValueKind.Array);
        var list=new List<StorageInfo>();var ids=new HashSet<string>(StringComparer.Ordinal);
        foreach(var row in json.EnumerateArray())
        {
            RequireKind(row,JsonValueKind.Object);
            var id=Text(row,"storage");if(string.IsNullOrWhiteSpace(id)||!ids.Add(id))throw new InvalidDataException();
            var enabled=Flag(row,"enabled")==true;var active=Flag(row,"active")==true;
            list.Add(new(id,Clean(id),origin,enabled,active,enabled&&active?Usage(Long(row,"used"),Long(row,"total")):Metric.Unavailable));
        }
        return new(list.OrderBy(s=>s.Id,StringComparer.Ordinal).ToImmutableArray());
    }
    private static Metric Cpu(double? fraction)=>fraction is {} value&&double.IsFinite(value)&&value>=0&&value<=1?new(value*100):Metric.Unavailable;
    private static Metric Usage(long? used,long? total)=>used is >=0&&total is >0&&used<=total?new((double)used.Value/total.Value*100,used,total):Metric.Unavailable;
    private static void RequireKind(JsonElement value,JsonValueKind kind){if(value.ValueKind!=kind)throw new InvalidDataException();}
    private static JsonElement Field(JsonElement value,string key)=>value.ValueKind==JsonValueKind.Object&&value.TryGetProperty(key,out var field)?field:default;
    private static double? Number(JsonElement value,string key){var field=Field(value,key);return field.ValueKind==JsonValueKind.Number&&field.TryGetDouble(out var number)&&double.IsFinite(number)?number:null;}
    private static long? Long(JsonElement value,string key){var field=Field(value,key);return field.ValueKind==JsonValueKind.Number&&field.TryGetInt64(out var number)?number:null;}
    private static string? Text(JsonElement value,string key){var field=Field(value,key);return field.ValueKind==JsonValueKind.String?field.GetString():null;}
    private static bool? Flag(JsonElement value,string key)
    {
        var field=Field(value,key);
        return field.ValueKind switch{JsonValueKind.True=>true,JsonValueKind.False=>false,JsonValueKind.Number when field.TryGetInt32(out var n)&&n is 0 or 1=>n==1,_=>null};
    }
    private static string Clean(string text)=>string.Concat(text.Select(c=>char.IsControl(c)?' ':c)).Trim();
}
