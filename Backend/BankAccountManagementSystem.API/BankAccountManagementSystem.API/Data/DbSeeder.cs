using BankAccountManagementSystem.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BankAccountManagementSystem.API.Data
{
    /// <summary>
    /// Seeds the database with initial data on application startup.
    ///
    /// This runs once when the app starts. It is idempotent — meaning it is
    /// safe to call multiple times; it only creates data that doesn't exist yet.
    ///
    /// What it seeds:
    ///   1. The "Admin" role
    ///   2. The "User" role
    ///   3. An initial Admin user (credentials from appsettings.json → "SeedAdmin")
    /// </summary>
    public static class DbSeeder
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            var logger = serviceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

            try
            {
                var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var configuration = serviceProvider.GetRequiredService<IConfiguration>();

                // ─── Step 1: Seed roles ───────────────────────────────────────────
                await SeedRolesAsync(roleManager, logger);

                // ─── Step 2: Seed Admin user ──────────────────────────────────────
                await SeedAdminUserAsync(userManager, configuration, logger);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while seeding the database.");
                throw;
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // PRIVATE: Create roles if they don't already exist
        // ─────────────────────────────────────────────────────────────────────────
        private static async Task SeedRolesAsync(
            RoleManager<IdentityRole> roleManager,
            ILogger logger)
        {
            string[] roles = ["Admin", "User"];

            foreach (var roleName in roles)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    var result = await roleManager.CreateAsync(new IdentityRole(roleName));
                    if (result.Succeeded)
                        logger.LogInformation("Seeded role: {RoleName}", roleName);
                    else
                        logger.LogWarning("Failed to seed role {RoleName}: {Errors}",
                            roleName, string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // PRIVATE: Create the initial Admin user if they don't already exist
        // ─────────────────────────────────────────────────────────────────────────
        private static async Task SeedAdminUserAsync(
            UserManager<ApplicationUser> userManager,
            IConfiguration configuration,
            ILogger logger)
        {
            // Read admin seed settings from appsettings.json — never hard-code.
            var adminEmail = configuration["SeedAdmin:Email"]!;
            var adminPassword = configuration["SeedAdmin:Password"]!;
            var adminEmployeeCode = configuration["SeedAdmin:EmployeeCode"]!;
            var adminFirstName = configuration["SeedAdmin:FirstName"]!;
            var adminLastName = configuration["SeedAdmin:LastName"]!;

            // Check if admin already exists
            var existingAdmin = await userManager.FindByEmailAsync(adminEmail);
            if (existingAdmin != null)
            {
                logger.LogInformation("Admin user already exists. Skipping seed.");
                return;
            }

            // Create the admin user object
            var adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,       // Skip email confirmation for seeded admin
                EmployeeCode = adminEmployeeCode,
                FirstName = adminFirstName,
                LastName = adminLastName,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            // CreateAsync will hash the password automatically using Identity's
            // PasswordHasher — we never see or store the plain-text password.
            var result = await userManager.CreateAsync(adminUser, adminPassword);

            if (result.Succeeded)
            {
                // Assign the Admin role
                await userManager.AddToRoleAsync(adminUser, "Admin");
                logger.LogInformation("Admin user seeded successfully: {Email}", adminEmail);
                // NOTE: We intentionally do NOT log the password.
            }
            else
            {
                logger.LogError("Failed to seed admin user: {Errors}",
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }
}
