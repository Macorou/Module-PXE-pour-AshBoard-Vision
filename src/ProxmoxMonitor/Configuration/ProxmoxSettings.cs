namespace ProxmoxMonitor.Configuration;

internal enum DetailLayout { Balanced, Resources, Mosaic }
internal sealed record DiskSelection(string Kind = "system",string? Origin = null,string? Id = null);
internal sealed record ProxmoxSettings(ConnectionOrigin Origin, DetailLayout Layout, bool ForceCompact, bool AllowSelfSigned,
    DiskSelection Disk, TimeSpan RefreshInterval, TimeSpan PageDuration);
