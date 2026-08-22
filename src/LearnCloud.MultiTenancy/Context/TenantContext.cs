using System.Threading;
using LearnCloud.MultiTenancy.Entities;

namespace LearnCloud.MultiTenancy.Context;

// Implementation using AsyncLocal for background jobs + HttpContext Items for request
public class TenantContext : ITenantContext
{
    private static readonly AsyncLocal<TenantContextState> _asyncLocal = new();

    private class TenantContextState
    {
        public long? TenantId;
        public long? SubdomainTenantId;
        public long? TokenTenantId;
        public Tenant? CurrentTenant;
        public TenantResolutionSource Source = TenantResolutionSource.None;
        public string? Reason;
        public long? ActorUserId;
        public string? ActorRole;
        public bool IsExplicitNoTenant = false;
        public Stack<TenantContextState> Stack = new();
    }

    private TenantContextState State
    {
        get => _asyncLocal.Value ??= new TenantContextState();
        set => _asyncLocal.Value = value;
    }

    public long? TenantId => State.TenantId;
    public long? SubdomainTenantId => State.SubdomainTenantId;
    public long? TokenTenantId => State.TokenTenantId;
    public Tenant? CurrentTenant => State.CurrentTenant;
    public bool IsResolved => State.TenantId.HasValue || State.IsExplicitNoTenant;
    public bool IsPlatform => !State.TenantId.HasValue && !State.IsExplicitNoTenant && State.ActorUserId.HasValue; // platform admin without tenant
    public bool IsExplicitNoTenant => State.IsExplicitNoTenant;
    public TenantResolutionSource ResolutionSource => State.Source;
    public string? ResolutionReason => State.Reason;
    public long? ActorUserId => State.ActorUserId;
    public string? ActorRole => State.ActorRole;

    public void SetResolvedTenant(long tenantId, Tenant tenant, TenantResolutionSource source, long? subdomainTenantId = null, long? tokenTenantId = null, long? actorUserId = null)
    {
        State.TenantId = tenantId;
        State.CurrentTenant = tenant;
        State.Source = source;
        if (subdomainTenantId.HasValue) State.SubdomainTenantId = subdomainTenantId;
        if (tokenTenantId.HasValue) State.TokenTenantId = tokenTenantId;
        if (actorUserId.HasValue) State.ActorUserId = actorUserId;
        State.IsExplicitNoTenant = false;
    }

    public void SetSubdomainTenant(long? subdomainTenantId, Tenant? tenant)
    {
        State.SubdomainTenantId = subdomainTenantId;
        if (!State.TenantId.HasValue) // only set as current if no token yet (anonymous)
        {
            if (subdomainTenantId.HasValue)
            {
                State.TenantId = subdomainTenantId;
                State.CurrentTenant = tenant;
                State.Source = TenantResolutionSource.Subdomain;
            }
        }
    }

    public void SetExplicitNoTenant(string reason, long actorUserId, string actorRole)
    {
        // SECURITY C5 FIX: explicit no-tenant requires privileged role PLATFORM_SUPERADMIN or PLATFORM_SUPPORT
        // Without this guard, any code could set IsExplicitNoTenant and bypass tenant filter returning all tenants data
        var allowedRoles = new[] { "PLATFORM_SUPERADMIN", "PLATFORM_SUPPORT", "SYSTEM" };
        if (!allowedRoles.Contains(actorRole))
        {
            throw new UnauthorizedAccessException($"SECURITY: Explicit no-tenant scope requires privileged role {string.Join(",", allowedRoles)}, but got {actorRole}. Actor {actorUserId} reason {reason} - possible privilege escalation attempt.");
        }
        if (string.IsNullOrWhiteSpace(reason) || reason.Length < 10)
            throw new InvalidOperationException("Explicit no-tenant reason must be >=10 chars for audit");
        
        // Audit - must be logged
        State.IsExplicitNoTenant = true;
        State.Reason = reason;
        State.ActorUserId = actorUserId;
        State.ActorRole = actorRole;
        State.Source = TenantResolutionSource.ExplicitNoTenant;
        State.TenantId = null; // no tenant
    }

    public void Clear()
    {
        _asyncLocal.Value = new TenantContextState();
    }

    public IDisposable BeginNoTenantScope(string reason, long actorUserId, string actorRole = "PLATFORM_SUPERADMIN")
    {
        var previous = CloneState();
        SetExplicitNoTenant(reason, actorUserId, actorRole);
        // Push stack for nesting
        State.Stack.Push(previous);
        return new ScopeDisposer(() =>
        {
            if (State.Stack.Count > 0)
                _asyncLocal.Value = State.Stack.Pop();
            else
                Clear();
        });
    }

    public IDisposable BeginTenantScope(long tenantId, TenantResolutionSource source = TenantResolutionSource.JwtToken)
    {
        var previous = CloneState();
        State.TenantId = tenantId;
        State.Source = source;
        State.IsExplicitNoTenant = false;
        State.Stack.Push(previous);
        return new ScopeDisposer(() =>
        {
            if (State.Stack.Count > 0)
                _asyncLocal.Value = State.Stack.Pop();
            else
                Clear();
        });
    }

    private TenantContextState CloneState()
    {
        return new TenantContextState
        {
            TenantId = State.TenantId,
            SubdomainTenantId = State.SubdomainTenantId,
            TokenTenantId = State.TokenTenantId,
            CurrentTenant = State.CurrentTenant,
            Source = State.Source,
            Reason = State.Reason,
            ActorUserId = State.ActorUserId,
            ActorRole = State.ActorRole,
            IsExplicitNoTenant = State.IsExplicitNoTenant,
            Stack = State.Stack
        };
    }

    private class ScopeDisposer : IDisposable
    {
        private readonly Action _dispose;
        private bool _disposed;
        public ScopeDisposer(Action dispose) => _dispose = dispose;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _dispose();
        }
    }
}
