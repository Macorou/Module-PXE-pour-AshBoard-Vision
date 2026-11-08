using ProxmoxMonitor.Rendering;
using ProxmoxMonitor.Tests.Support;
using SkiaSharp;
namespace ProxmoxMonitor.Tests;
public sealed class LayoutTests
{
    public static IEnumerable<object[]> Cases()=>from style in new[]{"balanced","resources","mosaic"} from scale in new[]{.85f,1f,1.2f} from size in new[]{(4,2),(6,3),(6,4),(16,4)} select new object[]{style,scale,size.Item1,size.Item2};
    [Theory][MemberData(nameof(Cases))]
    public void MandatoryInformation_FitsWithoutOverlap(string style,float scale,int columns,int rows)
    {
        var ctx=RenderFixtures.Context(columns,rows,scale);var settings=RenderFixtures.Settings(style);var view=ModulePresentation.Create(RenderFixtures.Snapshot(40),settings,ctx.Now);
        var layout=LayoutEngine.Measure(ctx,view,settings);
        Assert.Equal(columns<6||rows<3,layout.IsCompact);
        Assert.True(layout.Header.Height>=16*scale);Assert.True(layout.Footer.Height>=10*scale);
        var rects=new[]{layout.Header,layout.Footer}.Concat(layout.Metrics).Concat(layout.Counters).Concat(layout.GuestRects).Concat(layout.StorageRects).ToArray();
        Assert.All(rects,r=>{Assert.True(r.Width>0&&r.Height>0);Assert.True(ctx.Bounds.Contains(r));});
        for(var i=0;i<rects.Length;i++)for(var j=i+1;j<rects.Length;j++)Assert.False(SKRect.Intersect(rects[i],rects[j]).Width>0&&SKRect.Intersect(rects[i],rects[j]).Height>0,$"Chevauchement {style}/{scale}: {i},{j}");
        Assert.InRange(layout.PageIndex,0,layout.PageCount-1);
        if(!layout.IsCompact){Assert.NotEmpty(layout.GuestItems);Assert.All(layout.GuestRects,r=>Assert.True(r.Height>=28*scale));}
    }
    [Fact]
    public void ForcedCompact_RemainsCompactAfterResize()
    {
        var s=RenderFixtures.Settings("mosaic",true);var view=ModulePresentation.Create(RenderFixtures.Snapshot(),s,SnapshotTests.Now);
        Assert.True(LayoutEngine.Measure(RenderFixtures.Context(),view,s).IsCompact);
    }
}
