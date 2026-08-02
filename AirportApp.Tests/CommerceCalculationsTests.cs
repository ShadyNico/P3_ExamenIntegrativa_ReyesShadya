using AirportApp.Services;
using System.Globalization;

namespace AirportApp.Tests;

public sealed class CommerceCalculationsTests
{
    [Theory]
    [InlineData(1, 1, true)]
    [InlineData(20, 20, true)]
    [InlineData(0, 10, false)]
    [InlineData(21, 30, false)]
    [InlineData(5, 4, false)]
    public void QuantityValidation_EnforcesStockAndLimit(
        int quantity,
        int stock,
        bool expected)
    {
        Assert.Equal(expected, CommerceCalculations.IsValidQuantity(quantity, stock));
    }

    [Fact]
    public void SubtotalAndTotal_UseExactDecimalArithmetic()
    {
        Assert.Equal(37.50m, CommerceCalculations.Subtotal(3, 12.50m));

        var total = CommerceCalculations.Total(
            [(2, 10.25m), (1, 3.10m), (4, 0.50m)]);

        Assert.Equal(25.60m, total);
    }

    [Theory]
    [InlineData("10.004", 1000)]
    [InlineData("10.005", 1001)]
    [InlineData("0.009", 1)]
    public void ToCents_RoundsAwayFromZero(string value, int expected)
    {
        Assert.Equal(
            expected,
            CommerceCalculations.ToCents(decimal.Parse(value, CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void InvalidAmounts_AreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CommerceCalculations.Subtotal(0, 1m));
        Assert.Throws<ArgumentOutOfRangeException>(() => CommerceCalculations.Subtotal(1, -1m));
        Assert.Throws<ArgumentOutOfRangeException>(() => CommerceCalculations.ToCents(-1m));
    }
}
