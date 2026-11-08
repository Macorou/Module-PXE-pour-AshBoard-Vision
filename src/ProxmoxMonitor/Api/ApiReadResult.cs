using System.Text.Json;
namespace ProxmoxMonitor.Api;
public enum ApiFailureKind { Network,Timeout,Unauthorized,Forbidden,Certificate,Protocol,NotConfigured }
internal sealed record ApiFailure(ApiFailureKind Kind,string Message)
{
    internal bool IsTerminal => Kind is ApiFailureKind.Unauthorized or ApiFailureKind.Forbidden or ApiFailureKind.Certificate or ApiFailureKind.NotConfigured or ApiFailureKind.Protocol;
}
internal sealed record ApiReadResult(JsonElement? Data,ApiFailure? Failure)
{
    internal static ApiReadResult Ok(JsonElement data) => new(data.Clone(),null);
    internal static ApiReadResult Fail(ApiFailureKind kind,string message) => new(null,new ApiFailure(kind,message));
}
