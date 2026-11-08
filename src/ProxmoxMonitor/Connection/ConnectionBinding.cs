using ProxmoxMonitor.Api;
namespace ProxmoxMonitor.Connection;
internal enum TransportTrustKind { System,Pinned }
internal sealed record TransportTrust(TransportTrustKind Kind,string? Fingerprint=null);
internal sealed record ConnectionBinding(string Origin,string TokenFingerprint,bool AllowSelfSigned,TransportTrust Trust,string Node,Guid Revision);
internal sealed class ConnectionProblem(ApiFailure failure) : Exception(failure.Message)
{ internal ApiFailure Failure {get;}=failure; }
