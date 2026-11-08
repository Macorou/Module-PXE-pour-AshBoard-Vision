using System.Text.Json;
namespace ProxmoxMonitor.Connection;
internal sealed record CatalogStorage(string Id,string Name);
internal sealed record StorageCatalogData(string Origin,string Node,Guid Revision,CatalogStorage[] Storages);
internal sealed class StorageCatalog(string dataDirectory)
{
    private string CatalogPath=>Path.Combine(dataDirectory,"storage-catalog.json");
    internal StorageCatalogData? Read(ConnectionBinding binding)
    {
        try
        {
            if(!File.Exists(CatalogPath)||new FileInfo(CatalogPath).Length>8*1024*1024)return null;
            var data=JsonSerializer.Deserialize<StorageCatalogData>(File.ReadAllText(CatalogPath));
            return data is not null && data.Storages is not null &&
                data.Storages.All(s=>s is not null && !string.IsNullOrWhiteSpace(s.Id) && s.Id.Length<=128 && !string.IsNullOrWhiteSpace(s.Name) && !s.Id.Any(char.IsControl) && !s.Name.Any(char.IsControl)) &&
                data.Origin==binding.Origin && data.Node==binding.Node && data.Revision==binding.Revision ? data : null;
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException){return null;}
    }
    internal async Task SaveAsync(StorageCatalogData catalog,CancellationToken ct)
    {
        Directory.CreateDirectory(dataDirectory);
        var temporary=Path.Combine(dataDirectory,"storage-catalog-"+Guid.NewGuid().ToString("N")+".tmp");
        try
        {
            await File.WriteAllTextAsync(temporary,JsonSerializer.Serialize(catalog),ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            File.Move(temporary,CatalogPath,true);
        }
        finally {if(File.Exists(temporary))File.Delete(temporary);}
    }
}
