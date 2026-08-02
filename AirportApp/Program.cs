using System.Threading.RateLimiting;
using System.Security.Cryptography.X509Certificates;
using System.Globalization;
using AirportApp.Data;
using AirportApp.Services;
using AirportApp.Services.Ollama;
using AirportApp.Services.Payments;
using AirportApp.Settings;
using AirportApp.Repositories;
using AirportApp.Repositories.Interfaces;
using AirportApp.Services.Interfaces;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var applicationCulture = new CultureInfo("es-EC");
applicationCulture.NumberFormat.CurrencySymbol = "$";
CultureInfo.DefaultThreadCurrentCulture = applicationCulture;
CultureInfo.DefaultThreadCurrentUICulture = applicationCulture;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName("AirportApp")
    .PersistKeysToFileSystem(new DirectoryInfo(
        Path.Combine(builder.Environment.ContentRootPath, "DataProtectionKeys")));

var certificatePath = builder.Configuration["DataProtection:CertificatePath"];
if (!string.IsNullOrWhiteSpace(certificatePath))
{
    var certificatePassword = builder.Configuration["DataProtection:CertificatePassword"];
    var certificate = X509CertificateLoader.LoadPkcs12FromFile(
        certificatePath,
        certificatePassword,
        X509KeyStorageFlags.EphemeralKeySet);
    dataProtection.ProtectKeysWithCertificate(certificate);
}

var domainConnection = builder.Configuration.GetConnectionString("DomainConnection")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Configura ConnectionStrings:DomainConnection mediante secretos o variables de entorno.");
var applicationConnection = builder.Configuration.GetConnectionString("ApplicationConnection")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Configura ConnectionStrings:ApplicationConnection mediante secretos o variables de entorno.");

builder.Services.AddDbContext<DomainDbContext>(options =>
    options.UseNpgsql(domainConnection));
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(applicationConnection, npgsql =>
        npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "app")));

builder.Services
    .AddDefaultIdentity<IdentityUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequiredLength = 6;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "AirportApp.Identity";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.SlidingExpiration = true;
});

builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddTransient<IEmailSender, GmailEmailSender>();
builder.Services.AddScoped<IAirportServiceRepository, AirportServiceRepository>();
builder.Services.AddScoped<IPricingService, PricingService>();
builder.Services.AddScoped<IAvailabilityService, AvailabilityService>();
builder.Services.AddScoped<IReservationService, ReservationService>();
builder.Services.AddScoped<IPaymentService, SimulatedPaymentService>();
builder.Services.AddScoped<IServiceGatewayPaymentService, ServiceGatewayPaymentService>();
builder.Services.AddScoped<ServiceBookingQueryService>();

builder.Services.Configure<PayPhoneSettings>(
    builder.Configuration.GetSection("PayPhone"));
builder.Services.AddHttpClient<PayPhoneApiLinkService>();

builder.Services.Configure<PayPalSettings>(
    builder.Configuration.GetSection("PayPal"));
builder.Services.AddHttpClient<PayPalService>();

builder.Services
    .AddOptions<OllamaSettings>()
    .Bind(builder.Configuration.GetSection(OllamaSettings.SectionName))
    .Validate(
        settings =>
            Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps),
        "Ollama:BaseUrl debe ser una URL HTTP o HTTPS válida.")
    .Validate(settings => !string.IsNullOrWhiteSpace(settings.Model), "Ollama:Model es obligatorio.")
    .Validate(
        settings => settings.TimeoutSeconds is > 0 and <= 600,
        "Ollama:TimeoutSeconds debe estar entre 1 y 600.")
    .ValidateOnStart();

builder.Services.AddHttpClient<IOllamaService, OllamaService>((services, client) =>
{
    var settings = services.GetRequiredService<IOptions<OllamaSettings>>().Value;
    client.BaseAddress = new Uri($"{settings.BaseUrl.TrimEnd('/')}/");
    client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("ollama", limiter =>
    {
        limiter.PermitLimit = 20;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });
});

var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrWhiteSpace(googleClientId) &&
    !string.IsNullOrWhiteSpace(googleClientSecret))
{
    builder.Services.AddAuthentication().AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
    });
}

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", async (
    DomainDbContext domainDb,
    ApplicationDbContext applicationDb,
    CancellationToken cancellationToken) =>
{
    var domainReady = await DatabaseQuerySucceedsAsync(
        () => domainDb.Flights
            .AsNoTracking()
            .AnyAsync(cancellationToken));
    var applicationReady = await DatabaseQuerySucceedsAsync(
        () => applicationDb.Roles
            .AsNoTracking()
            .AnyAsync(cancellationToken));
    var payload = new
    {
        status = domainReady && applicationReady ? "Healthy" : "Unhealthy",
        application = "AirportApp",
        domainDatabase = domainReady,
        applicationDatabase = applicationReady,
        utc = DateTime.UtcNow
    };
    return domainReady && applicationReady
        ? Results.Ok(payload)
        : Results.Json(payload, statusCode: StatusCodes.Status503ServiceUnavailable);
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

using (var scope = app.Services.CreateScope())
{
    var applicationDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await applicationDb.Database.MigrateAsync();
    await IdentitySeeder.SeedAsync(scope.ServiceProvider);
    await AirportServicesInitializer.InitializeAsync(scope.ServiceProvider);
}

await app.RunAsync();

static async Task<bool> DatabaseQuerySucceedsAsync(Func<Task<bool>> query)
{
    try
    {
        return await query();
    }
    catch
    {
        return false;
    }
}

public partial class Program;
