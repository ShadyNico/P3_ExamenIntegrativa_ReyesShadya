using AirportApp.Data;
using AirportApp.Models.Commerce;
using AirportApp.Models.AirportServices;
using AirportApp.Models.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AirportApp.Tests;

public sealed class DbContextMappingTests
{
    private const string Connection =
        "Host=localhost;Database=airportapp_model_test;Username=test;Password=test";

    [Fact]
    public void DomainModel_MapsAllFourteenSourceTables()
    {
        var options = new DbContextOptionsBuilder<DomainDbContext>()
            .UseNpgsql(Connection)
            .Options;
        using var context = new DomainDbContext(options);

        var tableNames = context.Model.GetEntityTypes()
            .Select(type => type.GetTableName())
            .Where(name => name is not null)
            .ToHashSet(StringComparer.Ordinal);

        string[] expected =
        [
            "airport", "airport_geo", "airport_reachable", "airline", "airplane_type",
            "airplane", "flightschedule", "flight", "flight_log", "passenger",
            "passengerdetails", "booking", "employee", "weatherdata"
        ];

        Assert.Equal(expected.Length, tableNames.Count);
        Assert.All(expected, table => Assert.Contains(table, tableNames));
    }

    [Fact]
    public void WeatherData_UsesTheSourceCompositeKey()
    {
        var options = new DbContextOptionsBuilder<DomainDbContext>()
            .UseNpgsql(Connection)
            .Options;
        using var context = new DomainDbContext(options);

        var entity = context.Model.FindEntityType(typeof(WeatherData))!;
        var keyNames = entity.FindPrimaryKey()!.Properties.Select(property => property.Name);

        Assert.Equal(
            [nameof(WeatherData.LogDate), nameof(WeatherData.Time), nameof(WeatherData.Station)],
            keyNames);
    }

    [Fact]
    public void ApplicationTables_AreIsolatedInAppSchema()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(Connection)
            .Options;
        using var context = new ApplicationDbContext(options);

        Assert.All(
            context.Model.GetEntityTypes().Where(entity => entity.ClrType != typeof(AirportReference)),
            entity => Assert.Equal("app", entity.GetSchema()));

        var airportReference = context.Model.FindEntityType(typeof(AirportReference))!;
        Assert.Equal("airportdb", airportReference.GetSchema());

        var cart = context.Model.FindEntityType(typeof(ShoppingCartItem))!;
        var uniqueIndex = cart.GetIndexes().Single(index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(ShoppingCartItem.UserId), nameof(ShoppingCartItem.FlightStockId)]));
        Assert.True(uniqueIndex.IsUnique);
    }

    [Fact]
    public void CommerceModels_UseOptimisticConcurrency()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(Connection)
            .Options;
        using var context = new ApplicationDbContext(options);

        var stockVersion = context.Model.FindEntityType(typeof(FlightStock))!
            .FindProperty(nameof(FlightStock.Version))!;
        var paymentVersion = context.Model.FindEntityType(typeof(PaymentTransaction))!
            .FindProperty(nameof(PaymentTransaction.Version))!;

        Assert.True(stockVersion.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, stockVersion.ValueGenerated);
        Assert.True(paymentVersion.IsConcurrencyToken);
    }
}
