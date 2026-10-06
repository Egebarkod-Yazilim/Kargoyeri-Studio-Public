using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace Kargoyeri.Studio.Core.Infrastructure.OrderChannels.Adapters;

/// <summary>
/// N11 Marketplace SOAP API (gercek/canli).
/// WSDL: https://api.n11.com/ws/OrderService.wsdl
/// Endpoint: POST https://api.n11.com/ws/OrderService.wsdl
/// Auth:   SOAP envelope icinde &lt;auth&gt;&lt;appKey&gt;...&lt;/appKey&gt;&lt;appSecret&gt;...&lt;/appSecret&gt;&lt;/auth&gt;
/// Operasyon: OrderList (status: New|Approved|Shipped|Delivered|Completed|Claimed|LATE_SHIPMENT|Rejected)
/// Rate: 1000 req/dk; DetailedOrderList icin 5sn aralik
/// </summary>
public sealed class N11LiveAdapter : IOrderChannelAdapter
{
    private const string ServiceUrl = "https://api.n11.com/ws/OrderService.wsdl";
    private const string SoapNs = "http://www.n11.com/ws/schemas";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<N11LiveAdapter> _logger;

    public OrderChannelType Type => OrderChannelType.N11;

    public N11LiveAdapter(IHttpClientFactory httpClientFactory, ILogger<N11LiveAdapter> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<OrderChannelTestResult> TestConnectionAsync(
        OrderChannelCredentials credentials, CancellationToken ct)
    {
        var (appKey, appSecret, missing) = ReadCreds(credentials);
        if (missing is not null) return new OrderChannelTestResult(false, missing);

        try
        {
            var envelope = BuildOrderListEnvelope(appKey!, appSecret!, status: "Approved", currentPage: 0, pageSize: 1);
            var (statusCode, body) = await PostSoapAsync(envelope, "OrderList", ct);

            if (statusCode is >= 200 and < 300 && !body.Contains("<errorCode>", StringComparison.OrdinalIgnoreCase))
            {
                return new OrderChannelTestResult(true, "N11 SOAP API bilgileri dogrulandi.");
            }

            var err = ExtractError(body) ?? Truncate(body, 300);
            return new OrderChannelTestResult(false, $"N11 API yanit hatasi: {err}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "N11 test connection hatasi (tenant={Tenant})", credentials.TenantKey);
            return new OrderChannelTestResult(false, "N11 SOAP servise ulasilamadi: " + ex.Message);
        }
    }

    public async Task<OrderChannelFetchResult> FetchOrdersAsync(
        OrderChannelCredentials credentials, DateTimeOffset since, CancellationToken ct)
    {
        var (appKey, appSecret, missing) = ReadCreds(credentials);
        if (missing is not null) return new OrderChannelFetchResult(false, missing, Array.Empty<RawIncomingOrder>());

        var allOrders = new List<RawIncomingOrder>();
        try
        {
            // "New" + "Approved" status'larini cek (en sik kullanilan kombinasyon)
            foreach (var status in new[] { "New", "Approved" })
            {
                var envelope = BuildOrderListEnvelope(appKey!, appSecret!, status, currentPage: 0, pageSize: 100);
                var (sc, body) = await PostSoapAsync(envelope, "OrderList", ct);
                if (sc < 200 || sc >= 300)
                {
                    return new OrderChannelFetchResult(false,
                        $"N11 API: HTTP {sc} — {Truncate(body, 200)}",
                        Array.Empty<RawIncomingOrder>());
                }

                var parsed = ParseOrders(body, credentials.TenantKey);
                allOrders.AddRange(parsed);
            }

            return new OrderChannelFetchResult(true,
                $"N11: {allOrders.Count} siparis cekildi (status=New+Approved).",
                allOrders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "N11 fetch hatasi (tenant={Tenant})", credentials.TenantKey);
            return new OrderChannelFetchResult(false,
                "N11 SOAP siparis cekiminde hata: " + ex.Message,
                Array.Empty<RawIncomingOrder>());
        }
    }

    private async Task<(int statusCode, string body)> PostSoapAsync(string envelope, string soapAction, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("n11-orders");
        client.Timeout = TimeSpan.FromSeconds(45);
        using var content = new StringContent(envelope, Encoding.UTF8, "text/xml");
        content.Headers.Add("SOAPAction", soapAction);
        using var resp = await client.PostAsync(ServiceUrl, content, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        return ((int)resp.StatusCode, body);
    }

    private static string BuildOrderListEnvelope(string appKey, string appSecret, string status, int currentPage, int pageSize)
    {
        // N11 SOAP envelope (manuel kurulum — WCF/svcutil bagimliligi yok)
        return $"""
        <?xml version="1.0" encoding="utf-8"?>
        <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:sch="{SoapNs}">
          <soapenv:Header/>
          <soapenv:Body>
            <sch:OrderListRequest>
              <auth>
                <appKey>{Esc(appKey)}</appKey>
                <appSecret>{Esc(appSecret)}</appSecret>
              </auth>
              <searchData>
                <status>{Esc(status)}</status>
              </searchData>
              <pagingData>
                <currentPage>{currentPage.ToString(CultureInfo.InvariantCulture)}</currentPage>
                <pageSize>{pageSize.ToString(CultureInfo.InvariantCulture)}</pageSize>
              </pagingData>
            </sch:OrderListRequest>
          </soapenv:Body>
        </soapenv:Envelope>
        """;
    }

    private static IReadOnlyList<RawIncomingOrder> ParseOrders(string xml, string tenantKey)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            var orders = doc.Descendants().Where(e => e.Name.LocalName == "order");
            var result = new List<RawIncomingOrder>();
            foreach (var o in orders)
            {
                string? Val(string name) => o.Descendants().FirstOrDefault(e => e.Name.LocalName == name)?.Value;

                var orderNumber = Val("orderNumber") ?? Val("id");
                if (string.IsNullOrEmpty(orderNumber)) continue;

                DateTimeOffset.TryParse(Val("createDate"), CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal, out var createdAt);

                var billing = o.Descendants().FirstOrDefault(e => e.Name.LocalName == "shippingAddress")
                              ?? o.Descendants().FirstOrDefault(e => e.Name.LocalName == "billingAddress");

                string? AddrVal(string name) => billing?.Descendants()
                    .FirstOrDefault(e => e.Name.LocalName == name)?.Value;

                var recipient = new RawAddress(
                    FullName: AddrVal("fullName") ?? "N11 Musterisi",
                    Phone: AddrVal("gsm") ?? AddrVal("tel"),
                    Email: null,
                    City: AddrVal("city"),
                    District: AddrVal("district"),
                    AddressLine: AddrVal("address"),
                    PostalCode: AddrVal("postalCode"));

                var items = o.Descendants().Where(e => e.Name.LocalName == "orderItem")
                    .Select(it =>
                    {
                        string? IV(string n) => it.Descendants().FirstOrDefault(e => e.Name.LocalName == n)?.Value;
                        int qty = int.TryParse(IV("quantity"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var q) ? q : 1;
                        decimal? price = decimal.TryParse(IV("price"), NumberStyles.Number, CultureInfo.InvariantCulture, out var pp) ? pp : null;
                        return new RawOrderItem(
                            ProductName: IV("productName") ?? IV("title") ?? "N11 urun",
                            Quantity: qty,
                            UnitPrice: price,
                            Sku: IV("productSellerCode") ?? IV("productId"));
                    }).ToList();

                result.Add(new RawIncomingOrder(
                    SourceChannel: OrderChannelType.N11,
                    ChannelExternalOrderId: $"n11:{orderNumber}",
                    OrderNumber: orderNumber!,
                    CreatedAtUtc: createdAt == default ? DateTimeOffset.UtcNow : createdAt,
                    CollectionAmount: null,
                    CurrencyCode: "TRY",
                    Recipient: recipient,
                    Items: items,
                    Notes: $"N11 siparis ({tenantKey})"));
            }
            return result;
        }
        catch
        {
            return Array.Empty<RawIncomingOrder>();
        }
    }

    private static string? ExtractError(string body)
    {
        try
        {
            var doc = XDocument.Parse(body);
            var err = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "errorMessage");
            return err?.Value;
        }
        catch { return null; }
    }

    private static (string? appKey, string? appSecret, string? missingMsg) ReadCreds(OrderChannelCredentials c)
    {
        var k = c.Get("apiKey");
        var s = c.Get("apiSecret");
        if (string.IsNullOrWhiteSpace(k) || string.IsNullOrWhiteSpace(s))
            return (null, null, "N11 icin API Key ve API Secret zorunlu.");
        return (k, s, null);
    }

    private static string Esc(string s) =>
        System.Security.SecurityElement.Escape(s) ?? s;

    private static string Truncate(string s, int max) => s.Length <= max ? s : s.Substring(0, max) + "...";
}
