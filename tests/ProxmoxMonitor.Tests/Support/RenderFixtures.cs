using ProxmoxMonitor.Configuration;
using ProxmoxMonitor.Monitoring;
using ProxmoxMonitor.Rendering;
using SkiaSharp;
using Trofeo.ModuleKit;
namespace ProxmoxMonitor.Tests.Support;
internal static class RenderFixtures
{
    internal static ProxmoxSettings Settings(string layout="balanced",bool force=false)
    {SettingsReader.TryRead(System.Text.Json.JsonSerializer.SerializeToElement(new{address="https://pve.example:8006",layout,forceCompact=force}),out var s,out _);return s!;}
    internal static Theme Theme(string id="classique",bool light=false)=>new(id,
        light?new SKColor(241,244,249):new SKColor(10,15,24),light?SKColors.White:new SKColor(18,25,39),1,12,
        light?new SKColor(25,35,52):new SKColor(233,238,248),light?new SKColor(92,105,124):new SKColor(142,158,182),
        new SKColor(74,164,255),new SKColor(245,183,74),SKTypeface.Default);
    internal static ModuleRenderContext Context(int columns=6,int rows=4,float scale=1,string theme="classique",bool light=false,DateTimeOffset? now=null)=>
        new(new SKRect(0,0,columns==4?464:columns==16?1892:702,rows==2?212:rows==3?323:434),new GridSize(columns,rows),Theme(theme,light),scale,now??SnapshotTests.Now);
    internal static MonitoringSnapshot Snapshot(int guests=9)
    {
        var vms=Enumerable.Range(0,Math.Max(5,guests)).Select(i=>new{vmid=100+i,name=i==0?"Maison · Home Assistant":i==1?"Sauvegarde — données très importantes 🏠":$"Service {i+1:00}",status=i<2?"running":"stopped",cpu=0.12,mem=1073741824L,maxmem=4294967296L});
        var lxc=Enumerable.Range(0,4).Select(i=>new{vmid=200+i,name=$"Conteneur {i+1}",status=i<3?"running":"stopped",cpu=0.02,mem=134217728,maxmem=536870912});
        var batch=SnapshotTests.Batch() with{Vms=ApiReadResultFor(vms),Containers=ApiReadResultFor(lxc)};
        return SnapshotBuilder.Build(batch,null,SnapshotTests.Now);
    }
    private static ProxmoxMonitor.Api.ApiReadResult ApiReadResultFor<T>(T value)=>ProxmoxMonitor.Api.ApiReadResult.Ok(System.Text.Json.JsonSerializer.SerializeToElement(value));
}
