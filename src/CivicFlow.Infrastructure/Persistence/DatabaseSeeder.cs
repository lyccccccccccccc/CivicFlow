using CivicFlow.Domain.Entities;
using CivicFlow.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace CivicFlow.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    public static async Task SeedDevelopmentAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var configuration = services.GetRequiredService<IConfiguration>();

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in CivicFlowRoles.All)
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));

        var now = DateTimeOffset.UtcNow;
        if (!await db.ServiceCategories.AnyAsync())
        {
            db.ServiceCategories.AddRange(
                new ServiceCategory("Roads & footpaths", "Potholes, signs and damaged paths", 8, 72, now),
                new ServiceCategory("Waste & recycling", "Missed bins, illegal dumping and litter", 4, 48, now),
                new ServiceCategory("Parks & trees", "Playgrounds, trees and public open spaces", 12, 120, now),
                new ServiceCategory("Public facilities", "Lighting, toilets and council facilities", 8, 72, now));
            await db.SaveChangesAsync();
        }

        if (!configuration.GetValue<bool>("DemoAccounts:Enabled") || await db.Users.AnyAsync()) return;

        var password = configuration["DemoAccounts:Password"];
        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("DemoAccounts__Password is required when development demo accounts are enabled.");

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        foreach (var account in DevelopmentDemoAccounts.All)
            await SeedUser(userManager, account.Email, account.FirstName, account.LastName, account.Role, password);
    }

    private static async Task SeedUser(UserManager<ApplicationUser> manager, string email, string first, string last, string role, string password)
    {
        var user = await manager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                Id = Guid.NewGuid(), Email = email, UserName = email, EmailConfirmed = true,
                FirstName = first, LastName = last, CreatedAtUtc = DateTimeOffset.UtcNow
            };
            var result = await manager.CreateAsync(user, password);
            if (!result.Succeeded)
                throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
        }
        if (!await manager.IsInRoleAsync(user, role)) await manager.AddToRoleAsync(user, role);
    }
}

public sealed record DevelopmentDemoAccount(string Email, string FirstName, string LastName, string Role);

public static class DevelopmentDemoAccounts
{
    public static readonly IReadOnlyList<DevelopmentDemoAccount> All =
    [
        new("admin@civicflow.local", "Alex", "Admin", CivicFlowRoles.SystemAdministrator),
        new("manager@civicflow.local", "Morgan", "Manager", CivicFlowRoles.TeamManager),
        new("officer@civicflow.local", "Casey", "Officer", CivicFlowRoles.CaseOfficer),
        new("resident@civicflow.local", "Riley", "Resident", CivicFlowRoles.Resident)
    ];
}
