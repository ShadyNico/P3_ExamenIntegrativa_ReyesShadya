using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AirportApp.Settings;
using Microsoft.Extensions.Options;

namespace AirportApp.Services.Payments;

public sealed record PayPhoneAmountBreakdown(
    int Amount,
    int AmountWithoutTax,
    int AmountWithTax,
    int Tax);

public sealed class PayPhoneApiLinkService(
    HttpClient httpClient,
    IOptions<PayPhoneSettings> options)
{
    private const string ApiBaseUrl = "https://pay.payphonetodoesposible.com/api";
    private readonly PayPhoneSettings settings = options.Value;

    public Task<string> CreatePaymentLinkAsync(
        decimal total,
        string clientTransactionId,
        string reference,
        CancellationToken cancellationToken = default)
    {
        var amountInCents = CommerceCalculations.ToCents(total);
        var amounts = new PayPhoneAmountBreakdown(
            amountInCents,
            amountInCents,
            0,
            0);

        return CreatePaymentLinkAsync(
            amounts,
            clientTransactionId,
            reference,
            cancellationToken);
    }

    public Task<string> CreatePaymentLinkAsync(
        decimal subtotal,
        decimal tax,
        decimal total,
        string clientTransactionId,
        string reference,
        CancellationToken cancellationToken = default) =>
        CreatePaymentLinkAsync(
            CalculateTaxedAmountBreakdown(subtotal, tax, total),
            clientTransactionId,
            reference,
            cancellationToken);

    public static PayPhoneAmountBreakdown CalculateTaxedAmountBreakdown(
        decimal subtotal,
        decimal tax,
        decimal total)
    {
        var subtotalInCents = CommerceCalculations.ToCents(subtotal);
        var taxInCents = CommerceCalculations.ToCents(tax);
        var totalInCents = CommerceCalculations.ToCents(total);
        if (subtotalInCents < 0 || taxInCents < 0 ||
            totalInCents != subtotalInCents + taxInCents)
        {
            throw new InvalidOperationException("Los importes de la orden no son consistentes.");
        }

        return new PayPhoneAmountBreakdown(
            totalInCents,
            0,
            subtotalInCents,
            taxInCents);
    }

    private async Task<string> CreatePaymentLinkAsync(
        PayPhoneAmountBreakdown amounts,
        string clientTransactionId,
        string reference,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        if (string.IsNullOrWhiteSpace(clientTransactionId) || clientTransactionId.Length > 15)
        {
            throw new ArgumentException(
                "El identificador de PayPhone debe contener entre 1 y 15 caracteres.",
                nameof(clientTransactionId));
        }

        var response = await SendCreateLinkAsync(
            amounts,
            clientTransactionId,
            reference,
            includeStoreId: !string.IsNullOrWhiteSpace(settings.StoreId),
            cancellationToken);

        if (!response.IsSuccess &&
            response.StatusCode == StatusCodes.Status404NotFound &&
            IsInvalidLinkResponse(response.Content) &&
            !string.IsNullOrWhiteSpace(settings.StoreId))
        {
            response = await SendCreateLinkAsync(
                amounts,
                clientTransactionId,
                reference,
                includeStoreId: false,
                cancellationToken);
        }

        if (response.StatusCode == StatusCodes.Status401Unauthorized)
        {
            throw new InvalidOperationException(
                "PayPhone rechazó el token API (HTTP 401). Verifica que Token y StoreID pertenezcan a la misma aplicación API habilitada.");
        }

        if (!response.IsSuccess)
        {
            throw new InvalidOperationException(
                BuildApiErrorMessage(
                    response.Content,
                    "No fue posible crear el enlace en PayPhone",
                    response.StatusCode));
        }

        var link = ReadPaymentLink(response.Content);
        return Uri.TryCreate(link, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
            ? link
            : throw new InvalidOperationException("PayPhone devolvió una respuesta incompleta.");
    }

    private async Task<PayPhoneApiResponse> SendCreateLinkAsync(
        PayPhoneAmountBreakdown amounts,
        string clientTransactionId,
        string reference,
        bool includeStoreId,
        CancellationToken cancellationToken)
    {
        var body = new Dictionary<string, object?>
        {
            ["amount"] = amounts.Amount,
            ["amountWithoutTax"] = amounts.AmountWithoutTax,
            ["amountWithTax"] = amounts.AmountWithTax,
            ["tax"] = amounts.Tax,
            ["service"] = 0,
            ["tip"] = 0,
            ["currency"] = "USD",
            ["clientTransactionId"] = clientTransactionId,
            ["reference"] = Truncate(reference, 100),
            ["additionalData"] = "AirportApp",
            ["oneTime"] = true,
            ["expireIn"] = 0,
            ["isAmountEditable"] = false
        };
        if (includeStoreId)
        {
            body["storeId"] = settings.StoreId.Trim();
        }

        using var request = NewAuthorizedRequest(HttpMethod.Post, $"{ApiBaseUrl}/Links");
        request.Content = JsonContent.Create(body);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return new PayPhoneApiResponse(
            response.IsSuccessStatusCode,
            (int)response.StatusCode,
            content);
    }

    private HttpRequestMessage NewAuthorizedRequest(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.Token.Trim());
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.AcceptLanguage.ParseAdd("es");
        return request;
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(settings.Token))
        {
            throw new InvalidOperationException("La integración de PayPhone no está configurada.");
        }
    }

    private static bool IsInvalidLinkResponse(string content) =>
        content.Contains("Link Inválido", StringComparison.OrdinalIgnoreCase) ||
        content.Contains("Link Invalido", StringComparison.OrdinalIgnoreCase);

    private static string ReadPaymentLink(string content)
    {
        try
        {
            return JsonSerializer.Deserialize<string>(content)?.Trim() ?? string.Empty;
        }
        catch (JsonException)
        {
            return content.Trim().Trim('"');
        }
    }

    private static string? ReadString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.GetRawText(),
            _ => null
        };
    }

    private static string BuildApiErrorMessage(
        string content,
        string fallback,
        int statusCode)
    {
        string? detail = null;
        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("errors", out var errors) &&
                errors.ValueKind == JsonValueKind.Array)
            {
                foreach (var error in errors.EnumerateArray())
                {
                    if (error.TryGetProperty("errorDescriptions", out var descriptions) &&
                        descriptions.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var description in descriptions.EnumerateArray())
                        {
                            if (description.ValueKind == JsonValueKind.String)
                            {
                                detail = description.GetString();
                                break;
                            }
                        }
                    }

                    detail ??= ReadString(error, "message");
                    if (!string.IsNullOrWhiteSpace(detail))
                    {
                        break;
                    }
                }
            }

            detail ??= root.ValueKind == JsonValueKind.Object
                ? ReadString(root, "message")
                : null;
        }
        catch (JsonException)
        {
            // PayPhone puede devolver una respuesta vacía o no JSON; se conserva el mensaje genérico.
        }

        detail = SanitizeErrorDetail(detail);
        return string.IsNullOrWhiteSpace(detail)
            ? $"{fallback} (HTTP {statusCode})."
            : $"{fallback} (HTTP {statusCode}): {detail}";
    }

    private static string? SanitizeErrorDetail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var withoutControls = new string(value
            .Select(character => char.IsControl(character) ? ' ' : character)
            .ToArray());
        var compact = string.Join(' ', withoutControls.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return compact.Length <= 300 ? compact : compact[..300];
    }

    private static string Truncate(string? value, int maximumLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "Pago AirportApp" : value.Trim();
        return normalized.Length <= maximumLength ? normalized : normalized[..maximumLength];
    }

    private sealed record PayPhoneApiResponse(
        bool IsSuccess,
        int StatusCode,
        string Content);
}
