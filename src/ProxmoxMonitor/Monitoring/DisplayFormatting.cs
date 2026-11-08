using System.Globalization;
namespace ProxmoxMonitor.Monitoring;
internal static class DisplayFormatting
{
    private static readonly CultureInfo French=CultureInfo.GetCultureInfo("fr-FR");
    internal static string Percent(double? value)=>value is {} percent&&double.IsFinite(percent)&&percent>=0?
        Math.Round(percent,0,MidpointRounding.AwayFromZero).ToString("0",French)+" %":"—";
    internal static string Bytes(long? value)
    {
        if(value is null or <0)return "—";
        var unit=value>=1L<<40?40:value>=1L<<30?30:20;
        return ((double)value.Value/(1L<<unit)).ToString("0.#",French)+" "+(unit==40?"Tio":unit==30?"Gio":"Mio");
    }
    internal static string Count(GuestSet? guests)=>guests is null?"Inconnu":(guests.Online?.ToString(French)??"?")+"/"+guests.Total.ToString(French)+" en ligne";
    internal static MetricSeverity Severity(double percent)=>percent>=90?MetricSeverity.Critical:percent>=80?MetricSeverity.Warning:MetricSeverity.Normal;
}
