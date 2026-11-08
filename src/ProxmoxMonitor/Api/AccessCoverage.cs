using System.Text.Json;
namespace ProxmoxMonitor.Api;
internal sealed record CoverageResult(bool Complete,string? Reason,IReadOnlyList<string> MissingPaths);
internal static class AccessCoverage
{
    internal static bool CanReadAcls(JsonElement permissions)=>permissions.ValueKind==JsonValueKind.Object && HasGrant(permissions,"/access","Sys.Audit",false);
    internal static CoverageResult Evaluate(JsonElement permissions,JsonElement acls,string node)
    {
        if (permissions.ValueKind!=JsonValueKind.Object || acls.ValueKind!=JsonValueKind.Array)
            return new(false,"Impossible de vérifier les droits de lecture.",["/access"]);
        var missing=new HashSet<string>(StringComparer.Ordinal);
        foreach (var (path,privilege) in new[] {("/nodes","Sys.Audit"),("/vms","VM.Audit"),("/storage","Datastore.Audit"),("/pool","Pool.Audit")})
            if (!HasGrant(permissions,path,privilege,true)) missing.Add(path);
        if (!HasGrant(permissions,"/access","Sys.Audit",false)) missing.Add("/access");
        foreach (var acl in acls.EnumerateArray())
        {
            if (acl.ValueKind!=JsonValueKind.Object || !acl.TryGetProperty("path",out var pathValue) || pathValue.ValueKind!=JsonValueKind.String)
            { missing.Add("/access"); continue; }
            var path=pathValue.GetString()!;
            var privilege=PrivilegeFor(path);
            if (privilege is not null && !HasGrant(permissions,path,privilege,false)) missing.Add(path);
        }
        // Pool membership can introduce effective child paths without an explicit ACL on the VM.
        foreach (var entry in permissions.EnumerateObject())
        {
            var privilege=PrivilegeFor(entry.Name);
            if (privilege is not null && !HasGrant(permissions,entry.Name,privilege,false)) missing.Add(entry.Name);
        }
        return missing.Count==0 ? new(true,null,[]) :
            new(false,"Droits de lecture incomplets : accorde PVEAuditor avec propagation au compte et au jeton, puis teste à nouveau.",missing.Order(StringComparer.Ordinal).ToArray());
    }
    private static string? PrivilegeFor(string path) => path switch
    {
        "/nodes"=>"Sys.Audit", "/vms"=>"VM.Audit", "/storage"=>"Datastore.Audit", "/pool"=>"Pool.Audit",
        _ when path.StartsWith("/nodes/",StringComparison.Ordinal)=>"Sys.Audit",
        _ when path.StartsWith("/vms/",StringComparison.Ordinal)=>"VM.Audit",
        _ when path.StartsWith("/storage/",StringComparison.Ordinal)=>"Datastore.Audit",
        _ when path.StartsWith("/pool/",StringComparison.Ordinal)=>"Pool.Audit",
        _=>null
    };
    private static bool HasGrant(JsonElement map,string path,string privilege,bool propagate)
    {
        if (!map.TryGetProperty(path,out var grants) || grants.ValueKind!=JsonValueKind.Object || !grants.TryGetProperty(privilege,out var value)) return false;
        int? flag=value.ValueKind switch { JsonValueKind.True=>1,JsonValueKind.False=>0,JsonValueKind.Number when value.TryGetInt32(out var n) && n is 0 or 1=>n,_=>null };
        return flag is not null && (!propagate || flag==1);
    }
}
