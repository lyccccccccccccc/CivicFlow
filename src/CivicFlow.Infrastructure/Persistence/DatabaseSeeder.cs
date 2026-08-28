using CivicFlow.Domain.Entities;
using CivicFlow.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CivicFlow.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    public static async Task InitialiseAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
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

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await SeedUser(userManager, "admin@civicflow.local", "Alex", "Admin", CivicFlowRoles.SystemAdministrator);
        await SeedUser(userManager, "manager@civicflow.local", "Morgan", "Manager", CivicFlowRoles.TeamManager);
        await SeedUser(userManager, "officer@civicflow.local", "Casey", "Officer", CivicFlowRoles.CaseOfficer);
        await SeedUser(userManager, "resident@civicflow.local", "Riley", "Resident", CivicFlowRoles.Resident);
    }

    private static async Task SeedUser(UserManager<ApplicationUser> manager, string email, string first, string last, string role)
    {
        var user = await manager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                Id = Guid.NewGuid(), Email = email, UserName = email, EmailConfirmed = true,
                FirstName = first, LastName = last, CreatedAtUtc = DateTimeOffset.UtcNow
            };
            var result = await manager.CreateAsync(user, "REDACTED_HISTORICAL_DEVELOPMENT_SECRET");
            if (!result.Succeeded)
                throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
        }
        if (!await manager.IsInRoleAsync(user, role)) await manager.AddToRoleAsync(user, role);
    }
}
