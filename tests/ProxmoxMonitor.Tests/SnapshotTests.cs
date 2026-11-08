using ProxmoxMonitor.Api;
using ProxmoxMonitor.Monitoring;
using ProxmoxMonitor.Tests.Support;
namespace ProxmoxMonitor.Tests;
public sealed class SnapshotTests
{
    internal static readonly DateTimeOffset Now=new(2026,11,8,12,0,0,TimeSpan.Zero);
    internal static readonly Guid Revision=Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
    internal static ProbeBatch Batch()=>new("https://pve.example:8006/","pve",Revision,ApiReadResult.Ok(PveFixtures.Host()),ApiReadResult.Ok(PveFixtures.Vms()),ApiReadResult.Ok(PveFixtures.Lxc()),ApiReadResult.Ok(PveFixtures.Storages()),new(true,null,[]));
    [Fact]
    public void HostRatios_UseRealHostTotals()
    {
        var snapshot=SnapshotBuilder.Build(Batch(),null,Now);
        Assert.Equal(23,snapshot.Host.Value!.Cpu.Percent);
        Assert.Equal("56 %",DisplayFormatting.Percent(snapshot.Host.Value.Ram.Percent));
        Assert.Equal("32 %",DisplayFormatting.Percent(snapshot.Host.Value.SystemDisk.Percent));
        Assert.Equal("18 Gio",DisplayFormatting.Bytes(snapshot.Host.Value.Ram.UsedBytes));
        Assert.Equal(123456,snapshot.Host.Value.UptimeSeconds);
    }
    [Fact]
    public void CpuFraction_IsMultipliedOnce()
    {
        var snapshot=SnapshotBuilder.Build(Batch(),null,Now);
        Assert.Equal(12,snapshot.Vms.Value!.Guests[0].Cpu.Percent);
        Assert.Equal(25,snapshot.Vms.Value.Guests[0].Ram.Percent);
        Assert.Null(snapshot.Vms.Value.Guests[1].Cpu.Percent);
    }
    [Fact]
    public void Counts_ExcludeTemplatesAndSortById()
    {
        var vms=ApiReadResult.Ok(PveFixtures.Json("""[{"vmid":105,"status":"stopped"},{"vmid":101,"status":"running"},{"vmid":103,"status":"stopped"},{"vmid":100,"status":"running"},{"vmid":104,"status":"stopped"},{"vmid":999,"status":"stopped","template":true}]"""));
        var data=SnapshotBuilder.Build(Batch() with {Vms=vms},null,Now).Vms.Value!;
        Assert.Equal("2/5 en ligne",DisplayFormatting.Count(data));
        Assert.Equal(new long[]{100,101,103,104,105},data.Guests.Select(g=>g.Id));
        Assert.Equal("VM 100",data.Guests[0].Name);
    }
    [Theory]
    [InlineData("paused",GuestState.Paused)]
    [InlineData("suspended",GuestState.Suspended)]
    [InlineData("future-state",GuestState.Unknown)]
    public void QmpState_IsNotFalselyCountedOnline(string state,GuestState expected)
    {
        var json=System.Text.Json.JsonSerializer.SerializeToElement(new[]{new{vmid=100,status="running",qmpstatus=state}});
        var data=SnapshotBuilder.Build(Batch() with {Vms=ApiReadResult.Ok(json)},null,Now).Vms.Value!;
        Assert.Equal(expected,data.Guests[0].State);
        Assert.Equal(expected==GuestState.Unknown?null:0,data.Online);
    }
    [Fact]
    public void MissingQmpAndCpu_StillPreserveRunningGuest()
    {
        var data=SnapshotBuilder.Build(Batch() with {Vms=ApiReadResult.Ok(PveFixtures.Json("""[{"vmid":100,"status":"running"}]"""))},null,Now).Vms.Value!;
        Assert.Equal(1,data.Online);Assert.Null(data.Guests[0].Cpu.Percent);
    }
    [Fact]
    public void UnknownStatus_IsQuestionMarkAndEmptyListIsReallyZero()
    {
        var snapshot=SnapshotBuilder.Build(Batch() with {Vms=ApiReadResult.Ok(PveFixtures.Json("""[{"vmid":100,"status":"future"}]""")),Containers=ApiReadResult.Ok(PveFixtures.Json("[]"))},null,Now);
        Assert.Equal("?/1 en ligne",DisplayFormatting.Count(snapshot.Vms.Value));
        Assert.Equal("0/0 en ligne",DisplayFormatting.Count(snapshot.Containers.Value));
    }
    [Theory]
    [InlineData("""[{"name":"missing id","status":"running"}]""")]
    [InlineData("""[{"vmid":100,"status":"running"},{"vmid":100,"status":"running"}]""")]
    public void InvalidGuestInventory_IsNotSilentlyCountedAsZero(string json)
    {
        var snapshot=SnapshotBuilder.Build(Batch() with {Vms=ApiReadResult.Ok(PveFixtures.Json(json))},null,Now);
        Assert.Null(snapshot.Vms.Value);Assert.NotNull(snapshot.Vms.Failure);
        Assert.Equal("1/1 en ligne",DisplayFormatting.Count(snapshot.Containers.Value));
    }
    [Fact]
    public void VmNetworkFailure_PreservesOnlyItsPreviousTimestamp()
    {
        var before=SnapshotBuilder.Build(Batch(),null,Now);
        var later=SnapshotBuilder.Build(Batch() with {Vms=ApiReadResult.Fail(ApiFailureKind.Network,"Réseau indisponible.")},before,Now.AddSeconds(10));
        Assert.Equal(Now,later.Vms.LastSuccess);Assert.NotNull(later.Vms.Failure);
        Assert.Equal(Now.AddSeconds(10),later.Containers.LastSuccess);
    }
    [Fact]
    public void BadMetricsAndOfflineStorage_AreUnavailable()
    {
        var host=ApiReadResult.Ok(PveFixtures.Json("""{"cpu":-1,"memory":{"used":10,"total":0},"rootfs":{"used":20,"total":10}}"""));
        var storages=ApiReadResult.Ok(PveFixtures.Json("""[{"storage":"a","enabled":true,"active":false,"used":10,"total":20},{"storage":"b","enabled":1,"active":1,"used":10,"total":20}]"""));
        var snapshot=SnapshotBuilder.Build(Batch() with {Host=host,Storages=storages},null,Now);
        Assert.Null(snapshot.Host.Value!.Cpu.Percent);Assert.Null(snapshot.Host.Value.Ram.Percent);Assert.Null(snapshot.Host.Value.SystemDisk.Percent);
        Assert.Null(snapshot.Storages.Value!.Items[0].Usage.Percent);Assert.Equal(50,snapshot.Storages.Value.Items[1].Usage.Percent);
        Assert.Equal(2,snapshot.Storages.Value.Items.Length);
    }
    [Fact]
    public void HugeUptime_IsUnavailableInsteadOfBreakingRendering()
    {
        var host=ApiReadResult.Ok(PveFixtures.Json("""{"uptime":9223372036854775807}"""));
        Assert.Null(SnapshotBuilder.Build(Batch() with{Host=host},null,Now).Host.Value!.UptimeSeconds);
    }
}
