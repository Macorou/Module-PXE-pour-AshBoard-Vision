using ProxmoxMonitor.Configuration;
using ProxmoxMonitor.Monitoring;
using SkiaSharp;
using Trofeo.ModuleKit;
namespace ProxmoxMonitor.Rendering;
internal sealed record WidgetLayout
{
    internal bool IsCompact{get;init;}
    internal SKRect Header{get;init;}
    internal SKRect Footer{get;init;}
    internal SKRect[] Metrics{get;init;}=[];
    internal SKRect[] Counters{get;init;}=[];
    internal SKRect[] GuestRects{get;init;}=[];
    internal SKRect[] StorageRects{get;init;}=[];
    internal SKRect GuestHeading{get;init;}
    internal SKRect StorageHeading{get;init;}
    internal SKRect Uptime{get;init;}
    internal GuestInfo[] GuestItems{get;init;}=[];
    internal StorageInfo[] StorageItems{get;init;}=[];
    internal int PageIndex{get;init;}
    internal int PageCount{get;init;}=1;
    internal int StoragePageIndex{get;init;}
    internal int StoragePageCount{get;init;}=1;
}
internal static class LayoutEngine
{
    internal static WidgetLayout Measure(ModuleRenderContext context,ModulePresentation presentation,ProxmoxSettings settings)
    {
        var s=context.TextScale;var gap=6*s;var area=SKRect.Inflate(context.Bounds,-10*s,-10*s);
        var compact=settings.ForceCompact||context.Size.Columns<6||context.Size.Rows<3;
        var header=new SKRect(area.Left,area.Top,area.Right,area.Top+22*s);
        var footer=new SKRect(area.Left,area.Bottom-12*s,area.Right,area.Bottom);
        var counterY=compact?footer.Top-gap-28*s:header.Bottom+gap;
        var counters=Split(new SKRect(area.Left,counterY,area.Right,counterY+28*s),2,gap);
        if(compact)
            return new(){IsCompact=true,Header=header,Footer=footer,Counters=counters,
                Metrics=Split(new SKRect(area.Left,header.Bottom+gap,area.Right,counterY-gap),3,gap)};
        var body=new SKRect(area.Left,counters[0].Bottom+gap,area.Right,footer.Top-gap);
        SKRect[] metrics;SKRect guestArea;SKRect uptime=default,storageHeading=default;
        SKRect[] storageRects=[];StorageInfo[] storageItems=[];int storagePage=0,storagePages=1;
        if(settings.Layout==DetailLayout.Resources)
        {
            var sidebar=Math.Min(area.Width*.45f,Math.Max(220*s,area.Width*.35f));
            var left=new SKRect(body.Left,body.Top,body.Left+sidebar,body.Bottom);
            guestArea=new(left.Right+12*s,body.Top,body.Right,body.Bottom);
            var showUptime=body.Height>=250*s;
            var mh=Math.Clamp((body.Height-(84+(showUptime?14:0))*s)/3,28*s,52*s);
            var y=left.Top;var metricList=new List<SKRect>();
            for(var i=0;i<3;i++)
            {
                metricList.Add(new(left.Left,y,left.Right,y+mh));y+=mh+gap;
                if(i==1&&showUptime){uptime=new(left.Left,y,left.Right,y+12*s);y+=14*s;}
            }
            metrics=metricList.ToArray();
            storageHeading=new(left.Left,y,left.Right,y+12*s);y+=16*s;
            var capacity=Math.Max(1,(int)((left.Bottom-y+gap)/(40*s+gap)));
            (storageItems,storagePage,storagePages)=Page(presentation.Storages.ToArray(),capacity,context.Now,settings.PageDuration);
            storageRects=storageItems.Select((_,i)=>new SKRect(left.Left,y+i*(40*s+gap),left.Right,y+i*(40*s+gap)+40*s)).ToArray();
        }
        else
        {
            metrics=Split(new SKRect(body.Left,body.Top,body.Right,body.Top+70*s),3,gap);
            guestArea=new(body.Left,metrics[0].Bottom+gap,body.Right,body.Bottom);
        }
        var heading=new SKRect(guestArea.Left,guestArea.Top,guestArea.Right,guestArea.Top+16*s);
        var rowsTop=heading.Bottom+4*s;var grid=settings.Layout==DetailLayout.Mosaic;
        var columns=grid?Math.Max(1,(int)((guestArea.Width+gap)/(220*s+gap))):1;
        var height=(grid?78:34)*s;var rowGap=grid?gap:4*s;
        var rows=Math.Max(1,(int)((guestArea.Bottom-rowsTop+rowGap)/(height+rowGap)));
        var (guests,page,pages)=Page(presentation.Guests.ToArray(),rows*columns,context.Now,settings.PageDuration);
        var width=(guestArea.Width-gap*(columns-1))/columns;
        var guestRects=guests.Select((_,i)=>new SKRect(guestArea.Left+(i%columns)*(width+gap),rowsTop+(i/columns)*(height+rowGap),guestArea.Left+(i%columns)*(width+gap)+width,rowsTop+(i/columns)*(height+rowGap)+height)).ToArray();
        return new(){Header=header,Footer=footer,Counters=counters,Metrics=metrics,GuestHeading=heading,GuestRects=guestRects,GuestItems=guests,
            PageIndex=page,PageCount=pages,StorageHeading=storageHeading,StorageRects=storageRects,StorageItems=storageItems,StoragePageIndex=storagePage,StoragePageCount=storagePages,Uptime=uptime};
    }
    private static SKRect[] Split(SKRect area,int columns,float gap)
    {
        var width=(area.Width-gap*(columns-1))/columns;
        return Enumerable.Range(0,columns).Select(i=>new SKRect(area.Left+i*(width+gap),area.Top,area.Left+i*(width+gap)+width,area.Bottom)).ToArray();
    }
    private static (T[] Items,int Index,int Pages) Page<T>(T[] all,int capacity,DateTimeOffset now,TimeSpan duration)
    {
        var pages=Math.Max(1,(all.Length+capacity-1)/capacity);
        var index=(int)(now.UtcTicks/duration.Ticks%pages);
        return (all.Skip(index*capacity).Take(capacity).ToArray(),index,pages);
    }
}
