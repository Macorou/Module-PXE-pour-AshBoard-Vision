using System.Text.Json;
namespace ProxmoxMonitor.Tests.Support;
internal static class PveFixtures
{
    internal static JsonElement Json(string text) { using var doc=JsonDocument.Parse(text); return doc.RootElement.Clone(); }
    internal static JsonElement CompletePermissions() => Json("""{"/access":{"Sys.Audit":1},"/nodes":{"Sys.Audit":1},"/vms":{"VM.Audit":1},"/storage":{"Datastore.Audit":1},"/pool":{"Pool.Audit":1}}""");
    internal static JsonElement CompleteAcls() => Json("""[{"path":"/","type":"token","ugid":"monitor@pve!screen","roleid":"PVEAuditor","propagate":1}]""");
    internal static JsonElement Nodes() => Json("""[{"node":"pve","status":"online"}]""");
    internal static JsonElement Host() => Json("""{"cpu":0.23,"memory":{"used":19327352832,"total":34359738368},"rootfs":{"used":33285996544,"total":103079215104},"uptime":123456}""");
    internal static JsonElement Vms() => Json("""[{"vmid":100,"name":"home","status":"running","qmpstatus":"running","cpu":0.12,"cpus":4,"mem":1073741824,"maxmem":4294967296,"template":0},{"vmid":101,"name":"backup","status":"stopped","template":0}]""");
    internal static JsonElement Lxc() => Json("""[{"vmid":200,"name":"dns","status":"running","cpu":0.01,"mem":134217728,"maxmem":536870912,"template":0}]""");
    internal static JsonElement Storages() => Json("""[{"storage":"local-lvm","type":"lvmthin","content":"images,rootdir","active":1,"enabled":1,"used":644245094400,"total":1073741824000}]""");
    internal static JsonElement ForRoute(string route) => route switch
    {
        "/api2/json/version"=>Json("""{"version":"9.0.0","release":"9.0"}"""),
        "/api2/json/access/permissions"=>CompletePermissions(),
        "/api2/json/access/acl"=>CompleteAcls(),
        "/api2/json/nodes"=>Nodes(),
        "/api2/json/nodes/pve/status"=>Host(),
        "/api2/json/nodes/pve/qemu?full=1"=>Vms(),
        "/api2/json/nodes/pve/lxc"=>Lxc(),
        "/api2/json/nodes/pve/storage"=>Storages(),
        _=>throw new InvalidOperationException("Route de test inattendue : "+route)
    };
}
