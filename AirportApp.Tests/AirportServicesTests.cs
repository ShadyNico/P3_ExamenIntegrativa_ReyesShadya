using AirportApp.Models.AirportServices;
using AirportApp.Models.ViewModels;
using AirportApp.Services;
using AirportApp.Services.Payments;
using AirportApp.Settings;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text;
using Microsoft.Extensions.Options;

namespace AirportApp.Tests;

public sealed class AirportServicesTests
{
    private readonly PricingService pricing = new();

    [Fact]
    public void Pricing_CalculatesSubtotal()
    {
        var result = pricing.Calculate(20m, 3);

        Assert.Equal(60m, result.Subtotal);
    }

    [Fact]
    public void Pricing_CalculatesFifteenPercentTax()
    {
        var result = pricing.Calculate(20m, 3);

        Assert.Equal(9m, result.Tax);
    }

    [Fact]
    public void Pricing_CalculatesTotal()
    {
        var result = pricing.Calculate(20m, 3);

        Assert.Equal(69m, result.Total);
    }

    [Theory]
    [InlineData(10, 4, 6, true)]
    [InlineData(10, 4, 7, false)]
    [InlineData(10, 10, 1, false)]
    [InlineData(10, 0, 0, false)]
    public void Availability_ValidatesRemainingCapacity(
        int maximum,
        int reserved,
        int quantity,
        bool expected)
    {
        Assert.Equal(expected, AvailabilityService.HasCapacity(maximum, reserved, quantity));
    }

    [Fact]
    public void Reservation_IsRejectedWhenCapacityIsInsufficient()
    {
        var service = new AirportService { AirportServiceId = 7, IsActive = true };
        var slot = new ServiceAvailability
        {
            AirportServiceId = service.AirportServiceId,
            AirportService = service,
            AvailableDate = DateOnly.FromDateTime(DateTime.Today),
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(10, 0),
            MaximumCapacity = 5,
            ReservedCapacity = 4,
            IsAvailable = true
        };

        Assert.Throws<InvalidOperationException>(() =>
            ReservationRules.EnsureCanReserve(
                slot,
                service.AirportServiceId,
                2,
                DateOnly.FromDateTime(DateTime.Today)));
    }

    [Fact]
    public void ApprovedPayment_ConfirmsReservationAndConsumesCapacity()
    {
        var (order, payment) = BuildPendingOrder();

        PaymentRules.ApplyApprovedPayment(order, payment);

        Assert.Equal(SimulatedPaymentStatus.Approved, payment.PaymentStatus);
        Assert.Equal(ServiceOrderStatus.Paid, order.OrderStatus);
        Assert.Equal(ServiceReservationStatus.Confirmed, order.Reservation.ReservationStatus);
        Assert.Equal(3, order.Reservation.ServiceAvailability.ReservedCapacity);
    }

    [Fact]
    public void PaidOrder_BlocksASecondPayment()
    {
        var (order, payment) = BuildPendingOrder();
        PaymentRules.ApplyApprovedPayment(order, payment);
        order.Payments.Add(payment);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            PaymentRules.EnsureCanProcess(order));

