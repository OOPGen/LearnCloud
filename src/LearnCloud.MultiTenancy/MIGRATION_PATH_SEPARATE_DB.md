# Migration Path - When One Tenant Needs Its Own Database
**Freeze after acceptance - This design decision is critical**

Current design: **Shared Database, Shared Schema, tenant_id discriminator**. Every ITenantEntity has TenantId, global query filter via reflection, automatic assignment on SaveChanges, guard throws on mismatch.

This is optimal for 150-2000 learner schools (cost, ops, metering). But if a large tenant (e.g., 2000 learners, Scale plan, or government requirement data residency per school) needs physical isolation, we need to migrate to **Database-per-Tenant** for that one tenant while keeping others shared.

## What Would Change - Exact Steps

###  Phase 1: Introduce Tenant Connection Resolver (No Data Move Yet)

**Current:**
- Single connection string `Default`
- `LearnCloudDbContext` injected with `ITenantContext` and uses `_tenantContext.TenantId` for filter

**Changed:**
- New interface `ITenantConnectionResolver`:
```csharp
public interface ITenantConnectionResolver
{
    string GetConnectionString(long tenantId);
    bool IsDedicatedDatabase(long tenantId);
    string GetSharedConnectionString();
}
```
- Implementation backed by `TenantSettings` or `Tenant` table column `ConnectionString` (encrypted) + `IsDedicated` bool
- `LearnCloudDbContext` factory: instead of DI directly, create via `IDbContextFactory<LearnCloudDbContext>` that resolves connection string per tenant

- DI change:
```csharp
services.AddScoped<LearnCloudDbContext>(sp =>
{
    var tenantContext = sp.GetRequiredService<ITenantContext>();
    var resolver = sp.GetRequiredService<ITenantConnectionResolver>();
    var conn = tenantContext.TenantId.HasValue ? resolver.GetConnectionString(tenantContext.TenantId.Value) : resolver.GetSharedConnectionString();
    var options = new DbContextOptionsBuilder<LearnCloudDbContext>().UseMySql(conn, ...).Options;
    return new LearnCloudDbContext(options, tenantContext, auditInterceptor);
});
```

- **What stays same:** Entities still have TenantId, global filter still works (even in dedicated DB, TenantId will be same value for all rows, filter still passes). This allows code to stay unchanged.

### Phase 2: Schema Preparation for Dedicated Tenant

- For dedicated tenant DB, you still keep TenantId column but all rows have same TenantId. Unique constraints like `(TenantId, StudentNumber)` still work.
- Alternatively, you could drop TenantId filter for dedicated DB to gain performance, but **recommended to keep it** for code uniformity and to allow moving back to shared.

- Create new empty MySQL database `learncloud_tenant_{slug}` (e.g., `learncloud_petra`)
- Run migrations: `dotnet ef database update --connection "dedicated conn"` - same migrations as shared DB

### Phase 3: Data Migration (Zero-Downtime Approach)

1. **Maintenance window or read-only mode** for that tenant: set Tenant.Status = "migrating", middleware returns 503 "Migrating, try in 5 min" for that slug

2. **Export tenant data** from shared DB:
```sql
-- For each ITenantEntity table
-- Use explicit no-tenant scope in code to bypass filter
mysqldump --where="TenantId=42 AND IsDeleted=0" learncloud students grades streams ... > tenant_42.sql
-- Or use C# tool that iterates tables via reflection: GetAll ITenantEntity types, for each query IgnoreQueryFilters().Where(e=>e.TenantId==42)
```

3. **Transform if needed**: Keep TenantId same, but reset auto-increment? Keep Ids same to preserve FKs

4. **Import into dedicated DB**:
```sql
mysql learncloud_tenant_petra < tenant_42.sql
-- For FKs, disable checks temporarily: SET FOREIGN_KEY_CHECKS=0; ... SET FOREIGN_KEY_CHECKS=1;
```

5. **Verify counts**: For each table, `SELECT COUNT(*) FROM shared.students WHERE TenantId=42` vs `SELECT COUNT(*) FROM dedicated.students` must match. Also checksum of critical tables (fee invoices totals).

