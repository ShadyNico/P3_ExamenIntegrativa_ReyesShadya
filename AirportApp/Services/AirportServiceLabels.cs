using AirportApp.Models.AirportServices;

namespace AirportApp.Services;

public static class AirportServiceLabels
{
    public static string ServiceType(AirportServiceType value) => value switch
    {
        AirportServiceType.VipLounge => "Sala VIP",
        AirportServiceType.Parking => "Estacionamiento",
        AirportServiceType.InternalTransport => "Transporte interno",
        AirportServiceType.PriorityAssistance => "Asistencia prioritaria",
        AirportServiceType.Companion => "Acompañamiento",
        AirportServiceType.TerminalTransfer => "Traslado entre terminales",
        _ => value.ToString()
    };

    public static string PriceType(ServicePriceType value) => value switch
    {
        ServicePriceType.PerPerson => "Por persona",
        ServicePriceType.PerHour => "Por hora",
        ServicePriceType.PerDay => "Por día",
        ServicePriceType.PerVehicle => "Por vehículo",
        ServicePriceType.Fixed => "Precio fijo",
        _ => value.ToString()
    };

    public static string Airport(string? iata, string sourceName)
    {
        var code = iata?.Trim().ToUpperInvariant();
        return code switch
        {
            "UIO" => "Aeropuerto Internacional Mariscal Sucre",
            "GYE" => "Aeropuerto Internacional José Joaquín de Olmedo",
            "MEC" => "Aeropuerto Internacional Eloy Alfaro",
            "ESM" => "Aeropuerto Coronel Carlos Concha Torres",
            _ => sourceName.Trim()
        };
    }
}
