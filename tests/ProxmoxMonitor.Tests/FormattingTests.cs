using ProxmoxMonitor.Monitoring;
namespace ProxmoxMonitor.Tests;
public sealed class FormattingTests
{
    [Theory]
    [InlineData(79.9,MetricSeverity.Normal)]
    [InlineData(80,MetricSeverity.Warning)]
    [InlineData(90,MetricSeverity.Critical)]
    public void Thresholds_UseApprovedBoundaries(double value,MetricSeverity severity)=>Assert.Equal(severity,DisplayFormatting.Severity(value));
    [Theory]
    [InlineData(null,"—")]
    [InlineData(23d,"23 %")]
    [InlineData(56.5,"57 %")]
    [InlineData(double.NaN,"—")]
    public void Percent_IsHumanReadable(double? value,string expected)=>Assert.Equal(expected,DisplayFormatting.Percent(value));
    [Theory]
    [InlineData(1048576L,"1 Mio")]
    [InlineData(1073741824L,"1 Gio")]
    [InlineData(1099511627776L,"1 Tio")]
    public void Bytes_UseBinaryUnits(long bytes,string expected)=>Assert.Equal(expected,DisplayFormatting.Bytes(bytes));
}
