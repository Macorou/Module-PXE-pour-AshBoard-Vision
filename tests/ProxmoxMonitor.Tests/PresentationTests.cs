using ProxmoxMonitor.Configuration;
using ProxmoxMonitor.Rendering;
using ProxmoxMonitor.Tests.Support;
namespace ProxmoxMonitor.Tests;
public sealed class PresentationTests
{
    [Fact]
    public void Compact_ShowsOnlyHostAndGlobalCounts()
    {
        var view=ModulePresentation.Create(RenderFixtures.Snapshot(5),RenderFixtures.Settings(),SnapshotTests.Now);
        var layout=LayoutEngine.Measure(RenderFixtures.Context(4,2),view,RenderFixtures.Settings());
        Assert.True(layout.IsCompact);Assert.Empty(layout.GuestItems);
        Assert.Equal("2/5 en ligne",view.VmCountText);Assert.Equal("3/4 en ligne",view.ContainerCountText);
        Assert.Equal(23,view.Cpu.Value.Percent);
    }
    [Fact]
    public void StorageChoice_IsBoundToOrigin()
    {
        var settings=RenderFixtures.Settings() with{Disk=new DiskSelection("storage","https://other.example/","local-lvm")};
        var view=ModulePresentation.Create(RenderFixtures.Snapshot(),settings,SnapshotTests.Now);
        Assert.Null(view.Disk.Value.Percent);Assert.Contains("sélection",view.Disk.Label,StringComparison.OrdinalIgnoreCase);
        settings=settings with{Disk=new DiskSelection("storage",settings.Origin.BaseUri.AbsoluteUri,"local-lvm")};
        view=ModulePresentation.Create(RenderFixtures.Snapshot(),settings,SnapshotTests.Now);
        Assert.Equal(60,view.Disk.Value.Percent);Assert.Contains("local-lvm",view.Disk.Label);
    }
    [Fact]
    public void ExpiredValues_AreRemoved()
    {
        var view=ModulePresentation.Create(RenderFixtures.Snapshot(),RenderFixtures.Settings(),SnapshotTests.Now.AddSeconds(31));
        Assert.Null(view.Cpu.Value.Percent);Assert.Equal("Inconnu",view.VmCountText);Assert.Empty(view.Guests);
    }
    [Fact]
    public void StaleFooter_DatesTheOldCategoryRatherThanTheFreshOnes()
    {
        var snapshot=RenderFixtures.Snapshot();
        var now=SnapshotTests.Now.AddSeconds(10);
        snapshot=snapshot with
        {
            Host=snapshot.Host with{Failure=new(ProxmoxMonitor.Api.ApiFailureKind.Network,"Réseau indisponible")},
            Vms=snapshot.Vms with{LastSuccess=now},
            Containers=snapshot.Containers with{LastSuccess=now},
            Storages=snapshot.Storages with{LastSuccess=now}
        };
        var view=ModulePresentation.Create(snapshot,RenderFixtures.Settings(),now);
        Assert.True(view.Cpu.Stale);
        Assert.Equal("Mesures anciennes · "+SnapshotTests.Now.ToString("HH:mm:ss"),view.FooterText);
    }
    [Fact]
    public void Counts_DoNotChangeAcrossPages()
    {
        var settings=RenderFixtures.Settings();var view=ModulePresentation.Create(RenderFixtures.Snapshot(40),settings,SnapshotTests.Now);
        var first=LayoutEngine.Measure(RenderFixtures.Context(),view,settings);
        var second=LayoutEngine.Measure(RenderFixtures.Context(now:SnapshotTests.Now.AddSeconds(8)),view,settings);
        Assert.True(first.PageCount>1);Assert.NotEqual(first.PageIndex,second.PageIndex);
        Assert.Equal("2/40 en ligne",view.VmCountText);
    }
}
