namespace AirportApp.Models.Domain;

public sealed class Employee
{
    public int EmployeeId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateOnly BirthDate { get; set; }
    public string? Sex { get; set; }
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public short Zip { get; set; }
    public string Country { get; set; } = string.Empty;
    public string? EmailAddress { get; set; }
    public string? TelephoneNo { get; set; }
    public decimal? Salary { get; set; }
    public string? Department { get; set; }
    public string? UserName { get; set; }
    public string? LegacyPasswordHash { get; set; }
}

public sealed class WeatherData
{
    public DateOnly LogDate { get; set; }
    public TimeOnly Time { get; set; }
    public int Station { get; set; }
    public decimal Temperature { get; set; }
    public decimal Humidity { get; set; }
    public decimal AirPressure { get; set; }
    public decimal Wind { get; set; }
    public string? Weather { get; set; }
    public short WindDirection { get; set; }
}
