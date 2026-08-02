using Microsoft.AspNetCore.Identity;

namespace AirportApp.Data;

public static class IdentitySeeder
{
    public static readonly string[] Roles =
        ["Administrador", "Supervisor", "Operador", "Consulta"];

    public static async Task SeedAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var roleName in Roles)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                EnsureSucceeded(
                    await roleManager.CreateAsync(new IdentityRole(roleName)),
                    $"crear el rol {roleName}");
            }
        }

        var configuration = services.GetRequiredService<IConfiguration>();
        if (!configuration.GetValue("SeedUsers:Enabled", false))
        {
            return;
        }

        var environment = services.GetRequiredService<IHostEnvironment>();
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var definitions = new[]
        {
            new SeedDefinition(
                "Administrador",
                configuration["SeedUsers:AdminEmail"],
                configuration["SeedUsers:AdminPassword"]),
            new SeedDefinition(
                "Supervisor",
                configuration["SeedUsers:SupervisorEmail"],
                configuration["SeedUsers:SupervisorPassword"]),
            new SeedDefinition(
                "Operador",
                configuration["SeedUsers:OperatorEmail"],
                configuration["SeedUsers:OperatorPassword"]),
            new SeedDefinition(
                "Consulta",
                configuration["SeedUsers:QueryEmail"],
                configuration["SeedUsers:QueryPassword"])
        };

        foreach (var definition in definitions)
        {
            if (string.IsNullOrWhiteSpace(definition.Email) ||
                string.IsNullOrWhiteSpace(definition.Password))
            {
                throw new InvalidOperationException(
                    $"SeedUsers está habilitado, pero faltan correo o contraseña para {definition.Role}.");
            }

            var user = await userManager.FindByEmailAsync(definition.Email);
            if (user is null)
            {
                user = new IdentityUser
                {
                    UserName = definition.Email,
                    Email = definition.Email,
                    EmailConfirmed = environment.IsDevelopment()
                };
                EnsureSucceeded(
                    await userManager.CreateAsync(user, definition.Password),
                    $"crear el usuario de demostración {definition.Role}");
            }
            else if (!await userManager.HasPasswordAsync(user))
            {
                EnsureSucceeded(
                    await userManager.AddPasswordAsync(user, definition.Password),
                    $"establecer la contraseña inicial de {definition.Role}");
            }

            if (!await userManager.IsInRoleAsync(user, definition.Role))
            {
                EnsureSucceeded(
                    await userManager.AddToRoleAsync(user, definition.Role),
                    $"asignar el rol {definition.Role}");
            }
        }
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (result.Succeeded)
        {
            return;
        }

        throw new InvalidOperationException(
            $"No se pudo {operation}: " +
            string.Join("; ", result.Errors.Select(x => x.Description)));
    }

    private sealed record SeedDefinition(string Role, string? Email, string? Password);
}
