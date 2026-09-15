using System.Text;
using System.Threading.RateLimiting;
using LearnCloud.Auth.Authorization;
using LearnCloud.Auth.Entities;
using LearnCloud.Auth.Services;
using LearnCloud.MultiTenancy.Context;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using FluentValidation;

namespace LearnCloud.Auth.Extensions;

public static class AuthModuleExtensions
{
    public static IServiceCollection AddLearnCloudAuth(this IServiceCollection services, IConfiguration config)
    {
        // Options
        services.Configure<JwtOptions>(config.GetSection("Jwt"));
        services.Configure<PasswordPolicyOptions>(config.GetSection("PasswordPolicy"));
        services.Configure<AuthRateLimitOptions>(config.GetSection("RateLimit"));

        var jwtOptions = config.GetSection("Jwt").Get<JwtOptions>() ?? throw new InvalidOperationException("Jwt config missing");

        // SECURITY C1 FIX: Validate JWT secret strength - fail fast if weak/demo
        if (string.IsNullOrWhiteSpace(jwtOptions.Secret))
            throw new InvalidOperationException("SECURITY: Jwt__Secret must be set via env var JWT_SECRET - 32+ chars, 256-bit");
        if (jwtOptions.Secret.Length < 32)
            throw new InvalidOperationException($"SECURITY: Jwt__Secret must be at least 32 chars (256-bit), current {jwtOptions.Secret.Length}");
        if (jwtOptions.Secret.ToLower().Contains("demo") || jwtOptions.Secret.ToLower().Contains("secret") && jwtOptions.Secret.Length < 40)
            throw new InvalidOperationException("SECURITY: Jwt__Secret appears to be demo/weak - must be cryptographically random 32+ chars");
        if (jwtOptions.Secret == "demo-integration-key-32-chars-min" || jwtOptions.Secret == "demo-secret" || jwtOptions.Secret == "learncloud-secret")
            throw new InvalidOperationException("SECURITY: Jwt__Secret is demo key - must be replaced via env");

        var pwdOptions = config.GetSection("PasswordPolicy").Get<PasswordPolicyOptions>() ?? new PasswordPolicyOptions();

        // Auth data lives in LearnCloudDbContext, registered by AddLearnCloudMultiTenancy.

        // Identity Password Hasher - SECURITY H4 FIX: Argon2id memory-hard (OWASP recommended)
        // Uses Argon2PasswordHasher which tries Konscious Argon2id via reflection, falls back to PBKDF2 310k iterations
        // To enable true Argon2id, add PackageReference: <PackageReference Include="Konscious.Security.Cryptography.Argon2" Version="1.3.1" />
        services.AddScoped<IPasswordHasher<User>, Services.Argon2PasswordHasher>();

        // Services
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IEmailSender, FakeEmailSender>();

        // Validators
        services.AddValidatorsFromAssemblyContaining<Validators.RegisterTenantValidator>();

        // Auth - JWT
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = false; // set true in prod
            options.SaveToken = false; // never log tokens!
            // Keep claim names exactly as TokenService writes them. The default inbound
            // mapping renamed "tid" to a Microsoft tenant-id URI, so TenantResolutionMiddleware
            // never found the tenant and every authenticated request failed with "No tenant context".
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),
                ValidIssuer = jwtOptions.Issuer,
                ValidAudience = jwtOptions.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
                // TokenService writes role claims with ClaimTypes.Role; [Authorize(Roles=...)] reads this type.
                RoleClaimType = System.Security.Claims.ClaimTypes.Role,
                NameClaimType = "uid"
            };
            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = ctx =>
                {
                    // Never log tokens
                    ctx.HttpContext.RequestServices.GetService<ILoggerFactory>()?.CreateLogger("Jwt").LogWarning("JWT auth failed: {Message}", ctx.Exception.Message);
                    return Task.CompletedTask;
                },
                OnTokenValidated = async ctx =>
                {
                    // Additional check: token version and security stamp against DB already handled in Permission handler for deeper check
                    // But quick check for disabled user
                    var db = ctx.HttpContext.RequestServices.GetRequiredService<LearnCloudDbContext>();
                    var uidClaim = ctx.Principal?.FindFirst("uid")?.Value ?? ctx.Principal?.FindFirst("sub")?.Value;
                    if (long.TryParse(uidClaim, out var uid))
                    {
                        var user = await db.Set<User>().AsNoTracking().FirstOrDefaultAsync(u => u.Id == uid && !u.IsDeleted);
                        if (user == null || user.Status == "disabled" || user.IsLockedOut)
                        {
                            ctx.Fail("User disabled or locked");
                        }
                    }
                }
            };
        });

        // Authorization with permission handler
        services.AddAuthorization(options =>
        {
            // Default policy requires authenticated
            options.DefaultPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        });
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddHttpContextAccessor();

        // Rate Limiting - fixed window - SECURITY H3 FIX: Add missing policies for refresh, verify-email, etc.
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            options.AddPolicy("login", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5, Window = TimeSpan.FromSeconds(60), QueueLimit = 0, AutoReplenishment = true
                    }));
            options.AddPolicy("registration", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 3, Window = TimeSpan.FromHours(1), QueueLimit = 0
                    }));
            options.AddPolicy("password_reset", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 3, Window = TimeSpan.FromHours(1), QueueLimit = 0
                    }));
            // H3: Missing rate limits
            options.AddPolicy("refresh", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter((httpContext.User.FindFirst("uid")?.Value ?? httpContext.Connection.RemoteIpAddress?.ToString()) ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true
                    }));
            options.AddPolicy("verify_email", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10, Window = TimeSpan.FromHours(1), QueueLimit = 0
                    }));
            // RateLimiting:ApiGeneralPerMinute lets the integration tests, which run every
            // test as the same user, raise the limit; production keeps the default.
            var generalPerMinute = config.GetValue("RateLimiting:ApiGeneralPerMinute", 60);
            options.AddPolicy("api_general", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter((httpContext.User.FindFirst("uid")?.Value ?? httpContext.Connection.RemoteIpAddress?.ToString()) ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = generalPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true
                    }));
            options.AddPolicy("sensitive", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter((httpContext.User.FindFirst("uid")?.Value ?? httpContext.Connection.RemoteIpAddress?.ToString()) ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true
                    }));
        });

        services.AddControllers();
        services.AddEndpointsApiExplorer();

        return services;
    }

    public static IApplicationBuilder UseLearnCloudAuth(this IApplicationBuilder app)
    {
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }
}
