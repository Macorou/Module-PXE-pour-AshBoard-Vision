using System.Net;
namespace ProxmoxMonitor.Configuration;
internal sealed record ConnectionOrigin(Uri BaseUri)
{
    internal static bool TryParse(string text,out ConnectionOrigin? origin,out string? error)
    {
        origin=null;
        error="Indique une adresse HTTPS réseau directe, sans chemin, identifiant ni paramètre.";
        if (string.IsNullOrWhiteSpace(text) || text.Length>256 ||
            !Uri.TryCreate(text.Trim(),UriKind.Absolute,out var uri) || uri.Scheme!=Uri.UriSchemeHttps ||
            uri.UserInfo.Length!=0 || uri.Query.Length!=0 || uri.Fragment.Length!=0 || uri.AbsolutePath!="/" ||
            uri.HostNameType is UriHostNameType.Unknown or UriHostNameType.Basic) return false;
        var host=uri.IdnHost.Trim('[',']').TrimEnd('.').ToLowerInvariant();
        if (host.Length==0 || host.Equals("localhost",StringComparison.OrdinalIgnoreCase)) return false;
        if (IPAddress.TryParse(host,out var address))
        {
            if (address.IsIPv4MappedToIPv6) address=address.MapToIPv4();
            if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any)) return false;
        }
        origin=new ConnectionOrigin(new UriBuilder(uri) { Host=host,Path="/" }.Uri);
        error=null;
        return true;
    }
}
