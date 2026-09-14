using LearnCloud.MultiTenancy.Entities;
using LearnCloud.MultiTenancy.Security;

namespace LearnCloud.MultiTenancy.Context;

// Per-scope tenant state. Registered as scoped: one instance per HTTP request, and
// background work creates its own DI scope per unit of work.
//
// This used to keep its state in a static AsyncLocal. Values set inside an awaited
// helper method do not flow back to the caller, and every scope in the same async
// flow shared one mutable state object, so a resolved tenant could silently vanish
// or leak between scopes. Plain instance state has neither problem.
public class TenantContext : ITenantContext
{
    private sealed class State
    {
        public long? TenantId;
        public long? SubdomainTenantId;
        public long? TokenTenantId;
        public Tenant? CurrentTenant;
        public TenantResolutionSource Source = TenantResolutionSource.None;
        public string? Reason;
        public long? ActorUserId;
        public string? ActorRole;
        public bool IsExplicitNoTenant;

        public State Clone() => (State)MemberwiseClone();
    }

    private State _state = new();
    private readonly Stack<State> _scopes = new();

    public long? TenantId => _state.TenantId;
    public long? SubdomainTenantId => _state.SubdomainTenantId;
    public long? TokenTenantId => _state.TokenTenantId;
    public Tenant? CurrentTenant => _state.CurrentTenant;
    public bool IsResolved => _state.TenantId.HasValue || _state.IsExplicitNoTenant;
    public bool IsPlatform => !_state.TenantId.HasValue && !_state.IsExplicitNoTenant && _state.ActorUserId.HasValue;
    public bool IsExplicitNoTenant => _state.IsExplicitNoTenant;
    public TenantResolutionSource ResolutionSource => _state.Source;
    public string? ResolutionReason => _state.Reason;
    public long? ActorUserId => _state.ActorUserId;
    public string? ActorRole => _state.ActorRole;

    public void SetResolvedTenant(long tenantId, Tenant tenant, TenantResolutionSource source, long? subdomainTenantId = null, long? tokenTenantId = null, long? actorUserId = null)
    {
        _state.TenantId = tenantId;
        _state.CurrentTenant = tenant;
        _state.Source = source;
        if (subdomainTenantId.HasValue) _state.SubdomainTenantId = subdomainTenantId;
        if (tokenTenantId.HasValue) _state.TokenTenantId = tokenTenantId;
        if (actorUserId.HasValue) _state.ActorUserId = actorUserId;
        _state.IsExplicitNoTenant = false;
    }

    public void SetSubdomainTenant(long? subdomainTenantId, Tenant? tenant)
    {
        _state.SubdomainTenantId = subdomainTenantId;
        if (!_state.TenantId.HasValue && subdomainTenantId.HasValue)
        {
            // Anonymous requests only: the subdomain selects login branding.
            _state.TenantId = subdomainTenantId;
            _state.CurrentTenant = tenant;
            _state.Source = TenantResolutionSource.Subdomain;
        }
    }

    public void SetActor(long actorUserId, string? actorRole)
    {
        _state.ActorUserId = actorUserId;
        _state.ActorRole = actorRole;
    }

    public void SetExplicitNoTenant(string reason, long actorUserId, string actorRole)
    {
        if (!PrivilegedRoles.CanUseNoTenantScope(actorRole))
            throw new UnauthorizedAccessException($"SECURITY: Explicit no-tenant scope requires one of {string.Join(",", PrivilegedRoles.All)}, but got {actorRole}. Actor {actorUserId} reason {reason} - possible privilege escalation attempt.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Length < 10)
            throw new InvalidOperationException("Explicit no-tenant reason must be at least 10 characters for audit");

        _state.IsExplicitNoTenant = true;
        _state.Reason = reason;
        _state.ActorUserId = actorUserId;
        _state.ActorRole = actorRole;
        _state.Source = TenantResolutionSource.ExplicitNoTenant;
        _state.TenantId = null;
        _state.CurrentTenant = null;
    }

    public void Clear()
    {
        _state = new State();
        _scopes.Clear();
    }

    public IDisposable BeginNoTenantScope(string reason, long actorUserId, string actorRole = PrivilegedRoles.PlatformSuperAdmin)
    {
        _scopes.Push(_state.Clone());
        try { SetExplicitNoTenant(reason, actorUserId, actorRole); }
        catch { _state = _scopes.Pop(); throw; }
        return new ScopeDisposer(this);
    }

    public IDisposable BeginTenantScope(long tenantId, TenantResolutionSource source = TenantResolutionSource.JwtToken)
    {
        _scopes.Push(_state.Clone());
        _state.TenantId = tenantId;
        _state.CurrentTenant = null;
        _state.Source = source;
        _state.IsExplicitNoTenant = false;
        return new ScopeDisposer(this);
    }

    private void EndScope() => _state = _scopes.Count > 0 ? _scopes.Pop() : new State();

    private sealed class ScopeDisposer : IDisposable
    {
        private TenantContext? _owner;
        public ScopeDisposer(TenantContext owner) => _owner = owner;
        public void Dispose()
        {
            _owner?.EndScope();
            _owner = null;
        }
    }
}
