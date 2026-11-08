using AshboardVision.Sdk.Testing;
using Trofeo.ModuleKit;

namespace ProxmoxMonitor.Tests;

public sealed class UnconfiguredModuleTests
{
    [Theory]
    [InlineData("classique",4,2)]
    [InlineData("liquid-glass",6,4)]
    [InlineData("pixel",4,2)]
    public async Task EmptyConfiguration_IsExplainedAndDrawn(string theme,int columns,int rows)
    {
        await using var host = new ModuleTestHost<ProxmoxModule>(Path.Combine(AppContext.BaseDirectory,"manifest.json"));
        await host.StartAsync();
        Assert.Equal(ModuleHealth.NotConfigured,host.Health);
        Assert.False(string.IsNullOrWhiteSpace(host.StatusMessage));
        using var bitmap = host.Render(new GridSize(columns,rows),theme);
        Assert.Contains(bitmap.Pixels,p=>p.Alpha>0);
    }
}
