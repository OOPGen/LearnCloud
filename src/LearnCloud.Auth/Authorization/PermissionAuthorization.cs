using System.Security.Claims;
using LearnCloud.Auth.Entities;
using LearnCloud.MultiTenancy.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LearnCloud.Auth.Authorization;

// Attribute to use on controllers/actions: [RequiresPermission("students.read")]
public class RequiresPermissionAttribute : TypeFilterAttribute
{
    public RequiresPermissionAttribute(string permission) : base(typeof(PermissionFilter))
    {
        Arguments = new object[] { permission };
    }
}

public class PermissionFilter : IAsyncAuthorizationFilter
{
    private readonly string _permission;
    private readonly IAuthorizationService _authService;

    public PermissionFilter(string permission, IAuthorizationService authService)
    {
        _permission = permission;
        _authService = authService;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (!user.Identity?.IsAuthenticated ?? true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // Check token version against DB via claim? Handler below does deeper check, but we also quick check perms claim
        var hasPerm = user.Claims.Any(c => c.Type == "perm" && c.Value == _permission);
        if (hasPerm)
            return; // fast path - JWT contains permission

        // If perms_ref = db, need to check via requirement handler (load from DB cache)
        var result = await _authService.AuthorizeAsync(user, null, new PermissionRequirement(_permission));
        if (!result.Succeeded)
        {
            context.Result = new ForbidResult();
        }
    }
}

public class PermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }
    public PermissionRequirement(string permission) => Permission = permission;
}

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PermissionAuthorizationHandler> _logger;
    // In real app, inject cache: IDistributedCache or IMemoryCache for perms per user

    public PermissionAuthorizationHandler(IServiceScopeFactory scopeFactory, ILogger<PermissionAuthorizationHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var user = context.User;
        if (!user.Identity?.IsAuthenticated ?? true) return;

        // JWT claims must include: user id, tenant id, role ids, permission list (or ref), token version
        var uidClaim = user.FindFirst("uid")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;
        var tidClaim = user.FindFirst("tid")?.Value;
        var tvClaim = user.FindFirst("tv")?.Value;
        var ssClaim = user.FindFirst("ss")?.Value;

        if (uidClaim == null || !long.TryParse(uidClaim, out var userId))
            return;

        // Quick check from JWT perms (already in filter, but double-check)
        if (user.Claims.Any(c => c.Type == "perm" && c.Value == requirement.Permission))
        {
            // Still need to validate token version not revoked
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LearnCloudDbContext>();
            var dbUser = await db.Set<User>().AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);
            if (dbUser == null) return;
            if (tvClaim != null && int.TryParse(tvClaim, out var tv) && tv != dbUser.TokenVersion) { _logger.LogWarning("Token version mismatch user {UserId} jwt tv {JwtTv} db tv {DbTv}", userId, tv, dbUser.TokenVersion); return; }
            if (ssClaim != null && ssClaim != dbUser.SecurityStamp) return;

            context.Succeed(requirement);
            return;
        }

        // If perms_ref = db or perm not in JWT (large perm set), load from DB
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LearnCloudDbContext>();
            var dbUser = await db.Set<User>().AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);
            if (dbUser == null) return;
            if (tvClaim != null && int.TryParse(tvClaim, out var tv2) && tv2 != dbUser.TokenVersion) return;
            if (ssClaim != null && ssClaim != dbUser.SecurityStamp) return;

            // Load permissions via roles
            var hasPermission = await db.Set<UserRole>().Where(ur => ur.UserId == userId && !ur.IsDeleted)
                .Join(db.Set<RolePermission>(), ur => ur.RoleId, rp => rp.RoleId, (ur, rp) => rp.PermissionId)
                .Join(db.Set<Permission>(), pid => pid, p => p.Id, (pid, p) => p.Code)
                .AnyAsync(p => p == requirement.Permission);

            if (hasPermission)
            {
                context.Succeed(requirement);
            }
            else
            {
                _logger.LogInformation("User {UserId} missing permission {Perm}", userId, requirement.Permission);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking permission {Perm} for user {UserId}", requirement.Permission, userId);
        }
    }
}

// Extension for finding tenant id from claims
public static class ClaimsPrincipalExtensions
{
    public static long? GetTenantId(this ClaimsPrincipal user)
    {
        var tid = user.FindFirst("tid")?.Value;
        if (long.TryParse(tid, out var id) && id != 0) return id;
        return null;
    }
    public static long GetUserId(this ClaimsPrincipal user)
    {
        var uid = user.FindFirst("uid")?.Value ?? user.FindFirst("sub")?.Value;
        return long.Parse(uid!);
    }
    public static int GetTokenVersion(this ClaimsPrincipal user)
    {
        var tv = user.FindFirst("tv")?.Value;
        return int.TryParse(tv, out var v) ? v : 0;
    }
}
