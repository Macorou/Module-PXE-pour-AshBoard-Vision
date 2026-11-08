using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
namespace ProxmoxMonitor.Tests;
public sealed class PackagingTests
{
    private static readonly string Root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../"));
    private static async Task<(int Exit,string Output)> Run(Dictionary<string,string>? changes=null,bool validateOnly=true,string? output=null)
    {
        var values=new Dictionary<string,string>{{"ModuleId","macorou.proxmox-monitor"},{"Author","Macorou"},{"Repository","Macorou/Module-PXE-pour-AshBoard-Vision"},{"Version","0.1.0-beta.2"}};
        if(changes is not null)foreach(var pair in changes)values[pair.Key]=pair.Value;
        var info=new ProcessStartInfo("pwsh"){WorkingDirectory=Root,RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false,CreateNoWindow=true};
        foreach(var argument in new[]{"-NoLogo","-NoProfile","-NonInteractive","-File",Path.Combine(Root,"scripts","pack.ps1")})info.ArgumentList.Add(argument);
        foreach(var pair in values){info.ArgumentList.Add("-"+pair.Key);info.ArgumentList.Add(pair.Value);}
        if(validateOnly)info.ArgumentList.Add("-ValidateOnly");
        if(output is not null){info.ArgumentList.Add("-OutputDirectory");info.ArgumentList.Add(output);}
        using var process=Process.Start(info)!;var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try{await process.WaitForExitAsync(timeout.Token);}catch{process.Kill(true);throw;}
        return(process.ExitCode,(await stdout)+(await stderr));
    }
    [Fact]
    public async Task ValidPublicMetadata_IsAcceptedWithoutBuilding()
    {var result=await Run();Assert.True(result.Exit==0,result.Output);}
    [Theory]
    [InlineData("ModuleId","dev.proxmox-monitor")]
    [InlineData("ModuleId","UPPERCASE.module")]
    [InlineData("Repository","")]
    [InlineData("Author","")]
    [InlineData("Version","latest")]
    [InlineData("Version","01.0.0")]
    public async Task InvalidMetadata_IsRejectedBeforeBuilding(string key,string value)
    {var result=await Run(new(){{key,value}});Assert.NotEqual(0,result.Exit);}
    [Fact]
    public void Manifest_HasOnlyRequiredPermissionsAndSizes()
    {
        using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(Root,"src","ProxmoxMonitor","manifest.json")));var m=document.RootElement;
        Assert.Equal(new[]{"internet","reseau-local"},m.GetProperty("permissions").EnumerateArray().Select(x=>x.GetString()).Order());
        Assert.Equal("1.0",m.GetProperty("sdk").GetString());
        Assert.Equal(4,m.GetProperty("minSize").GetProperty("columns").GetInt32());Assert.Equal(2,m.GetProperty("minSize").GetProperty("rows").GetInt32());
        Assert.Equal(6,m.GetProperty("presets").GetProperty("ideal").GetProperty("columns").GetInt32());
        Assert.Equal("macorou.proxmox-monitor",m.GetProperty("id").GetString());
    }
    [Fact]
    public async Task BuiltPackage_ContainsOnlyModuleAndPublicManifest()
    {
        var output=Path.Combine(Root,"artifacts","package-tests",Guid.NewGuid().ToString("N"));
        var result=await Run(validateOnly:false,output:output);Assert.True(result.Exit==0,result.Output);
        var package=Path.Combine(output,"macorou.proxmox-monitor-0.1.0-beta.2.avmod");Assert.True(File.Exists(package),result.Output);
        Assert.True(File.Exists(package+".sha256"));
        using var zip=ZipFile.OpenRead(package);Assert.Equal(new[]{"LICENSE","ProxmoxMonitor.dll","manifest.json"},zip.Entries.Select(e=>e.FullName).Order(StringComparer.Ordinal));
        using var reader=new StreamReader(zip.GetEntry("manifest.json")!.Open());var manifest=await reader.ReadToEndAsync();
        Assert.DoesNotContain("fake-secret",manifest);Assert.DoesNotContain("storage-catalog",manifest);
    }
}
