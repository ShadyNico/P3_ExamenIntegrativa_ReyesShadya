using AirportApp.Models.Domain;
using Microsoft.EntityFrameworkCore;

namespace AirportApp.Data;

public sealed class DomainDbContext(DbContextOptions<DomainDbContext> options)
    : DbContext(options)
{
    public DbSet<Airport> Airports => Set<Airport>();
    public DbSet<AirportGeo> AirportGeographies => Set<AirportGeo>();
    public DbSet<AirportReachable> AirportReachability => Set<AirportReachable>();
    public DbSet<Airline> Airlines => Set<Airline>();
    public DbSet<AirplaneType> AirplaneTypes => Set<AirplaneType>();
    public DbSet<Airplane> Airplanes => Set<Airplane>();
    public DbSet<FlightSchedule> FlightSchedules => Set<FlightSchedule>();
    public DbSet<Flight> Flights => Set<Flight>();
    public DbSet<FlightLog> FlightLogs => Set<FlightLog>();
    public DbSet<Passenger> Passengers => Set<Passenger>();
    public DbSet<PassengerDetails> PassengerDetails => Set<PassengerDetails>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<WeatherData> WeatherData => Set<WeatherData>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("airportdb");

        modelBuilder.Entity<Airport>(entity =>
        {
            entity.ToTable("airport");
            entity.HasKey(x => x.AirportId).HasName("airport_pkey");
            entity.Property(x => x.AirportId).HasColumnName("airport_id").UseIdentityByDefaultColumn();
            entity.Property(x => x.Iata).HasColumnName("iata").HasMaxLength(3).IsFixedLength();
            entity.Property(x => x.Icao).HasColumnName("icao").HasMaxLength(4).IsFixedLength();
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(50);
            entity.HasIndex(x => x.Icao).IsUnique().HasDatabaseName("airport_icao_unq");
            entity.HasIndex(x => x.Iata).HasDatabaseName("airport_iata_idx");
            entity.HasIndex(x => x.Name).HasDatabaseName("airport_name_idx");
        });

        modelBuilder.Entity<AirportGeo>(entity =>
        {
            entity.ToTable("airport_geo");
            entity.HasKey(x => x.AirportId).HasName("airport_geo_pkey");
            entity.Property(x => x.AirportId).HasColumnName("airport_id");
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(50);
            entity.Property(x => x.City).HasColumnName("city").HasMaxLength(50);
            entity.Property(x => x.Country).HasColumnName("country").HasMaxLength(50);
            entity.Property(x => x.Latitude).HasColumnName("latitude").HasPrecision(11, 8);
            entity.Property(x => x.Longitude).HasColumnName("longitude").HasPrecision(11, 8);
            entity.Property(x => x.Geolocation).HasColumnName("geolocation");
            entity.Property(x => x.Location)
                .HasColumnName("location")
                .HasColumnType("point")
                .ValueGeneratedOnAddOrUpdate();
            entity.HasOne(x => x.Airport).WithOne(x => x.Geo)
                .HasForeignKey<AirportGeo>(x => x.AirportId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("airport_geo_airport_fk");
        });

        modelBuilder.Entity<AirportReachable>(entity =>
        {
            entity.ToTable("airport_reachable");
            entity.HasKey(x => x.AirportId).HasName("airport_reachable_pkey");
            entity.Property(x => x.AirportId).HasColumnName("airport_id");
            entity.Property(x => x.Hops).HasColumnName("hops");
            entity.HasOne(x => x.Airport).WithOne(x => x.Reachability)
                .HasForeignKey<AirportReachable>(x => x.AirportId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("airport_reachable_airport_fk");
        });

        modelBuilder.Entity<Airline>(entity =>
        {
            entity.ToTable("airline");
            entity.HasKey(x => x.AirlineId).HasName("airline_pkey");
            entity.Property(x => x.AirlineId).HasColumnName("airline_id").UseIdentityByDefaultColumn();
            entity.Property(x => x.Iata).HasColumnName("iata").HasMaxLength(2).IsFixedLength();
            entity.Property(x => x.AirlineName).HasColumnName("airlinename").HasMaxLength(30);
            entity.Property(x => x.BaseAirportId).HasColumnName("base_airport");
            entity.HasIndex(x => x.Iata).IsUnique().HasDatabaseName("airline_iata_unq");
            entity.HasOne(x => x.BaseAirport).WithMany(x => x.BasedAirlines)
                .HasForeignKey(x => x.BaseAirportId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("airline_base_airport_fk");
        });

        modelBuilder.Entity<AirplaneType>(entity =>
        {
            entity.ToTable("airplane_type");
            entity.HasKey(x => x.TypeId).HasName("airplane_type_pkey");
            entity.Property(x => x.TypeId).HasColumnName("type_id").UseIdentityByDefaultColumn();
            entity.Property(x => x.Identifier).HasColumnName("identifier").HasMaxLength(50);
            entity.Property(x => x.Description).HasColumnName("description");
        });

        modelBuilder.Entity<Airplane>(entity =>
        {
            entity.ToTable("airplane");
            entity.HasKey(x => x.AirplaneId).HasName("airplane_pkey");
            entity.Property(x => x.AirplaneId).HasColumnName("airplane_id").UseIdentityByDefaultColumn();
            entity.Property(x => x.Capacity).HasColumnName("capacity");
            entity.Property(x => x.TypeId).HasColumnName("type_id");
            entity.Property(x => x.AirlineId)
                .HasColumnName("airline_id")
                .HasColumnType("integer")
                .HasConversion<int>();
            entity.HasOne(x => x.Type).WithMany(x => x.Airplanes)
                .HasForeignKey(x => x.TypeId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("airplane_type_fk");
            entity.HasOne(x => x.Airline).WithMany(x => x.Airplanes)
                .HasForeignKey(x => x.AirlineId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("airplane_airline_fk");
        });

        modelBuilder.Entity<FlightSchedule>(entity =>
        {
            entity.ToTable("flightschedule");
            entity.HasKey(x => x.FlightNo).HasName("flightschedule_pkey");
            entity.Property(x => x.FlightNo).HasColumnName("flightno").HasMaxLength(8).IsFixedLength();
            entity.Property(x => x.FromAirportId).HasColumnName("from");
            entity.Property(x => x.ToAirportId).HasColumnName("to");
            entity.Property(x => x.Departure).HasColumnName("departure");
            entity.Property(x => x.Arrival).HasColumnName("arrival");
            entity.Property(x => x.AirlineId).HasColumnName("airline_id");
            entity.Property(x => x.Monday).HasColumnName("monday");
            entity.Property(x => x.Tuesday).HasColumnName("tuesday");
            entity.Property(x => x.Wednesday).HasColumnName("wednesday");
            entity.Property(x => x.Thursday).HasColumnName("thursday");
            entity.Property(x => x.Friday).HasColumnName("friday");
            entity.Property(x => x.Saturday).HasColumnName("saturday");
            entity.Property(x => x.Sunday).HasColumnName("sunday");
            entity.HasOne(x => x.FromAirport).WithMany(x => x.DepartingSchedules)
                .HasForeignKey(x => x.FromAirportId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("flightschedule_from_fk");
            entity.HasOne(x => x.ToAirport).WithMany(x => x.ArrivingSchedules)
                .HasForeignKey(x => x.ToAirportId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("flightschedule_to_fk");
            entity.HasOne(x => x.Airline).WithMany(x => x.FlightSchedules)
                .HasForeignKey(x => x.AirlineId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("flightschedule_airline_fk");
        });

        modelBuilder.Entity<Flight>(entity =>
        {
            entity.ToTable("flight");
            entity.HasKey(x => x.FlightId).HasName("flight_pkey");
            entity.Property(x => x.FlightId).HasColumnName("flight_id").UseIdentityByDefaultColumn();
            entity.Property(x => x.FlightNo).HasColumnName("flightno").HasMaxLength(8).IsFixedLength();
            entity.Property(x => x.FromAirportId).HasColumnName("from");
            entity.Property(x => x.ToAirportId).HasColumnName("to");
            entity.Property(x => x.Departure).HasColumnName("departure").HasColumnType("timestamp without time zone");
            entity.Property(x => x.Arrival).HasColumnName("arrival").HasColumnType("timestamp without time zone");
            entity.Property(x => x.AirlineId).HasColumnName("airline_id");
            entity.Property(x => x.AirplaneId).HasColumnName("airplane_id");
            entity.Ignore(x => x.Duration);
            entity.HasOne(x => x.Schedule).WithMany(x => x.Flights)
                .HasForeignKey(x => x.FlightNo).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("flight_schedule_fk");
            entity.HasOne(x => x.FromAirport).WithMany(x => x.DepartingFlights)
                .HasForeignKey(x => x.FromAirportId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("flight_from_fk");
            entity.HasOne(x => x.ToAirport).WithMany(x => x.ArrivingFlights)
                .HasForeignKey(x => x.ToAirportId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("flight_to_fk");
            entity.HasOne(x => x.Airline).WithMany(x => x.Flights)
                .HasForeignKey(x => x.AirlineId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("flight_airline_fk");
            entity.HasOne(x => x.Airplane).WithMany(x => x.Flights)
                .HasForeignKey(x => x.AirplaneId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("flight_airplane_fk");
        });

        modelBuilder.Entity<Passenger>(entity =>
        {
            entity.ToTable("passenger");
            entity.HasKey(x => x.PassengerId).HasName("passenger_pkey");
            entity.Property(x => x.PassengerId).HasColumnName("passenger_id").UseIdentityByDefaultColumn();
            entity.Property(x => x.PassportNo).HasColumnName("passportno").HasMaxLength(9).IsFixedLength();
            entity.Property(x => x.FirstName).HasColumnName("firstname").HasMaxLength(100);
            entity.Property(x => x.LastName).HasColumnName("lastname").HasMaxLength(100);
            entity.HasIndex(x => x.PassportNo).IsUnique().HasDatabaseName("passenger_passportno_unq");
        });

        modelBuilder.Entity<PassengerDetails>(entity =>
        {
            entity.ToTable("passengerdetails");
            entity.HasKey(x => x.PassengerId).HasName("passengerdetails_pkey");
            entity.Property(x => x.PassengerId).HasColumnName("passenger_id");
            entity.Property(x => x.BirthDate).HasColumnName("birthdate");
            entity.Property(x => x.Sex).HasColumnName("sex").HasMaxLength(1).IsFixedLength();
            entity.Property(x => x.Street).HasColumnName("street").HasMaxLength(100);
            entity.Property(x => x.City).HasColumnName("city").HasMaxLength(100);
            entity.Property(x => x.Zip).HasColumnName("zip");
            entity.Property(x => x.Country).HasColumnName("country").HasMaxLength(100);
            entity.Property(x => x.EmailAddress).HasColumnName("emailaddress").HasMaxLength(120);
            entity.Property(x => x.TelephoneNo).HasColumnName("telephoneno").HasMaxLength(30);
            entity.HasOne(x => x.Passenger).WithOne(x => x.Details)
                .HasForeignKey<PassengerDetails>(x => x.PassengerId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("passengerdetails_passenger_fk");
        });

        modelBuilder.Entity<Booking>(entity =>
        {
            entity.ToTable("booking");
            entity.HasKey(x => x.BookingId).HasName("booking_pkey");
            entity.Property(x => x.BookingId).HasColumnName("booking_id").UseIdentityByDefaultColumn();
            entity.Property(x => x.FlightId).HasColumnName("flight_id");
            entity.Property(x => x.Seat).HasColumnName("seat").HasMaxLength(4).IsFixedLength();
            entity.Property(x => x.PassengerId).HasColumnName("passenger_id");
            entity.Property(x => x.Price).HasColumnName("price").HasPrecision(10, 2);
            entity.HasIndex(x => new { x.FlightId, x.Seat }).IsUnique().HasDatabaseName("booking_seatplan_unq");
            entity.HasIndex(x => x.PassengerId).HasDatabaseName("booking_passenger_idx");
            entity.HasOne(x => x.Flight).WithMany(x => x.Bookings)
                .HasForeignKey(x => x.FlightId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("booking_flight_fk");
            entity.HasOne(x => x.Passenger).WithMany(x => x.Bookings)
                .HasForeignKey(x => x.PassengerId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("booking_passenger_fk");
        });

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.ToTable("employee");
            entity.HasKey(x => x.EmployeeId).HasName("employee_pkey");
            entity.Property(x => x.EmployeeId).HasColumnName("employee_id").UseIdentityByDefaultColumn();
            entity.Property(x => x.FirstName).HasColumnName("firstname").HasMaxLength(100);
            entity.Property(x => x.LastName).HasColumnName("lastname").HasMaxLength(100);
            entity.Property(x => x.BirthDate).HasColumnName("birthdate");
            entity.Property(x => x.Sex).HasColumnName("sex").HasMaxLength(1).IsFixedLength();
            entity.Property(x => x.Street).HasColumnName("street").HasMaxLength(100);
            entity.Property(x => x.City).HasColumnName("city").HasMaxLength(100);
            entity.Property(x => x.Zip).HasColumnName("zip");
            entity.Property(x => x.Country).HasColumnName("country").HasMaxLength(100);
            entity.Property(x => x.EmailAddress).HasColumnName("emailaddress").HasMaxLength(120);
            entity.Property(x => x.TelephoneNo).HasColumnName("telephoneno").HasMaxLength(30);
            entity.Property(x => x.Salary).HasColumnName("salary").HasPrecision(8, 2);
            entity.Property(x => x.Department).HasColumnName("department").HasMaxLength(20);
            entity.Property(x => x.UserName).HasColumnName("username").HasMaxLength(20);
            entity.Property(x => x.LegacyPasswordHash).HasColumnName("password").HasMaxLength(32).IsFixedLength();
        });

        modelBuilder.Entity<FlightLog>(entity =>
        {
            entity.ToTable("flight_log");
            entity.HasKey(x => x.FlightLogId).HasName("flight_log_pkey");
            entity.Property(x => x.FlightLogId).HasColumnName("flight_log_id").UseIdentityByDefaultColumn();
            entity.Property(x => x.LogDate).HasColumnName("log_date").HasColumnType("timestamp without time zone");
            entity.Property(x => x.UserName).HasColumnName("user").HasMaxLength(100);
            entity.Property(x => x.FlightId).HasColumnName("flight_id");
            entity.Property(x => x.FlightNoOld).HasColumnName("flightno_old").HasMaxLength(8).IsFixedLength();
            entity.Property(x => x.FlightNoNew).HasColumnName("flightno_new").HasMaxLength(8).IsFixedLength();
            entity.Property(x => x.FromOld).HasColumnName("from_old");
            entity.Property(x => x.ToOld).HasColumnName("to_old");
            entity.Property(x => x.FromNew).HasColumnName("from_new");
            entity.Property(x => x.ToNew).HasColumnName("to_new");
            entity.Property(x => x.DepartureOld).HasColumnName("departure_old").HasColumnType("timestamp without time zone");
            entity.Property(x => x.ArrivalOld).HasColumnName("arrival_old").HasColumnType("timestamp without time zone");
            entity.Property(x => x.DepartureNew).HasColumnName("departure_new").HasColumnType("timestamp without time zone");
            entity.Property(x => x.ArrivalNew).HasColumnName("arrival_new").HasColumnType("timestamp without time zone");
            entity.Property(x => x.AirplaneIdOld).HasColumnName("airplane_id_old");
            entity.Property(x => x.AirplaneIdNew).HasColumnName("airplane_id_new");
            entity.Property(x => x.AirlineIdOld).HasColumnName("airline_id_old");
            entity.Property(x => x.AirlineIdNew).HasColumnName("airline_id_new");
            entity.Property(x => x.Comment).HasColumnName("comment").HasMaxLength(200);
            entity.HasOne(x => x.Flight).WithMany(x => x.LogEntries)
                .HasForeignKey(x => x.FlightId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("flight_log_flight_fk");
        });

        modelBuilder.Entity<WeatherData>(entity =>
        {
            entity.ToTable("weatherdata");
            entity.HasKey(x => new { x.LogDate, x.Time, x.Station }).HasName("weatherdata_pkey");
            entity.Property(x => x.LogDate).HasColumnName("log_date");
            entity.Property(x => x.Time).HasColumnName("time");
            entity.Property(x => x.Station).HasColumnName("station");
            entity.Property(x => x.Temperature).HasColumnName("temp").HasPrecision(3, 1);
            entity.Property(x => x.Humidity).HasColumnName("humidity").HasPrecision(4, 1);
            entity.Property(x => x.AirPressure).HasColumnName("airpressure").HasPrecision(10, 2);
            entity.Property(x => x.Wind).HasColumnName("wind").HasPrecision(5, 2);
            entity.Property(x => x.Weather).HasColumnName("weather").HasMaxLength(30);
            entity.Property(x => x.WindDirection).HasColumnName("winddirection");
        });
    }
}
