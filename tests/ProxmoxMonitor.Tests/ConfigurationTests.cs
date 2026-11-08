using System.Text.Json;
using ProxmoxMonitor.Configuration;

namespace ProxmoxMonitor.Tests;

public sealed class ConfigurationTests
{
    [Fact]
    public void ValidAddress_UsesApprovedDefaults()
    {
        using var doc = JsonDocument.Parse("""{"address":"https://pve.example:8006"}""");
        Assert.True(SettingsReader.TryRead(doc.RootElement, out var value, out _));
        Assert.Equal(DetailLayout.Balanced, value!.Layout);
        Assert.Equal(TimeSpan.FromSeconds(10), value.RefreshInterval);
        Assert.Equal(TimeSpan.FromSeconds(8), value.PageDuration);
        Assert.False(value.AllowSelfSigned);
        Assert.False(value.ForceCompact);
        Assert.Equal("system", value.Disk.Kind);
    }

    [Theory]
    [InlineData("http://pve.example:8006")]
    [InlineData("https://user:password@pve.example")]
    [InlineData("https://pve.example?query=1")]
    [InlineData("https://pve.example#fragment")]
    [InlineData("https://pve.example/custom")]
    [InlineData("https://localhost.")]
    [InlineData("https://127.0.0.2")]
    [InlineData("https://0.0.0.0")]
    [InlineData("https://[::]")]
    [InlineData("https://[::1]")]
    [InlineData("https://[::ffff:127.0.0.1]")]
    [InlineData("https://127.1")]
    [InlineData("https://pve.exa\nmple")]
    [InlineData("")]
    public void UnsafeOrigins_AreRejectedBeforeNetwork(string address)
    {
        Assert.False(ConnectionOrigin.TryParse(address, out var origin, out var error));
        Assert.Null(origin);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Theory]
    [InlineData("https://PVE.EXAMPLE.:8006/", "https://pve.example:8006/")]
    [InlineData("https://pve.example:443/", "https://pve.example/")]
    [InlineData("https://[2001:db8::1]:8006", "https://[2001:db8::1]:8006/")]
    public void NetworkOrigin_IsNormalized(string input, string expected)
    {
        Assert.True(ConnectionOrigin.TryParse(input, out var origin, out _));
        Assert.Equal(expected, origin!.BaseUri.AbsoluteUri);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("""{"address":true}""")]
    [InlineData("""{"address":"https://pve.example","refreshSeconds":4}""")]
    [InlineData("""{"address":"https://pve.example","refreshSeconds":61}""")]
    [InlineData("""{"address":"https://pve.example","pageSeconds":2}""")]
    [InlineData("""{"address":"https://pve.example","pageSeconds":31}""")]
    [InlineData("""{"address":"https://pve.example","forceCompact":"true"}""")]
    [InlineData("""{"address":"https://pve.example","layout":"unknown"}""")]
    [InlineData("""{"address":"https://pve.example","disk":{"kind":"storage","id":"local"}}""")]
    public void InvalidSettings_AreNotConfigured(string json)
    {
        using var doc = JsonDocument.Parse(json);
        Assert.False(SettingsReader.TryRead(doc.RootElement, out _, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Theory]
    [InlineData(5, 3)]
    [InlineData(60, 30)]
    public void Settings_AcceptApprovedBounds(int refresh, int page)
    {
        var json = JsonSerializer.SerializeToElement(new { address = "https://pve.example", refreshSeconds = refresh, pageSeconds = page, layout = "mosaic" });
        Assert.True(SettingsReader.TryRead(json, out var value, out _));
        Assert.Equal(TimeSpan.FromSeconds(refresh), value!.RefreshInterval);
        Assert.Equal(TimeSpan.FromSeconds(page), value.PageDuration);
        Assert.Equal(DetailLayout.Mosaic, value.Layout);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("secret-only")]
    [InlineData("monitor@pve!=secret")]
    [InlineData("monitor!screen=secret")]
    [InlineData("monitor@pve!screen=abc\r\nHeader: value")]
    [InlineData("monitor@pve!screen=secret\r\n")]
    public void InvalidToken_DoesNotExposeItsContents(string? text)
    {
        Assert.False(ApiToken.TryParse(text, out var value, out var error));
        Assert.Null(value);
        Assert.DoesNotContain("secret", error ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CompleteToken_IsUsableButItsDisplayIsRedacted()
    {
        const string fake = "monitor@pve!screen=fake-secret-only-for-tests";
        Assert.True(ApiToken.TryParse(fake, out var token, out _));
        Assert.Equal("PVEAPIToken=" + fake, token!.AuthorizationValue);
        Assert.Equal(64, token.Fingerprint.Length);
        Assert.DoesNotContain("fake-secret", token.ToString());
    }
}
