using System.Security.Cryptography;
using System.Text;
namespace ProxmoxMonitor.Configuration;
internal sealed class ApiToken
{
    private readonly string _value;
    private ApiToken(string value) => _value=value;
    internal string Fingerprint => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(_value)));
    internal string AuthorizationValue => "PVEAPIToken="+_value;
    public override string ToString() => "Jeton API (masqué)";
    internal static bool TryParse(string? text,out ApiToken? token,out string? error)
    {
        token=null;
        error="Indique le jeton complet : utilisateur@realm!nom-du-jeton=valeur.";
        if (string.IsNullOrWhiteSpace(text) || text.Length>4096 || text.Any(char.IsControl)) return false;
        var value=text.Trim();
        if (value.Any(c=>char.IsControl(c)||char.IsWhiteSpace(c))) return false;
        var equals=value.IndexOf('=');
        var bang=value.IndexOf('!');
        var at=value.IndexOf('@');
        if (at<=0 || bang<=at+1 || equals<=bang+1 || equals==value.Length-1 ||
            value.LastIndexOf('@',equals-1)!=at || value.LastIndexOf('!',equals-1)!=bang) return false;
        token=new ApiToken(value);
        error=null;
        return true;
    }
}