        Assert.Contains("ya fue pagada", exception.Message);
    }

    [Theory]
    [InlineData("4111111111111111", true)]
    [InlineData("4000000000000002", true)]
    [InlineData("1234567890123456", false)]
    public void CardValidation_UsesLuhnAndNeverNeedsCvvStorage(string card, bool expected)
    {
        Assert.Equal(expected, SimulatedPaymentService.IsValidCardNumber(card));
    }

    [Theory]
    [InlineData(SimulatedPaymentMethod.PayPalSandbox)]
    [InlineData(SimulatedPaymentMethod.PayPhone)]
    public void ExternalGateway_DoesNotRequestSimulatedCardFields(
        SimulatedPaymentMethod paymentMethod)
    {
        var model = new PaymentViewModel
        {
            OrderId = 1,
            PaymentMethod = paymentMethod
        };
        var results = new List<ValidationResult>();

        var valid = Validator.TryValidateObject(
            model,
            new ValidationContext(model),
            results,
            validateAllProperties: true);

        Assert.True(valid);
        Assert.Empty(results);
    }

    [Fact]
    public void PayPhone_UsesSubtotalAsTaxableBaseAndPreservesFifteenPercentTax()
    {
        var result = PayPhoneApiLinkService.CalculateTaxedAmountBreakdown(60m, 9m, 69m);

        Assert.Equal(6900, result.Amount);
        Assert.Equal(0, result.AmountWithoutTax);
        Assert.Equal(6000, result.AmountWithTax);
        Assert.Equal(900, result.Tax);
    }

    [Fact]
    public async Task PayPhone_CreateLink_UsesApiCredentialsLinkEndpointAndTaxBreakdown()
    {
        var handler = new RecordingHandler(
            HttpStatusCode.OK,
            "\"https://payp.page.link/airport-test\"");
        var service = CreatePayPhoneService(handler);

        var result = await service.CreatePaymentLinkAsync(
            60m,
            9m,
            69m,
            "SP1234567890123",
            "Servicio de prueba");

        Assert.Equal("https://payp.page.link/airport-test", result);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("https://pay.payphonetodoesposible.com/api/Links", handler.RequestUri?.ToString());
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("test-api-token", handler.AuthorizationParameter);
        Assert.Contains("\"amount\":6900", handler.Body);
        Assert.Contains("\"amountWithoutTax\":0", handler.Body);
        Assert.Contains("\"amountWithTax\":6000", handler.Body);
        Assert.Contains("\"tax\":900", handler.Body);
        Assert.Contains("\"storeId\":\"test-store\"", handler.Body);
        Assert.DoesNotContain("phoneNumber", handler.Body);
    }

    [Fact]
    public async Task PayPhone_CreateLink_RetriesWithoutOptionalStoreIdWhenProviderRejectsIt()
    {
        var handler = new SequenceRecordingHandler(
            (HttpStatusCode.NotFound, "Link Inválido"),
            (HttpStatusCode.OK, "\"https://payp.page.link/default-store\""));
        var service = CreatePayPhoneService(handler);

        var result = await service.CreatePaymentLinkAsync(
            60m,
            9m,
            69m,
            "SP1234567890123",
            "Servicio de prueba");

        Assert.Equal("https://payp.page.link/default-store", result);
        Assert.Equal(2, handler.Bodies.Count);
        Assert.Contains("\"storeId\":\"test-store\"", handler.Bodies[0]);
        Assert.DoesNotContain("storeId", handler.Bodies[1]);
    }

    [Fact]
    public async Task PayPhone_CreateLink_ShowsSafeProviderValidationDescription()
    {
        var handler = new RecordingHandler(
            HttpStatusCode.BadRequest,
            "{\"message\":\"Validaciones fallidas\",\"errorCode\":800,\"errors\":[{" +
            "\"message\":\"Amount\",\"errorDescriptions\":[\"El monto no coincide con el desglose\"]}]}");
        var service = CreatePayPhoneService(handler);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreatePaymentLinkAsync(
                60m,
                9m,
                69m,
                "SP1234567890123",
                "Servicio de prueba"));

        Assert.Contains("HTTP 400", exception.Message);
        Assert.Contains("El monto no coincide con el desglose", exception.Message);
        Assert.DoesNotContain("errorCode", exception.Message);
    }

    private static PayPhoneApiLinkService CreatePayPhoneService(HttpMessageHandler handler) =>
        new(
            new HttpClient(handler),
            Options.Create(new PayPhoneSettings
            {
                Token = "test-api-token",
                StoreId = "test-store"
            }));

    private static (ServiceOrder Order, ServicePayment Payment) BuildPendingOrder()
    {
        var availability = new ServiceAvailability
        {
            MaximumCapacity = 10,
            ReservedCapacity = 1,
            IsAvailable = true
        };
        var reservation = new ServiceReservation
        {
            Quantity = 2,
            ReservationStatus = ServiceReservationStatus.Pending,
            ServiceAvailability = availability
        };
        var order = new ServiceOrder
        {
            OrderStatus = ServiceOrderStatus.Pending,
            Reservation = reservation
        };
        var payment = new ServicePayment
        {
            PaymentStatus = SimulatedPaymentStatus.Pending
        };
        return (order, payment);
    }

    private sealed class RecordingHandler(HttpStatusCode statusCode, string responseBody)
        : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public Uri? RequestUri { get; private set; }
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }
        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            Body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class SequenceRecordingHandler(
        params (HttpStatusCode StatusCode, string ResponseBody)[] responses)
        : HttpMessageHandler
    {
        private int index;
        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Bodies.Add(request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken));
            var response = responses[Math.Min(index++, responses.Length - 1)];
            return new HttpResponseMessage(response.StatusCode)
            {
                Content = new StringContent(response.ResponseBody, Encoding.UTF8, "application/json")
            };
        }
    }
}
