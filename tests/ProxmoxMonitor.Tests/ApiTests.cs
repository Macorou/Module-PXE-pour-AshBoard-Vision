using System.Net;
using Microsoft.Extensions.Time.Testing;
using ProxmoxMonitor.Api;
using ProxmoxMonitor.Configuration;
using ProxmoxMonitor.Tests.Support;
namespace ProxmoxMonitor.Tests;
public sealed class ApiTests
{
    private static ProxmoxApiClient Api(HttpClient http,TimeProvider? time=null)
    {
        ConnectionOrigin.TryParse("https://pve.example:8006",out var origin,out _);
        ApiToken.TryParse("monitor@pve!screen=fake-secret",out var token,out _);
        return new ProxmoxApiClient(http,origin!,token,time??TimeProvider.System);
    }
    [Fact]
    public async Task GetAsync_SendsOnlyGetToBoundOrigin()
    {
        var handler=new FakeProxmoxHandler(); using var http=new HttpClient(handler);
        var result=await Api(http).GetAsync("nodes",CancellationToken.None);
        Assert.Null(result.Failure);
        Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get,handler.Requests[0].Method);
        Assert.Equal("https://pve.example:8006/api2/json/nodes",handler.Requests[0].Uri.AbsoluteUri);
        Assert.True(handler.Requests[0].HasAuthorization);
        Assert.False(http.DefaultRequestHeaders.Contains("Authorization"));
    }
    [Theory]
    [InlineData("https://other.example/nodes")]
    [InlineData("//other.example/nodes")]
    [InlineData("../nodes")]
    public async Task ExternalOrEscapingRoute_IsNeverSent(string route)
    {
        var handler=new FakeProxmoxHandler(); using var http=new HttpClient(handler);
        Assert.Equal(ApiFailureKind.Protocol,(await Api(http).GetAsync(route,CancellationToken.None)).Failure?.Kind);
        Assert.Empty(handler.Requests);
    }
    [Fact]
    public async Task GetAsync_RedirectedFinalUri_IsRejected()
    {
        var handler=new FakeProxmoxHandler { Respond=(r,ct)=>Task.FromResult(FakeProxmoxHandler.JsonResponse(PveFixtures.Nodes(),new HttpRequestMessage(HttpMethod.Get,"https://other.example/api2/json/nodes"))) };
        using var http=new HttpClient(handler);
        Assert.Equal(ApiFailureKind.Protocol,(await Api(http).GetAsync("nodes",CancellationToken.None)).Failure?.Kind);
    }
    [Theory]
    [InlineData(401,ApiFailureKind.Unauthorized)]
    [InlineData(403,ApiFailureKind.Forbidden)]
    [InlineData(500,ApiFailureKind.Network)]
    public async Task HttpFailure_IsClassifiedWithoutLeakingBody(int code,ApiFailureKind expected)
    {
        var handler=new FakeProxmoxHandler { Respond=(r,ct)=>Task.FromResult(new HttpResponseMessage((HttpStatusCode)code) {RequestMessage=r,Content=new StringContent("fake-secret")}) };
        using var http=new HttpClient(handler);
        var failure=(await Api(http).GetAsync("nodes",CancellationToken.None)).Failure;
        Assert.Equal(expected,failure?.Kind); Assert.DoesNotContain("fake-secret",failure!.Message);
    }
    [Theory]
    [InlineData("<html>fake-secret</html>")]
    [InlineData("{}")]
    [InlineData("{\"data\":null}")]
    public async Task MalformedResponse_IsAnExplicitProtocolFailure(string body)
    {
        var handler=new FakeProxmoxHandler { Respond=(r,ct)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {RequestMessage=r,Content=new StringContent(body)}) };
        using var http=new HttpClient(handler);
        Assert.Equal(ApiFailureKind.Protocol,(await Api(http).GetAsync("nodes",CancellationToken.None)).Failure?.Kind);
    }
    [Fact]
    public async Task RequestTimesOut_AfterFiveSeconds()
    {
        var time=new FakeTimeProvider(); var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler=new FakeProxmoxHandler {Respond=async (r,ct)=>{entered.SetResult();await Task.Delay(Timeout.Infinite,ct);return new HttpResponseMessage();}};
        using var http=new HttpClient(handler);
        var pending=Api(http,time).GetAsync("nodes",CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2)); time.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(ApiFailureKind.Timeout,(await pending.WaitAsync(TimeSpan.FromSeconds(2))).Failure?.Kind);
    }
    [Fact]
    public async Task CallerCancellation_IsPropagated()
    {
        using var cts=new CancellationTokenSource(); cts.Cancel();
        using var http=new HttpClient(new FakeProxmoxHandler());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>Api(http).GetAsync("nodes",cts.Token));
    }
}
