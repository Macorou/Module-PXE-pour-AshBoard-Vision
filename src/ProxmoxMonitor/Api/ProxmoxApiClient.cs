using ProxmoxMonitor.Configuration;
using System.Net;
using System.Security.Authentication;
using System.Text.Json;
namespace ProxmoxMonitor.Api;
internal sealed class ProxmoxApiClient(HttpClient client,ConnectionOrigin origin,ApiToken? token,TimeProvider time)
{
    internal async Task<ApiReadResult> GetAsync(string relativeRoute,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(relativeRoute) || relativeRoute.StartsWith('/') || relativeRoute.Contains('\\') ||
            relativeRoute.Contains('#') || relativeRoute.Any(char.IsControl) ||
            Uri.TryCreate(relativeRoute,UriKind.Absolute,out _) ||
            relativeRoute.Split('/').Any(p=>Uri.UnescapeDataString(p) is "." or ".."))
            return Protocol();
        var uri=new Uri(origin.BaseUri,"api2/json/"+relativeRoute);
        if (uri.GetLeftPart(UriPartial.Authority)!=origin.BaseUri.GetLeftPart(UriPartial.Authority)) return Protocol();
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(5),time);
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(ct,deadline.Token);
        using var request=new HttpRequestMessage(HttpMethod.Get,uri);
        if (token is not null) request.Headers.TryAddWithoutValidation("Authorization",token.AuthorizationValue);
        try
        {
            using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,linked.Token).ConfigureAwait(false);
            if (response.RequestMessage?.RequestUri?.AbsoluteUri!=uri.AbsoluteUri) return ApiReadResult.Fail(ApiFailureKind.Protocol,"Utilise l’adresse directe de l’API, sans redirection.");
            if (response.StatusCode==HttpStatusCode.Unauthorized) return ApiReadResult.Fail(ApiFailureKind.Unauthorized,"Jeton refusé : vérifie le jeton complet et teste à nouveau.");
            if (response.StatusCode==HttpStatusCode.Forbidden) return ApiReadResult.Fail(ApiFailureKind.Forbidden,"Droits insuffisants : vérifie PVEAuditor pour le compte et le jeton.");
            if ((int)response.StatusCode>=500) return ApiReadResult.Fail(ApiFailureKind.Network,"Proxmox ne répond pas correctement. Nouvel essai automatique.");
            if (!response.IsSuccessStatusCode) return Protocol();
            const int maxBytes=8*1024*1024;
            if (response.Content.Headers.ContentLength>maxBytes) return Protocol();
            await using var stream=await response.Content.ReadAsStreamAsync(linked.Token).ConfigureAwait(false);
            using var bytes=new MemoryStream();
            var buffer=new byte[16384];
            int read;
            while ((read=await stream.ReadAsync(buffer,linked.Token).ConfigureAwait(false))>0)
            {
                if (bytes.Length+read>maxBytes) return Protocol();
                bytes.Write(buffer,0,read);
            }
            using var doc=JsonDocument.Parse(bytes.GetBuffer().AsMemory(0,(int)bytes.Length),new JsonDocumentOptions {MaxDepth=32});
            if (doc.RootElement.ValueKind!=JsonValueKind.Object || !doc.RootElement.TryGetProperty("data",out var data) ||
                data.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array)) return Protocol();
            return ApiReadResult.Ok(data);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { return ApiReadResult.Fail(ApiFailureKind.Timeout,"Délai dépassé pour joindre Proxmox. Nouvel essai automatique."); }
        catch (JsonException) { return Protocol(); }
        catch (HttpRequestException ex)
        {
            for (Exception? cause=ex;cause is not null;cause=cause.InnerException)
                if (cause is AuthenticationException) return ApiReadResult.Fail(ApiFailureKind.Certificate,"Certificat refusé : vérifie le mode de certificat et teste à nouveau.");
            return ApiReadResult.Fail(ApiFailureKind.Network,"Proxmox est injoignable. Vérifie l’adresse et les permissions réseau.");
        }
        catch (IOException) { return ApiReadResult.Fail(ApiFailureKind.Network,"Lecture interrompue. Nouvel essai automatique."); }
    }
    private static ApiReadResult Protocol() => ApiReadResult.Fail(ApiFailureKind.Protocol,"Réponse API invalide : vérifie l’adresse et teste la connexion.");
}
