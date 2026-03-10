using MatchUp.Utilities.Constants;
using Microsoft.AspNetCore.Identity;

namespace MatchUp.Data
{
    public static class IdentitySeeder
    {
        public static async Task SeedRolesAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();

            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

            string[] roles =
            {
                AppRoles.Admin,
                AppRoles.Moderator,
                AppRoles.User
            };

            foreach (var role in roles)
            {
                var exists = await roleManager.RoleExistsAsync(role);
                if (exists)
                    continue;

                await roleManager.CreateAsync(new IdentityRole<Guid>
                {
                    Id = Guid.NewGuid(),
                    Name = role,
                    NormalizedName = role.ToUpperInvariant()
                });
            }
        }
    }
}
