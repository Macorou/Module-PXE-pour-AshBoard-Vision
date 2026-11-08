using SkiaSharp;
using Trofeo.ModuleKit;
namespace ProxmoxMonitor.Rendering;
internal static class CompactRenderer
{
    internal static void Draw(SKCanvas canvas,ModuleRenderContext context,ModulePresentation presentation,WidgetLayout layout)
    {
        canvas.Save();
        try
        {
            canvas.ClipRect(context.Bounds);DrawingPrimitives.Chrome(canvas,context,presentation,layout);
            var metrics=new[]{presentation.Cpu,presentation.Ram,presentation.Disk};
            for(var i=0;i<Math.Min(metrics.Length,layout.Metrics.Length);i++)DrawingPrimitives.MetricCard(canvas,context,metrics[i],layout.Metrics[i]);
        }
        finally{canvas.Restore();}
    }
}
