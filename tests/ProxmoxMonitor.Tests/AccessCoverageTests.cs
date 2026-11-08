using ProxmoxMonitor.Api;
using ProxmoxMonitor.Tests.Support;
namespace ProxmoxMonitor.Tests;
public sealed class AccessCoverageTests
{
    [Fact] public void FullPropagatedAudit_IsComplete() => Assert.True(AccessCoverage.Evaluate(PveFixtures.CompletePermissions(),PveFixtures.CompleteAcls(),"pve").Complete);
    [Theory]
    [InlineData("/vms/101")]
    [InlineData("/storage/private")]
    [InlineData("/pool/private")]
    [InlineData("/nodes/pve")]
    public void HiddenNoAccessChild_PreventsCompleteTotals(string path)
    {
        var acls=PveFixtures.Json(System.Text.Json.JsonSerializer.Serialize(new[] {new {path,type="token",ugid="monitor@pve!screen",roleid="NoAccess",propagate=1}}));
        var result=AccessCoverage.Evaluate(PveFixtures.CompletePermissions(),acls,"pve");
        Assert.False(result.Complete); Assert.Contains(path,result.MissingPaths);
    }
    [Fact]
    public void ExactVmGrant_WithPropagationZero_IsStillGranted()
    {
        var map=PveFixtures.CompletePermissions().GetRawText()[..^1]+",\"/vms/101\":{\"VM.Audit\":0}}";
        var acls=PveFixtures.Json("""[{"path":"/vms/101","type":"group","roleid":"PVEAuditor","propagate":0}]""");
        Assert.True(AccessCoverage.Evaluate(PveFixtures.Json(map),acls,"pve").Complete);
    }
    [Fact]
    public void UnpropagatedRoot_CannotProveAllGuests()
    {
        var map=PveFixtures.CompletePermissions().GetRawText().Replace("\"VM.Audit\":1","\"VM.Audit\":0");
        Assert.False(AccessCoverage.Evaluate(PveFixtures.Json(map),PveFixtures.CompleteAcls(),"pve").Complete);
    }
    [Fact]
    public void PoolMemberWithoutVmAudit_IsIncompleteEvenIfPoolIsVisible()
    {
        var map=PveFixtures.CompletePermissions().GetRawText()[..^1]+",\"/vms/101\":{\"Pool.Audit\":1}}";
        Assert.False(AccessCoverage.Evaluate(PveFixtures.Json(map),PveFixtures.CompleteAcls(),"pve").Complete);
    }
    [Fact] public void EmptyOrMalformedPermissions_AreIncomplete()
    {
        Assert.False(AccessCoverage.Evaluate(PveFixtures.Json("{}"),PveFixtures.CompleteAcls(),"pve").Complete);
        Assert.False(AccessCoverage.Evaluate(PveFixtures.Json("[]"),PveFixtures.CompleteAcls(),"pve").Complete);
        Assert.False(AccessCoverage.Evaluate(PveFixtures.CompletePermissions(),PveFixtures.Json("{}"),"pve").Complete);
    }
}
