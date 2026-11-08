using ProxmoxMonitor.Rendering;
using ProxmoxMonitor.Tests.Support;
using SkiaSharp;
using ProxmoxMonitor.Api;
using ProxmoxMonitor.Configuration;
using ProxmoxMonitor.Monitoring;
using Trofeo.ModuleKit;
namespace ProxmoxMonitor.Tests;
public sealed class RenderTests
{
    [Theory]
    [InlineData("balanced",6,4)]
    [InlineData("resources",6,4)]
    [InlineData("mosaic",6,4)]
    [InlineData("balanced",4,2)]
    public void RendersActualDataAndExportsPreview(string style,int columns,int rows)
    {
        foreach(var theme in new[]{"classique","liquid-glass","pixel"})foreach(var scale in new[]{1f,1.2f})foreach(var light in new[]{false,true})
        {
            var ctx=RenderFixtures.Context(columns,rows,scale,theme,light);var settings=RenderFixtures.Settings(style);
            var view=ModulePresentation.Create(RenderFixtures.Snapshot(5),settings,ctx.Now);var layout=LayoutEngine.Measure(ctx,view,settings);
            using var bitmap=new SKBitmap((int)ctx.Bounds.Width,(int)ctx.Bounds.Height);using var canvas=new SKCanvas(bitmap);canvas.Clear(SKColors.Transparent);
            if(layout.IsCompact)CompactRenderer.Draw(canvas,ctx,view,layout);else DetailedRenderers.Draw(canvas,ctx,view,layout,settings.Layout);
            Assert.Contains(bitmap.Pixels,p=>p.Alpha>0);
            var output=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../artifacts/previews"));Directory.CreateDirectory(output);
            using var composite=new SKBitmap(bitmap.Width,bitmap.Height);using var background=new SKCanvas(composite);background.Clear(ctx.Theme.Panel);background.DrawBitmap(bitmap,0,0);background.Flush();
            using var image=SKImage.FromBitmap(composite);using var png=image.Encode(SKEncodedImageFormat.Png,100);
            using var file=File.Create(Path.Combine(output,FormattableString.Invariant($"{style}-{columns}x{rows}-{theme}-{(light?"light":"dark")}-{scale:0.0}.png")));png.SaveTo(file);
        }
    }
    [Fact]
    public void ExportsOfflineAndManyStorageScenarios()
    {
        var snapshot=RenderFixtures.Snapshot(40);
        Export(RenderFixtures.Context(4,2,1.2f,now:SnapshotTests.Now.AddSeconds(31)),RenderFixtures.Settings(),snapshot,"compact-offline.png");
        var storages=System.Text.Json.JsonSerializer.SerializeToElement(Enumerable.Range(0,9).Select(i=>new{storage="stockage-"+i,enabled=1,active=1,used=300L*1073741824,total=500L*1073741824}));
        snapshot=SnapshotBuilder.Build(SnapshotTests.Batch() with {Storages=ApiReadResult.Ok(storages)},null,SnapshotTests.Now);
        Export(RenderFixtures.Context(scale:1.2f),RenderFixtures.Settings("resources"),snapshot,"resources-many-storages.png");
    }
    private static void Export(ModuleRenderContext ctx,ProxmoxSettings settings,MonitoringSnapshot snapshot,string name)
    {
        var view=ModulePresentation.Create(snapshot,settings,ctx.Now);var layout=LayoutEngine.Measure(ctx,view,settings);
        using var bitmap=new SKBitmap((int)ctx.Bounds.Width,(int)ctx.Bounds.Height);using var canvas=new SKCanvas(bitmap);canvas.Clear(ctx.Theme.Panel);
        if(layout.IsCompact)CompactRenderer.Draw(canvas,ctx,view,layout);else DetailedRenderers.Draw(canvas,ctx,view,layout,settings.Layout);
        var output=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../artifacts/previews"));Directory.CreateDirectory(output);
        using var image=SKImage.FromBitmap(bitmap);using var png=image.Encode(SKEncodedImageFormat.Png,100);using var file=File.Create(Path.Combine(output,name));png.SaveTo(file);
    }
}
