using System.Text.Json;
namespace ProxmoxMonitor.Configuration;
internal static class SettingsReader
{
    internal static bool TryRead(JsonElement json,out ProxmoxSettings? settings,out string? error)
    {
        settings=null;
        error="Vérifie les réglages du serveur dans les paramètres.";
        if (json.ValueKind!=JsonValueKind.Object || !json.TryGetProperty("address",out var address) ||
            address.ValueKind!=JsonValueKind.String || !ConnectionOrigin.TryParse(address.GetString()!,out var origin,out error)) return false;
        if (!Number(json,"refreshSeconds",10,5,60,out var refresh) || !Number(json,"pageSeconds",8,3,30,out var page) ||
            !Boolean(json,"forceCompact",out var force) || !Boolean(json,"allowSelfSigned",out var selfSigned))
        { error="Vérifie les durées et les options dans les paramètres."; return false; }
        var layout=DetailLayout.Balanced;
        if (json.TryGetProperty("layout",out var layoutValue))
        {
            if (layoutValue.ValueKind!=JsonValueKind.String) return Invalid(out error);
            switch(layoutValue.GetString())
            {
                case "balanced": layout=DetailLayout.Balanced; break;
                case "resources": layout=DetailLayout.Resources; break;
                case "mosaic": layout=DetailLayout.Mosaic; break;
                default: return Invalid(out error);
            }
        }
        var disk=new DiskSelection();
        if (json.TryGetProperty("disk",out var diskValue) && diskValue.ValueKind!=JsonValueKind.Null)
        {
            if (diskValue.ValueKind!=JsonValueKind.Object || !diskValue.TryGetProperty("kind",out var kind) || kind.ValueKind!=JsonValueKind.String) return Invalid(out error);
            if (kind.GetString()=="storage")
            {
                if (!diskValue.TryGetProperty("id",out var id) || id.ValueKind!=JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(id.GetString()) || id.GetString()!.Length>128 ||
                    !diskValue.TryGetProperty("origin",out var selectedOrigin) || selectedOrigin.ValueKind!=JsonValueKind.String ||
                    !ConnectionOrigin.TryParse(selectedOrigin.GetString()!,out var source,out _)) return Invalid(out error);
                disk=new DiskSelection("storage",source!.BaseUri.AbsoluteUri,id.GetString());
            }
            else if (kind.GetString()!="system") return Invalid(out error);
        }
        settings=new ProxmoxSettings(origin!,layout,force,selfSigned,disk,TimeSpan.FromSeconds(refresh),TimeSpan.FromSeconds(page));
        error=null;
        return true;
    }
    private static bool Invalid(out string? error) { error="Vérifie la présentation et le disque choisis dans les paramètres."; return false; }
    private static bool Number(JsonElement json,string key,int fallback,int min,int max,out int value)
    {
        value=fallback;
        return !json.TryGetProperty(key,out var field) || field.ValueKind==JsonValueKind.Number && field.TryGetInt32(out value) && value>=min && value<=max;
    }
    private static bool Boolean(JsonElement json,string key,out bool value)
    {
        value=false;
        if (!json.TryGetProperty(key,out var field)) return true;
        if (field.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) return false;
        value=field.GetBoolean();
        return true;
    }
}
