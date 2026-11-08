using ProxmoxMonitor.Monitoring;
using SkiaSharp;
using Trofeo.ModuleKit;
namespace ProxmoxMonitor.Rendering;
internal static class DrawingPrimitives
{
    private static readonly SKColor Green=new(72,210,155);
    private static readonly SKColor Red=new(248,113,133);
    internal static void Text(SKCanvas canvas,ModuleRenderContext ctx,string text,SKRect rect,float size,SKColor? color=null,bool bold=false,SKTextAlign align=SKTextAlign.Left)
    {
        if(rect.Width<=0||rect.Height<=0)return;
        using var font=ModuleDrawing.Font(ctx.Theme,size*ctx.TextScale);font.Embolden=bold;
        var height=font.Metrics.Descent-font.Metrics.Ascent;
        if(height>rect.Height)font.Size*=rect.Height/height;
        using var paint=new SKPaint{Color=color??ctx.Theme.Text,IsAntialias=true};
        var fitted=ModuleDrawing.Ellipsize(text,font,rect.Width);
        var x=align==SKTextAlign.Right?rect.Right:align==SKTextAlign.Center?rect.MidX:rect.Left;
        var baseline=rect.MidY-(font.Metrics.Ascent+font.Metrics.Descent)/2;
        ModuleDrawing.DrawText(canvas,fitted,x,baseline,align,font,paint);
    }
    internal static void Surface(SKCanvas canvas,ModuleRenderContext ctx,SKRect rect)
    {
        if(rect.Width<=0||rect.Height<=0)return;
        var radius=ctx.Theme.Id=="pixel"?0:7*ctx.TextScale;
        using var fill=new SKPaint{Color=ctx.Theme.Text.WithAlpha(9),IsAntialias=true};canvas.DrawRoundRect(rect,radius,radius,fill);
        using var stroke=new SKPaint{Color=ctx.Theme.Text.WithAlpha(15),Style=SKPaintStyle.Stroke,StrokeWidth=1,IsAntialias=true};
        canvas.DrawRoundRect(SKRect.Inflate(rect,-.5f,-.5f),radius,radius,stroke);
    }
    internal static SKColor Tone(ModuleRenderContext ctx,Metric value,bool stale=false)=>stale?ctx.Theme.TextMuted:value.Percent is not {} percent?ctx.Theme.TextMuted:
        DisplayFormatting.Severity(percent) switch{MetricSeverity.Critical=>IsLight(ctx)?new SKColor(185,34,69):Red,MetricSeverity.Warning=>ctx.Theme.Warning,_=>ctx.Theme.Accent};
    private static bool IsLight(ModuleRenderContext ctx)=>ctx.Theme.Background.Red*.2126+ctx.Theme.Background.Green*.7152+ctx.Theme.Background.Blue*.0722>150;
    private static SKColor Positive(ModuleRenderContext ctx)=>IsLight(ctx)?new SKColor(22,130,88):Green;
    internal static void MetricCard(SKCanvas canvas,ModuleRenderContext ctx,MetricView metric,SKRect rect)
    {
        Surface(canvas,ctx,rect);var s=ctx.TextScale;var pad=6*s;
        var content=SKRect.Inflate(rect,-pad,-pad);var tone=Tone(ctx,metric.Value,metric.Stale);
        if(rect.Height<58*s)
        {
            var line=new SKRect(content.Left,content.Top,content.Right,rect.Bottom-9*s);
            Text(canvas,ctx,metric.Label,new(line.Left,line.Top,line.Left+line.Width*.58f,line.Bottom),10,ctx.Theme.TextMuted);
            Text(canvas,ctx,DisplayFormatting.Percent(metric.Value.Percent),new(line.Left+line.Width*.58f,line.Top,line.Right,line.Bottom),17,tone,true,SKTextAlign.Right);
        }
        else
        {
            Text(canvas,ctx,metric.Label,new(content.Left,content.Top,content.Right,content.Top+14*s),10.5f,ctx.Theme.TextMuted);
            var detail=rect.Height>=70*s;
            var valueBottom=rect.Bottom-(detail?23:11)*s;
            Text(canvas,ctx,DisplayFormatting.Percent(metric.Value.Percent),new(content.Left,content.Top+16*s,content.Right,valueBottom),26,tone,true);
            if(detail)
            {
                var label=metric.Value.UsedBytes is not null?DisplayFormatting.Bytes(metric.Value.UsedBytes)+" / "+DisplayFormatting.Bytes(metric.Value.TotalBytes):metric.Value.Percent is null?"Indisponible":"Charge de l’hôte";
                Text(canvas,ctx,label,new(content.Left,rect.Bottom-23*s,content.Right,rect.Bottom-11*s),9.5f,ctx.Theme.TextMuted);
            }
        }
        var bar=new SKRect(content.Left,rect.Bottom-6*s,content.Right,rect.Bottom-3*s);
        ModuleDrawing.DrawBar(canvas,bar,(float)Math.Clamp((metric.Value.Percent??0)/100,0,1),tone,ctx.Theme);
    }
    internal static void Chrome(SKCanvas canvas,ModuleRenderContext ctx,ModulePresentation view,WidgetLayout layout)
    {
        var s=ctx.TextScale;var h=layout.Header;
        Text(canvas,ctx,"PROXMOX · "+view.Node,new(h.Left,h.Top,h.Right-95*s,h.Bottom),13.5f,null,true);
        var tone=view.HealthText=="En ligne"?Positive(ctx):view.HealthText=="Partiel"?ctx.Theme.Warning:ctx.Theme.TextMuted;
        Text(canvas,ctx,view.HealthText,new(h.Right-91*s,h.Top,h.Right,h.Bottom),10.5f,tone,false,SKTextAlign.Right);
        Counter(canvas,ctx,"VM",view.VmCountText,layout.Counters[0],view.VmStale);
        Counter(canvas,ctx,"Conteneurs",view.ContainerCountText,layout.Counters[1],view.ContainerStale);
        var footer=layout.Footer;var page=layout.PageCount>1?$"Page {layout.PageIndex+1}/{layout.PageCount}":"";
        Text(canvas,ctx,view.FooterText,new(footer.Left,footer.Top,footer.Right-(page.Length>0?85*s:0),footer.Bottom),9,ctx.Theme.TextMuted);
        if(page.Length>0)Text(canvas,ctx,page,new(footer.Right-82*s,footer.Top,footer.Right,footer.Bottom),9,ctx.Theme.TextMuted,false,SKTextAlign.Right);
    }
    private static void Counter(SKCanvas canvas,ModuleRenderContext ctx,string label,string count,SKRect rect,bool stale)
    {
        Surface(canvas,ctx,rect);var s=ctx.TextScale;var inner=SKRect.Inflate(rect,-8*s,-4*s);
        using var font=ModuleDrawing.Font(ctx.Theme,10.5f*s);var labelWidth=ModuleDrawing.MeasureText(label,font)+8*s;
        Text(canvas,ctx,label,new(inner.Left,inner.Top,inner.Left+labelWidth,inner.Bottom),10.5f,ctx.Theme.TextMuted);
        var right=new SKRect(inner.Left+labelWidth,inner.Top,inner.Right,inner.Bottom);
        using var numberFont=ModuleDrawing.Font(ctx.Theme,11.5f*s);
        if(ModuleDrawing.MeasureText(count,numberFont)>right.Width)count=count.Split(' ')[0];
        Text(canvas,ctx,count,right,11.5f,stale||count=="Inconnu"?ctx.Theme.TextMuted:ctx.Theme.Text,true,SKTextAlign.Right);
    }
    internal static SKColor StateColor(ModuleRenderContext ctx,GuestState state,bool stale)=>stale?ctx.Theme.TextMuted:state switch{GuestState.Running=>Positive(ctx),GuestState.Paused or GuestState.Suspended=>ctx.Theme.Warning,_=>ctx.Theme.TextMuted};
    internal static string StateText(GuestState state)=>state switch{GuestState.Running=>"En ligne",GuestState.Stopped=>"Arrêté",GuestState.Paused=>"En pause",GuestState.Suspended=>"Suspendu",_=>"Inconnu"};
}
