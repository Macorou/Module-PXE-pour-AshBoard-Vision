using SkiaSharp;
using Trofeo.ModuleKit;
using ProxmoxMonitor.Configuration;
using ProxmoxMonitor.Monitoring;
namespace ProxmoxMonitor.Rendering;
internal static class DetailedRenderers
{
    internal static void Draw(SKCanvas canvas,ModuleRenderContext context,ModulePresentation presentation,WidgetLayout layout,DetailLayout style)
    {
        canvas.Save();
        try
        {
            canvas.ClipRect(context.Bounds);DrawingPrimitives.Chrome(canvas,context,presentation,layout);
            var metrics=new[]{presentation.Cpu,presentation.Ram,style==DetailLayout.Resources?presentation.SystemDisk:presentation.Disk};
            for(var i=0;i<Math.Min(3,layout.Metrics.Length);i++)DrawingPrimitives.MetricCard(canvas,context,metrics[i],layout.Metrics[i]);
            if(style==DetailLayout.Resources)
            {
                var suffix=layout.StoragePageCount>1?$" · {layout.StoragePageIndex+1}/{layout.StoragePageCount}":"";
                DrawingPrimitives.Text(canvas,context,"STOCKAGES"+suffix,layout.StorageHeading,9,context.Theme.TextMuted);
                if(presentation.UptimeSeconds is {} seconds)
                {
                    var up=TimeSpan.FromSeconds(seconds);
                    DrawingPrimitives.Text(canvas,context,$"Démarré depuis {(int)up.TotalDays} j {up.Hours:00} h",layout.Uptime,9,context.Theme.TextMuted);
                }
                for(var i=0;i<layout.StorageItems.Length;i++)Storage(canvas,context,layout.StorageItems[i],layout.StorageRects[i],presentation.StorageStale);
            }
            if(style==DetailLayout.Mosaic)DrawingPrimitives.Text(canvas,context,"VM ET CONTENEURS",layout.GuestHeading,9,context.Theme.TextMuted);
            else TableHeading(canvas,context,layout.GuestHeading);
            for(var i=0;i<layout.GuestItems.Length;i++)
            {
                var guest=layout.GuestItems[i];var stale=guest.Kind==GuestKind.Vm?presentation.VmStale:presentation.ContainerStale;
                if(style==DetailLayout.Mosaic)Tile(canvas,context,guest,layout.GuestRects[i],stale);else Row(canvas,context,guest,layout.GuestRects[i],stale);
            }
            if(presentation.Guests.IsEmpty)
            {
                var r=layout.GuestHeading;r.Top=r.Bottom+8*context.TextScale;r.Bottom=r.Top+35*context.TextScale;
                DrawingPrimitives.Text(canvas,context,presentation.VmCountText=="Inconnu"||presentation.ContainerCountText=="Inconnu"?"Invités indisponibles":"Aucune VM ni conteneur",r,12,context.Theme.TextMuted);
            }
        }
        finally{canvas.Restore();}
    }
    private static SKRect Column(SKRect row,float from,float to)=>new(row.Left+row.Width*from,row.Top,row.Left+row.Width*to,row.Bottom);
    private static void TableHeading(SKCanvas c,ModuleRenderContext ctx,SKRect rect)
    {
        var s=ctx.TextScale;rect.Left+=8*s;rect.Right-=8*s;
        DrawingPrimitives.Text(c,ctx,"VM / LXC",Column(rect,0,.43f),9,ctx.Theme.TextMuted);
        DrawingPrimitives.Text(c,ctx,"ÉTAT",Column(rect,.43f,.66f),9,ctx.Theme.TextMuted);
        DrawingPrimitives.Text(c,ctx,"CPU",Column(rect,.66f,.82f),9,ctx.Theme.TextMuted,false,SKTextAlign.Right);
        DrawingPrimitives.Text(c,ctx,"RAM",Column(rect,.82f,1),9,ctx.Theme.TextMuted,false,SKTextAlign.Right);
    }
    private static void Row(SKCanvas c,ModuleRenderContext ctx,GuestInfo guest,SKRect rect,bool stale)
    {
        DrawingPrimitives.Surface(c,ctx,rect);var s=ctx.TextScale;var inner=SKRect.Inflate(rect,-8*s,-3*s);
        var name=Column(inner,0,.42f);var title=name;title.Bottom=title.Top+16*s;
        DrawingPrimitives.Text(c,ctx,guest.Name,title,12,stale?ctx.Theme.TextMuted:null,true);
        var id=name;id.Top=title.Bottom;DrawingPrimitives.Text(c,ctx,(guest.Kind==GuestKind.Vm?"VM #":"LXC #")+guest.Id,id,8.5f,ctx.Theme.TextMuted);
        DrawingPrimitives.Text(c,ctx,DrawingPrimitives.StateText(guest.State),Column(inner,.43f,.66f),10,DrawingPrimitives.StateColor(ctx,guest.State,stale));
        DrawingPrimitives.Text(c,ctx,DisplayFormatting.Percent(guest.Cpu.Percent),Column(inner,.66f,.82f),10.5f,stale?ctx.Theme.TextMuted:null,false,SKTextAlign.Right);
        var ram=Column(inner,.82f,1);
        DrawingPrimitives.Text(c,ctx,DisplayFormatting.Percent(guest.Ram.Percent),ram,10.5f,stale?ctx.Theme.TextMuted:null,false,SKTextAlign.Right);
    }
    private static void Tile(SKCanvas c,ModuleRenderContext ctx,GuestInfo guest,SKRect rect,bool stale)
    {
        DrawingPrimitives.Surface(c,ctx,rect);var s=ctx.TextScale;var x=rect.Left+8*s;var right=rect.Right-8*s;
        DrawingPrimitives.Text(c,ctx,(guest.Kind==GuestKind.Vm?"VM #":"LXC #")+guest.Id,new(x,rect.Top+6*s,x+(right-x)*.45f,rect.Top+19*s),9,ctx.Theme.TextMuted);
        DrawingPrimitives.Text(c,ctx,DrawingPrimitives.StateText(guest.State),new(x+(right-x)*.45f,rect.Top+6*s,right,rect.Top+19*s),9,DrawingPrimitives.StateColor(ctx,guest.State,stale),false,SKTextAlign.Right);
        DrawingPrimitives.Text(c,ctx,guest.Name,new(x,rect.Top+24*s,right,rect.Top+44*s),13,stale?ctx.Theme.TextMuted:null,true);
        DrawingPrimitives.Text(c,ctx,"CPU "+DisplayFormatting.Percent(guest.Cpu.Percent),new(x,rect.Bottom-25*s,x+(right-x)*.5f,rect.Bottom-11*s),10,ctx.Theme.TextMuted);
        DrawingPrimitives.Text(c,ctx,"RAM "+DisplayFormatting.Percent(guest.Ram.Percent),new(x+(right-x)*.5f,rect.Bottom-25*s,right,rect.Bottom-11*s),10,ctx.Theme.TextMuted,false,SKTextAlign.Right);
    }
    private static void Storage(SKCanvas c,ModuleRenderContext ctx,StorageInfo storage,SKRect rect,bool stale)
    {
        DrawingPrimitives.Surface(c,ctx,rect);var s=ctx.TextScale;var content=SKRect.Inflate(rect,-7*s,-5*s);
        DrawingPrimitives.Text(c,ctx,storage.Name,new(content.Left,content.Top,content.Left+content.Width*.65f,content.Top+15*s),10,ctx.Theme.TextMuted);
        DrawingPrimitives.Text(c,ctx,DisplayFormatting.Percent(storage.Usage.Percent),new(content.Left+content.Width*.65f,content.Top,content.Right,content.Top+15*s),12,DrawingPrimitives.Tone(ctx,storage.Usage,stale),true,SKTextAlign.Right);
        var detail=storage.Enabled&&storage.Active?DisplayFormatting.Bytes(storage.Usage.UsedBytes)+" / "+DisplayFormatting.Bytes(storage.Usage.TotalBytes):"Indisponible";
        DrawingPrimitives.Text(c,ctx,detail,new(content.Left,content.Top+16*s,content.Right,rect.Bottom-7*s),8.5f,ctx.Theme.TextMuted);
        ModuleDrawing.DrawBar(c,new(content.Left,rect.Bottom-5*s,content.Right,rect.Bottom-2*s),(float)Math.Clamp((storage.Usage.Percent??0)/100,0,1),DrawingPrimitives.Tone(ctx,storage.Usage,stale),ctx.Theme);
    }
}
