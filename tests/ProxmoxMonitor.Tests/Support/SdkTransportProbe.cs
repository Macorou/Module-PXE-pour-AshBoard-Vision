using AshboardVision.Sdk;
using SkiaSharp;
using Trofeo.ModuleKit;
namespace ProxmoxMonitor.Tests.Support;
internal sealed class SdkTransportProbe : CommunityModule
{
    internal static readonly AsyncLocal<Action<SdkTransportProbe>?> Capture=new();
    public SdkTransportProbe()=>Capture.Value?.Invoke(this);
    internal HttpClient CreateClient(string? key)=>Services.CreateHttpClient(key);
    public override void Render(SKCanvas canvas,ModuleRenderContext ctx) { }
}