6. **Update Tenant resolver**: Set `Tenants.ConnectionString = "Server=...;Database=learncloud_tenant_petra;..."`, `IsDedicated=true`, `DedicatedMigratedAt=now`

7. **Switch resolver**: Next request for that slug will get dedicated connection string. No code deploy needed, just DB config.

8. **Dual-write verification (optional)**: For 24h, write to both shared and dedicated (shadow) to ensure no data loss if rollback needed.

9. **Delete from shared after retention (30 days)**: Soft-delete or hard-delete shared data for that tenant after confirming dedicated works. Keep backup.

### Phase 4: What Changes in Code

**Must Change:**
- `ITenantConnectionResolver` implementation + `Tenant` entity new columns: `ConnectionStringEncrypted`, `IsDedicatedDatabase`, `DedicatedDatabaseRegion`
- `LearnCloudDbContext` factory from connection resolver, not single connection
- `TenantResolutionMiddleware` - after resolving tenant, also resolve connection string and store in HttpContext for DbContext factory
- Background jobs: `INoTenantOperation` now needs to loop over both shared DB (query Tenants where IsDedicated=false) and each dedicated DB individually. Billing meter job becomes: for each dedicated tenant, open dedicated context, count active students.

**Stays Same:**
- Entities: still ITenantEntity with TenantId - no need to remove TenantId column even in dedicated DB (keeps code uniform, allows moving tenant back to shared)
- Global query filter via reflection - still works, TenantId constant
- Automatic TenantId assignment on SaveChanges - still works
- Audit interceptor - works, writes to dedicated DB's AuditLogs table
- Authorization handler - checks tid claim same as before
- Permission matrix - unchanged
- Integration tests - need to add `DedicatedDatabaseIsolationTests` that spins up two DbContexts with different connections and proves no cross-talk (but shared DB tests still pass)

**Could Be Optimized (Optional) for Dedicated:**
- Drop TenantId from indexes leading? Keep for uniformity, but you could add covering index without TenantId for performance in dedicated DB
- Remove global filter for dedicated DB to improve query plan (since TenantId is constant) - but then you lose uniformity; recommended to keep filter but it will be optimized away by MySQL query planner (TenantId constant = single value)

**What Would Have to Change if We Chose Schema-per-Tenant or Database-per-Tenant from Day One:**
- Would need schema migration runner that runs N times (one per tenant) - complexity
- Connection pooling: 200 tenants * 1 connection pool = 200 pools, memory heavy
- Platform reporting (MRR, total learners) would need to query across all DBs - map-reduce
- Tenant provisioning 2 minutes would become 10 minutes (create DB, run migrations)
- Our current shared approach avoids all that, and migration path above allows selective dedicated for large tenants only (hybrid)

### Phase 5: Rollback Plan

- Keep shared data soft-deleted for 30 days, not hard deleted
- If dedicated fails, flip `IsDedicated=false` and clear ConnectionString, next request goes back to shared DB (data still there if soft-deleted, need to restore IsDeleted=false)
- Audit everything via explicit no-tenant scope

### Summary Table

| Aspect | Shared DB (Current) | After Dedicated Migration (Hybrid) |
|---|---|---|
| Connection resolver | Single conn | Per-tenant conn via ITenantConnectionResolver |
| TenantId column | Discriminator + filter | Still present, constant in dedicated DB |
| Global filter | `e.TenantId == CurrentTenantId && !IsDeleted` via reflection | Same, works for both shared and dedicated |
| SaveChanges guard | Throws if FK from other tenant | Same, still throws |
| Audit | Writes to shared AuditLogs | Writes to dedicated's AuditLogs for dedicated tenant, platform audit stays shared |
| MTTR | 8h RTO shared backup | Dedicated backup per tenant + shared |
| Cost | Low (1 DB) | Higher for dedicated tenant, but Scale plan pays for it |

**Conclusion:** The current design with `ITenantEntity`, reflection global filter, automatic TenantId assignment, guard, explicit no-tenant audited scope, and subdomain vs token 403 check makes migration to dedicated DB a **config change + data copy**, not a rewrite. The only new code is `ITenantConnectionResolver` and factory. Everything else (entities, permission handler, audit interceptor, tests) stays.

This is why we freeze multi-tenancy after acceptance.

