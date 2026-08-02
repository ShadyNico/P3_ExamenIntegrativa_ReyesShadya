using System.Security.Cryptography;
using AirportApp.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

const string ConnectionVariable = "AIRPORTAPP_APPLICATION_CONNECTION";

var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        $"Define {ConnectionVariable} con la conexión de la base Identity.");
}

var services = new ServiceCollection();
services.AddLogging();
services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));
services
    .AddIdentityCore<IdentityUser>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequiredLength = 6;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();
var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

var definitions = new[]
{
    new AccountDefinition(
        "Administrador",
        "administrador@airportapp.local",
        CreateTemporaryPassword()),
    new AccountDefinition(
        "Supervisor",
        "supervisor@airportapp.local",
        CreateTemporaryPassword())
};

foreach (var definition in definitions)
{
    await EnsureRoleAsync(roleManager, definition.Role);
    await ProvisionAccountAsync(userManager, definition);

    Console.WriteLine(
        $"CREDENTIAL|{definition.Role}|{definition.Email}|{definition.Password}");
}

static async Task EnsureRoleAsync(RoleManager<IdentityRole> roleManager, string role)
{
    if (await roleManager.RoleExistsAsync(role))
    {
        return;
    }

    EnsureSucceeded(await roleManager.CreateAsync(new IdentityRole(role)),
        $"crear el rol {role}");
}

static async Task ProvisionAccountAsync(
    UserManager<IdentityUser> userManager,
    AccountDefinition definition)
{
    var user = await userManager.FindByEmailAsync(definition.Email);
    if (user is null)
    {
        user = new IdentityUser
        {
            UserName = definition.Email,
            Email = definition.Email,
            EmailConfirmed = true
        };

        EnsureSucceeded(
            await userManager.CreateAsync(user, definition.Password),
            $"crear {definition.Email}");
    }
    else
    {
        user.EmailConfirmed = true;
        EnsureSucceeded(
            await userManager.UpdateAsync(user),
            $"confirmar {definition.Email}");

        if (await userManager.HasPasswordAsync(user))
        {
            EnsureSucceeded(
                await userManager.RemovePasswordAsync(user),
                $"retirar la contraseña anterior de {definition.Email}");
        }

        EnsureSucceeded(
            await userManager.AddPasswordAsync(user, definition.Password),
            $"establecer la contraseña de {definition.Email}");
    }

    var currentRoles = await userManager.GetRolesAsync(user);
    var unrelatedRoles = currentRoles
        .Where(role => !string.Equals(
            role,
            definition.Role,
            StringComparison.OrdinalIgnoreCase))
        .ToArray();

    if (unrelatedRoles.Length > 0)
    {
        EnsureSucceeded(
            await userManager.RemoveFromRolesAsync(user, unrelatedRoles),
            $"retirar roles adicionales de {definition.Email}");
    }

    if (!await userManager.IsInRoleAsync(user, definition.Role))
    {
        EnsureSucceeded(
            await userManager.AddToRoleAsync(user, definition.Role),
            $"asignar {definition.Role} a {definition.Email}");
    }

    if (!await userManager.CheckPasswordAsync(user, definition.Password))
    {
        throw new InvalidOperationException(
            $"La verificación de contraseña falló para {definition.Email}.");
    }
}

static string CreateTemporaryPassword() =>
    $"A!9a{Convert.ToHexString(RandomNumberGenerator.GetBytes(10))}z";

static void EnsureSucceeded(IdentityResult result, string operation)
{
    if (result.Succeeded)
    {
        return;
    }

    throw new InvalidOperationException(
        $"No se pudo {operation}: " +
        string.Join("; ", result.Errors.Select(error => error.Description)));
}

internal sealed record AccountDefinition(
    string Role,
    string Email,
    string Password);
