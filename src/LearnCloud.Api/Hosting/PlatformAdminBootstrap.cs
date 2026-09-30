using LearnCloud.Auth.Entities;
using LearnCloud.MultiTenancy.Context;
using LearnCloud.MultiTenancy.Security;
using LearnCloud.PlatformAdmin.Entities;
using LearnCloud.PlatformAdmin.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LearnCloud.Api.Hosting;

/// <summary>
/// Creates the first platform administrator.
///
/// The platform console (trials, suspensions, sales enquiries, impersonation) requires a
/// PLATFORM_SUPERADMIN, and nothing could create one: registration only makes school users,
/// and RoleSecurityGuard refuses to put a platform role on a user who belongs to a school.
/// So the account is made here, deliberately, from the command line:
///
///   dotnet LearnCloud.Api.dll --create-platform-admin admin@learncloud.co.zw
///
/// with the password in PLATFORM_ADMIN_PASSWORD. The password is never taken from the
/// command line, where it would sit in shell history and in the process list, and it is
/// never logged. The command refuses to run once a platform administrator exists: a second
/// one is made from the console by the first.
/// </summary>
public static class PlatformAdminBootstrap
{
    public const string Argument = "--create-platform-admin";
    private const string PasswordVariable = "PLATFORM_ADMIN_PASSWORD";
    private const int MinimumPasswordLength = 12;

    public static bool IsRequested(string[] args) => args.Contains(Argument);

    /// <summary>Returns the process exit code: 0 created, 1 refused.</summary>
    public static async Task<int> RunAsync(WebApplication app, string[] args)
    {
        var logger = app.Logger;
        var email = args.SkipWhile(a => a != Argument).Skip(1).FirstOrDefault()?.Trim().ToLowerInvariant();
        var password = Environment.GetEnvironmentVariable(PasswordVariable);

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            logger.LogError("Usage: dotnet LearnCloud.Api.dll {Argument} <email>, with the password in {Variable}", Argument, PasswordVariable);
            return 1;
        }
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinimumPasswordLength)
        {
            logger.LogError("Set {Variable} to a password of at least {Length} characters before running {Argument}", PasswordVariable, MinimumPasswordLength, Argument);
            return 1;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LearnCloudDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        var noTenant = scope.ServiceProvider.GetRequiredService<INoTenantOperation>();

        // Platform rows belong to no school, so this runs in an audited no-tenant scope like
        // every other cross-tenant operation.
        using var _ = noTenant.BeginScope("Bootstrapping the first platform administrator", actorUserId: 0, actorRole: PrivilegedRoles.SystemJob);

        var existing = await db.Set<UserRole>()
            .Join(db.Set<Role>().Where(r => r.Code == PrivilegedRoles.PlatformSuperAdmin), ur => ur.RoleId, r => r.Id, (ur, r) => ur)
            .Where(ur => !ur.IsDeleted)
            .AnyAsync();
        if (existing)
        {
            logger.LogError("A platform administrator already exists. Create further ones from the platform console, or remove that account first");
            return 1;
        }

        if (await db.Set<User>().AnyAsync(u => u.Email == email && u.TenantId == null && !u.IsDeleted))
        {
            logger.LogError("A platform user with that email already exists");
            return 1;
        }

        var role = await db.Set<Role>().FirstOrDefaultAsync(r => r.TenantId == null && r.Code == PrivilegedRoles.PlatformSuperAdmin);
        if (role is null)
        {
            role = new Role
            {
                TenantId = null,
                Code = PrivilegedRoles.PlatformSuperAdmin,
                Name = "Platform Superadmin",
                IsSystem = true,
                Description = "Runs the platform: trials, suspensions, enquiries, support access",
            };
            db.Set<Role>().Add(role);
            await db.SaveChangesAsync();
        }

        var admin = new User
        {
            TenantId = null,
            Email = email,
            DisplayName = "Platform Administrator",
            Status = "active",
            // There is no school to send a verification email on behalf of, and whoever runs
            // this command already controls the deployment.
            EmailVerified = true,
            EmailVerifiedAt = DateTime.UtcNow,
            SecurityStamp = Guid.NewGuid().ToString(),
            TokenVersion = 1,
        };
        admin.PasswordHash = hasher.HashPassword(admin, password);
        db.Set<User>().Add(admin);
        await db.SaveChangesAsync();

        db.Set<UserRole>().Add(new UserRole { TenantId = null, UserId = admin.Id, RoleId = role.Id });

        // The console asks for an authenticator code as well as the password, so the secret is
        // created here and shown once. It is printed rather than logged, so it does not end up
        // in the platform's log store.
        var secret = Totp.NewSecret();
        db.Set<PlatformSecondFactor>().Add(new PlatformSecondFactor { UserId = admin.Id, Secret = secret });
        await db.SaveChangesAsync();

        logger.LogInformation("Platform administrator {Email} created (user {UserId})", email, admin.Id);
        Console.WriteLine();
        Console.WriteLine("  Platform administrator created: " + email);
        Console.WriteLine("  Scan this into your authenticator app now - it is not shown again:");
        Console.WriteLine();
        Console.WriteLine("    " + Totp.EnrolmentUri(secret, email));
        Console.WriteLine();
        Console.WriteLine("    secret: " + secret);
        Console.WriteLine();
        Console.WriteLine("  Sign in without a school code, then POST /api/platform/second-factor/verify");
        Console.WriteLine("  with the six-digit code to open the console for 30 minutes.");
        Console.WriteLine();
        return 0;
    }
}
