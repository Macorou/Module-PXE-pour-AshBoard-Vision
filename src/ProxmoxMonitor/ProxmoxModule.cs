using AshboardVision.Sdk;
using SkiaSharp;
using Trofeo.ModuleKit;
using ProxmoxMonitor.Configuration;
using ProxmoxMonitor.Connection;
using System.Text.Json;
using ProxmoxMonitor.Monitoring;
using ProxmoxMonitor.Rendering;
namespace ProxmoxMonitor;
public class ProxmoxModule : CommunityModule
{
    internal virtual HttpClient CreateHttpClient(string? certificateKey)=>Services.CreateHttpClient(certificateKey);
    private ConnectionCoordinator Coordinator(ProxmoxSettings settings)=>new(settings,new ConnectionStore(Services.Secrets),
        new CertificateTrust(CreateHttpClient,Services.Secrets,Services.Time),new StorageCatalog(Services.DataDirectory),CreateHttpClient,Services.Time);
    public override async Task<ActionResult> RunActionAsync(string actionKey,CancellationToken ct)
    {
        if(!SettingsReader.TryRead(Services.Settings,out var settings,out var error))return new(false,error??"Vérifie l’adresse du serveur.");
        var coordinator=Coordinator(settings!);
        if(actionKey=="forgetCertificate")return coordinator.ForgetCertificate();
        if(actionKey!="testConnection")return new(false,"Action inconnue.");
        if(!Services.Has(ModulePermission.LocalNetwork)&&!Services.Has(ModulePermission.Internet))return new(false,"Accorde la permission Réseau local ou Internet avant de tester la connexion.");
        return await coordinator.TestAsync(ct).ConfigureAwait(false);
    }
    public override Task<IReadOnlyList<LookupResult>> LookupAsync(string fieldKey,string query,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if(fieldKey!="disk")return Task.FromResult<IReadOnlyList<LookupResult>>([]);
        // The SDK supplies the system default through emptyLabel. A fabricated
        // result here duplicates that row and hides its Reload button before discovery.
        var results=new List<LookupResult>();
        var store=new ConnectionStore(Services.Secrets);var binding=store.ReadBinding();
        if(binding is not null&&store.ReadToken()?.Fingerprint==binding.TokenFingerprint&&new StorageCatalog(Services.DataDirectory).Read(binding) is {} catalog)
            foreach(var storage in catalog.Storages)
            {
                var label=storage.Name+" — "+binding.Node;
                if(!label.Contains(query,StringComparison.OrdinalIgnoreCase))continue;
                results.Add(new(label,JsonSerializer.SerializeToElement(new {kind="storage",origin=binding.Origin,id=storage.Id,name=label})));
            }
        return Task.FromResult<IReadOnlyList<LookupResult>>(results);
    }
    private string _message="Indique l’adresse du serveur et ton jeton dans les paramètres.";
    private CancellationTokenSource? _stop;
    private Task? _background;
    private ITimer? _redrawTimer;
    private MonitoringLoop? _loop;
    private ProxmoxSettings? _settings;
    private int _disposed;
    public override Task StartAsync(CancellationToken ct)
    {
        if (!SettingsReader.TryRead(Services.Settings,out var settings,out var error))
        {_message=error??_message;SetStatus(ModuleHealth.NotConfigured,_message);return Task.CompletedTask;}
        if(!Services.Has(ModulePermission.LocalNetwork)&&!Services.Has(ModulePermission.Internet))
        {_message="Accorde la permission Réseau local ou Internet dans les paramètres du module.";SetStatus(ModuleHealth.Offline,_message);return Task.CompletedTask;}
        if(_loop is not null||Volatile.Read(ref _disposed)!=0)return Task.CompletedTask;
        _settings=settings;
        _message="Actualisation de Proxmox…";SetStatus(ModuleHealth.Offline,_message);
        _stop=CancellationTokenSource.CreateLinkedTokenSource(ct);
        var store=new ConnectionStore(Services.Secrets);
        _loop=new MonitoringLoop(new ProxmoxMonitoringSource(Coordinator(settings!),store,settings!),settings!,Services.Time);
        _loop.SnapshotChanged+=OnSnapshot;
        _loop.StatusChanged+=OnStatus;
        _redrawTimer=Services.Time.CreateTimer(_=>Invalidate(),null,TimeSpan.FromSeconds(1),TimeSpan.FromSeconds(1));
        _background=Task.Run(()=>_loop.RunAsync(_stop.Token),CancellationToken.None);
        return Task.CompletedTask;
    }
    private void OnSnapshot(MonitoringSnapshot snapshot)=>Invalidate();
    private void OnStatus(ModuleHealth health,string? message){_message=message??"Données actualisées.";SetStatus(health,message);}
    public override async ValueTask DisposeAsync()
    {
        if(Interlocked.Exchange(ref _disposed,1)!=0)return;
        if(_stop is not null)await _stop.CancelAsync().ConfigureAwait(false);
        _redrawTimer?.Dispose();
        if(_background is not null)await _background.ConfigureAwait(false);
        if(_loop is not null){_loop.SnapshotChanged-=OnSnapshot;_loop.StatusChanged-=OnStatus;}
        _stop?.Dispose();
    }
    public override void Render(SKCanvas canvas,ModuleRenderContext ctx)
    {
        if(_settings is null||_loop is null){ModuleDrawing.DrawActionMessage(canvas,ctx.Bounds,ctx.Theme,ctx.TextScale,_message);return;}
        var view=ModulePresentation.Create(_loop.Current,_settings,ctx.Now);
        var layout=LayoutEngine.Measure(ctx,view,_settings);
        if(layout.IsCompact)CompactRenderer.Draw(canvas,ctx,view,layout);else DetailedRenderers.Draw(canvas,ctx,view,layout,_settings.Layout);
    }
}
