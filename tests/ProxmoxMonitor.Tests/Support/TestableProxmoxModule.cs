namespace ProxmoxMonitor.Tests.Support;
internal sealed class TestableProxmoxModule : ProxmoxModule
{
    internal static readonly AsyncLocal<FakeProxmoxHandler?> Handler=new();
    internal static readonly AsyncLocal<Action<TestableProxmoxModule>?> Capture=new();
    internal static readonly AsyncLocal<bool> SkipStart=new();
    public TestableProxmoxModule()=>Capture.Value?.Invoke(this);
    internal string TestDataDirectory=>Services.DataDirectory;
    internal Task StartProduction(CancellationToken ct)=>base.StartAsync(ct);
    internal override HttpClient CreateHttpClient(string? certificateKey)=>new(Handler.Value??throw new InvalidOperationException("Handler de test absent."),false);
    public override Task StartAsync(CancellationToken ct)=>SkipStart.Value?Task.CompletedTask:base.StartAsync(ct);
}
