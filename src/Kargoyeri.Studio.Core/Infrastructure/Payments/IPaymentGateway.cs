namespace Kargoyeri.Studio.Core.Infrastructure.Payments;

/// <summary>
/// P5-#3 — Tahsilat saglayicisi soyutlamasi.
/// Implementasyonlar: IyzicoPaymentGateway, ParamPaymentGateway, PayTrPaymentGateway, NoopPaymentGateway (dev).
/// </summary>
public interface IPaymentGateway
{
    /// <summary>Saglayici adi (orn: "iyzico", "param", "paytr", "noop").</summary>
    string Name { get; }

    /// <summary>3D-Secure baslatir; kullaniciya yonlendirilecek HTML/URL doner.</summary>
    Task<PaymentInitiationResult> InitiateAsync(PaymentRequest request, CancellationToken ct);

    /// <summary>Saglayicidan donen callback'i dogrular (HMAC/imza), odeme durumunu cozer.</summary>
    Task<PaymentCallbackResult> HandleCallbackAsync(IDictionary<string, string> formOrQuery, CancellationToken ct);

    /// <summary>Iade (full/partial). Provider destekliyorsa.</summary>
    Task<PaymentRefundResult> RefundAsync(string providerTransactionId, decimal amount, string? reason, CancellationToken ct);
}

public sealed record PaymentRequest(
    string ConversationId,
    string TenantKey,
    string TenantEmail,
    string TenantName,
    decimal Amount,
    string Currency,
    string Description,
    string CallbackUrl,
    string? IpAddress);

public sealed record PaymentInitiationResult(
    bool Success,
    string? RedirectUrl,
    string? Html3D,
    string? ProviderRef,
    string? ErrorCode,
    string? ErrorMessage);

public sealed record PaymentCallbackResult(
    bool Paid,
    string? ProviderTransactionId,
    string? ConversationId,
    decimal? Amount,
    string? RawStatus,
    string? ErrorMessage);

public sealed record PaymentRefundResult(
    bool Success,
    string? ProviderRefundId,
    string? ErrorMessage);
