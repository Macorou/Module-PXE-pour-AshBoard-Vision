using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;

namespace ProxmoxMonitor.Tests.Support;

internal sealed record CapturedRequest(HttpMethod Method,Uri Uri,bool HasAuthorization);
internal sealed class FakeProxmoxHandler : HttpMessageHandler
{
    private readonly ConcurrentQueue<CapturedRequest> _requests=new();
    internal IReadOnlyList<CapturedRequest> Requests => _requests.ToArray();
    internal Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> Respond {get;set;}=
        (request,ct)=>Task.FromResult(JsonResponse(PveFixtures.ForRoute(request.RequestUri!.PathAndQuery),request));
    internal static HttpResponseMessage JsonResponse(JsonElement data,HttpRequestMessage? request=null) =>
        new(HttpStatusCode.OK) { RequestMessage=request,Content=new StringContent(JsonSerializer.Serialize(new {data}),Encoding.UTF8,"application/json") };
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
    {
        _requests.Enqueue(new CapturedRequest(request.Method,request.RequestUri!,request.Headers.Contains("Authorization")));
        return Respond(request,ct);
    }
}
