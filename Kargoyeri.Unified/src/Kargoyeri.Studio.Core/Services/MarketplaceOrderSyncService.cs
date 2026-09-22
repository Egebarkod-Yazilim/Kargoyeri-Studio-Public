using Kargoyeri.Application.Services;
using Kargoyeri.Studio.Core.Infrastructure;
using Kargoyeri.Studio.Core.Infrastructure.OrderChannels;

namespace Kargoyeri.Studio.Core.Services;

/// <summary>
/// Tum tenant'lari tarayan ve aktif konfigure edilmis pazaryeri kanallarindan
/// her dakika siparis ceken arka plan servisi.
///
/// Calisma mantigi (per-tenant per-channel job):
///   1) 60 saniyelik tick.
///   2) Aktif (IsActive) tenant'lari listele.
///   3) Her tenant icin:
///      a) Legacy <see cref="MarketplaceWorkspaceSettings"/> (tek pazaryeri secimi):
///         HasConfiguration ve AutoSyncEnabled ise <see cref="MarketplaceOrderSyncEngine"/>
///         calistir — bu engine artik Live adapter bridge'i uzerinden 7 platformu da destekliyor.
///      b) Multi-channel <see cref="OrderChannelMetadata"/> (Pazarama, ÇiçekSepeti, Amazon, ...):
///         Enabled ve tum required field'lar dolu ise <see cref="OrderChannelSyncEngine"/> ile
///         o kanali tek tek sync et.
///   4) Kullanilmayan (Enabled=false veya HasConfiguration=false) kanallar atlanir — hic API cagrisi olmaz.
/// </summary>
public sealed class MarketplaceOrderSyncService : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MarketplaceOrderSyncService> _logger;

    public MarketplaceOrderSyncService(IServiceScopeFactory scopeFactory, ILogger<MarketplaceOrderSyncService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Servis basladiginda kisa bir gecikme — host warm-up sirasinda tum DI hazir olsun.
        try { await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOneTickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Marketplace auto sync tick'i hata verdi.");
            }

            try { await Task.Delay(TickInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task RunOneTickAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;
        var customerService = sp.GetRequiredService<CustomerService>();
        var legacyEngine    = sp.GetRequiredService<MarketplaceOrderSyncEngine>();
        var channelEngine   = sp.GetRequiredService<OrderChannelSyncEngine>();

        var profiles = await customerService.ListAllAsync(ct);

        foreach (var profile in profiles.Where(p => p.IsActive))
        {
            if (ct.IsCancellationRequested) return;

            var tenantKey = profile.TenantKey;

            // ── (a) Legacy tek-pazaryeri konfigurasyonu (Workspaces > Marketplace bolumu) ─
            try
            {
                var legacy = WorkspaceFeatureMetadata.ReadMarketplace(profile);
                if (legacy.HasConfiguration && legacy.AutoSyncEnabled)
                {
                    await legacyEngine.SyncTenantAsync(tenantKey, force: false, ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Legacy marketplace sync hata verdi (tenant={Tenant}).", tenantKey);
            }

            // ── (b) Multi-channel — her aktif kanal icin ayri job ─────────────
            //   OrderChannelMetadata.ReadAll, katalogtaki tum kanallari okur;
            //   Enabled=false veya zorunlu alanlar bos olanlar zaten skip edilir
            //   (SyncChannelAsync icinde de double-check var).
            try
            {
                var states = OrderChannelMetadata.ReadAll(profile);
                foreach (var state in states)
                {
                    if (ct.IsCancellationRequested) return;
                    if (!state.Enabled) continue;

                    var descriptor = OrderChannelCatalog.Find(state.Channel);
                    if (descriptor is null) continue;
                    if (descriptor.Stage == OrderChannelStage.ComingSoon) continue;
                    if (!state.HasRequiredFields(descriptor)) continue;

                    try
                    {
                        await channelEngine.SyncChannelAsync(
                            tenantKey,
                            state.Channel,
                            includeSinceLast: true,
                            ct);
                    }
                    catch (Exception channelEx)
                    {
                        _logger.LogWarning(channelEx,
                            "Channel sync hata verdi (tenant={Tenant}, channel={Channel}).",
                            tenantKey, state.Channel);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Multi-channel sync döngüsü hata verdi (tenant={Tenant}).", tenantKey);
            }
        }
    }
}
