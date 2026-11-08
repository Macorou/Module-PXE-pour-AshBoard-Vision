using ProxmoxMonitor.Configuration;
using Trofeo.ModuleKit;
using System.Text.Json;
namespace ProxmoxMonitor.Connection;
internal sealed class ConnectionStore(ISecretStore secrets)
{
    internal ConnectionBinding? ReadBinding()
    {
        var json=secrets.Get("connection");if(string.IsNullOrWhiteSpace(json))return null;
        try
        {
            var binding=JsonSerializer.Deserialize<ConnectionBinding>(json);
            if(binding is null || binding.Trust is null || binding.Revision==Guid.Empty || string.IsNullOrWhiteSpace(binding.Node) ||
                binding.TokenFingerprint is not {Length:64} || !ConnectionOrigin.TryParse(binding.Origin,out _,out _) ||
                !Enum.IsDefined(binding.Trust.Kind) || binding.Trust.Kind==TransportTrustKind.Pinned && string.IsNullOrWhiteSpace(binding.Trust.Fingerprint))return null;
            return binding;
        }
        catch(JsonException){return null;}
    }
    internal ApiToken? ReadToken(){ApiToken.TryParse(secrets.Get("token"),out var token,out _);return token;}
    internal bool TrySave(ConnectionBinding binding)
    {
        secrets.Set("connection",JsonSerializer.Serialize(binding));
        return ReadBinding()==binding;
    }
    internal void ClearBinding()=>secrets.Set("connection",null);
}
