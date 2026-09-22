namespace Kargoyeri.Studio.Core.Infrastructure.Payments;

/// <summary>
/// P5-#3 — Dev/test ortami icin: hicbir odeme yapmaz, hep "basarili" doner.
/// Production'da KULLANILMAZ.
/// </summary>
public sealed class NoopPaymentGateway : IPaymentGateway
{
    public string Name => "noop";

    public Task<PaymentInitiationResult> InitiateAsync(PaymentRequest request, CancellationToken ct)
    {
        // Otomatik onaylanmis sahte odeme
        var url = $"{request.CallbackUrl}?conversationId={Uri.EscapeDataString(request.ConversationId)}&status=success&amount={request.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        return Task.FromResult(new PaymentInitiationResult(
            Success: true,
            RedirectUrl: url,
            Html3D: null,
            ProviderRef: $"NOOP-{Guid.NewGuid():N}",
            ErrorCode: null,
            ErrorMessage: null));
    }

    public Task<PaymentCallbackResult> HandleCallbackAsync(IDictionary<string, string> formOrQuery, CancellationToken ct)
    {
        var status = formOrQuery.TryGetValue("status", out var st) ? st : "success";
        var convId = formOrQuery.TryGetValue("conversationId", out var c) ? c : null;
        var amountStr = formOrQuery.TryGetValue("amount", out var a) ? a : null;
        decimal? amount = decimal.TryParse(amountStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : null;
        return Task.FromResult(new PaymentCallbackResult(
            Paid: string.Equals(status, "success", StringComparison.OrdinalIgnoreCase),
            ProviderTransactionId: $"NOOP-TX-{Guid.NewGuid():N}",
            ConversationId: convId,
            Amount: amount,
            RawStatus: status,
            ErrorMessage: null));
    }

    public Task<PaymentRefundResult> RefundAsync(string providerTransactionId, decimal amount, string? reason, CancellationToken ct)
    {
        return Task.FromResult(new PaymentRefundResult(
            Success: true,
            ProviderRefundId: $"NOOP-REF-{Guid.NewGuid():N}",
            ErrorMessage: null));
    }
}
