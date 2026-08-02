using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AirportApp.Settings;
using Microsoft.Extensions.Options;

namespace AirportApp.Services.Payments;

public sealed record PayPalOrderResult(string OrderId, string ApprovalUrl, string Status);
public sealed record PayPalCaptureResult(string Status, string CaptureId);

public sealed class PayPalService(HttpClient httpClient, IOptions<PayPalSettings> options)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly PayPalSettings settings = options.Value;

    public async Task<PayPalOrderResult> CreateOrderAsync(
        decimal total,
        string reference,
        bool includeRedirectUrls = true,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var accessToken = await GetAccessTokenAsync(cancellationToken);
        var purchaseUnit = new
        {
            reference_id = reference,
            description = reference,
            custom_id = reference,
            amount = new
            {
                currency_code = settings.CurrencyCode,
                value = total.ToString("0.00", CultureInfo.InvariantCulture)
            }
        };

        if (includeRedirectUrls &&
            (string.IsNullOrWhiteSpace(settings.ReturnUrl) ||
             string.IsNullOrWhiteSpace(settings.CancelUrl)))
        {
            throw new InvalidOperationException("La integración de PayPal no está configurada.");
        }

        object body = includeRedirectUrls
            ? new
            {
                intent = "CAPTURE",
                purchase_units = new[] { purchaseUnit },
                application_context = new
                {
                    brand_name = "AirportApp",
                    return_url = settings.ReturnUrl,
                    cancel_url = settings.CancelUrl,
                    user_action = "PAY_NOW"
                }
            }
            : new { intent = "CAPTURE", purchase_units = new[] { purchaseUnit } };

        using var request = new HttpRequestMessage(HttpMethod.Post, BuildApiUrl("/v2/checkout/orders"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("Prefer", "return=representation");
        request.Content = JsonContent.Create(body, options: JsonOptions);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw GatewayFailure("crear la orden", response.StatusCode);
        }

        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;
        var id = root.TryGetProperty("id", out var idElement)
            ? idElement.GetString() ?? string.Empty
            : string.Empty;

        if (id.Length == 0)
        {
            throw new InvalidOperationException("PayPal devolvió una respuesta incompleta.");
        }

        var status = root.TryGetProperty("status", out var statusElement)
            ? statusElement.GetString() ?? "CREATED"
            : "CREATED";
        return new PayPalOrderResult(id, FindApprovalUrl(root), status);
    }

    public async Task<PayPalCaptureResult> CaptureOrderAsync(
        string orderId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderId);
        EnsureConfigured();
        var accessToken = await GetAccessTokenAsync(cancellationToken);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            BuildApiUrl($"/v2/checkout/orders/{Uri.EscapeDataString(orderId)}/capture"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("Prefer", "return=representation");
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw GatewayFailure("capturar la orden", response.StatusCode);
        }

        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;
        var status = root.TryGetProperty("status", out var statusElement)
            ? statusElement.GetString() ?? string.Empty
            : string.Empty;
        return new PayPalCaptureResult(status, FindCaptureId(root));
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildApiUrl("/v1/oauth2/token"));
        var credentials = Convert.ToBase64String(
            Encoding.ASCII.GetBytes($"{settings.ClientId}:{settings.ClientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new FormUrlEncodedContent(
            [new KeyValuePair<string, string>("grant_type", "client_credentials")]);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw GatewayFailure("autenticar", response.StatusCode);
        }

        using var document = JsonDocument.Parse(content);
        var token = document.RootElement.TryGetProperty("access_token", out var tokenElement)
            ? tokenElement.GetString()
            : null;
        return !string.IsNullOrWhiteSpace(token)
            ? token
            : throw new InvalidOperationException("PayPal devolvió una respuesta incompleta.");
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(settings.ClientId) ||
            string.IsNullOrWhiteSpace(settings.ClientSecret) ||
            !Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("La integración de PayPal no está configurada.");
        }
    }

    private string BuildApiUrl(string path) => $"{settings.BaseUrl.TrimEnd('/')}{path}";

    private static InvalidOperationException GatewayFailure(string action, System.Net.HttpStatusCode status) =>
        new($"No fue posible {action} en PayPal (HTTP {(int)status}).");

    private static string FindApprovalUrl(JsonElement root)
    {
        if (!root.TryGetProperty("links", out var links) || links.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        foreach (var link in links.EnumerateArray())
        {
            if (link.TryGetProperty("rel", out var rel) &&
                string.Equals(rel.GetString(), "approve", StringComparison.OrdinalIgnoreCase) &&
                link.TryGetProperty("href", out var href))
            {
                return href.GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static string FindCaptureId(JsonElement root)
    {
        if (!root.TryGetProperty("purchase_units", out var units) ||
            units.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        foreach (var unit in units.EnumerateArray())
        {
            if (!unit.TryGetProperty("payments", out var payments) ||
                !payments.TryGetProperty("captures", out var captures) ||
                captures.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var capture in captures.EnumerateArray())
            {
                if (capture.TryGetProperty("id", out var id))
                {
                    return id.GetString() ?? string.Empty;
                }
            }
        }

        return string.Empty;
    }
}
