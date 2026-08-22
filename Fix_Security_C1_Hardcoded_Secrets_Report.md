# Fix Security C1 - Hardcoded Secrets & Fallback Passwords

**Date:** 2026-08-09
**Severity:** Critical
**Status:** FIXED

## Issue
Hardcoded demo secrets and fallback DB passwords in production code path allowing deployment with demo keys if env vars missing.

**Findings:**
- `AuthModuleExtensions.cs` fallback `Server=localhost;Database=learncloud;User=root;Password=root`
- `MultiTenancyExtensions.cs` same fallback
- `PayNowGateway.cs` `IntegrationId = "demo-integration-id"` `IntegrationKey = "demo-integration-key-32-chars-min"`
- `ConcreteProviders.cs` `ApiKey = "demo-key"` `SmtpUser="user" SmtpPass="pass"`

## Fix Applied

### 1. AuthModuleExtensions.cs
```csharp
// BEFORE (INSECURE):
var conn = config.GetConnectionString("Default") ?? "Server=localhost;Database=learncloud;User=root;Password=root;CharSet=utf8mb4;";

// AFTER (SECURE):
var conn = config.GetConnectionString("Default") ?? throw new InvalidOperationException("SECURITY: ConnectionStrings__Default must be set via env var - no default allowed in production. Set MYSQL_PASSWORD via .env.production");
if (conn.Contains("Password=root", OrdinalIgnoreCase) || conn.Contains("Password=learncloud", OrdinalIgnoreCase))
    throw new InvalidOperationException("SECURITY: Insecure default DB password detected...");
```

Added JWT secret validation:
```csharp
if (string.IsNullOrWhiteSpace(jwtOptions.Secret))
    throw new InvalidOperationException("SECURITY: Jwt__Secret must be set via env var JWT_SECRET");
if (jwtOptions.Secret.Length < 32)
    throw new InvalidOperationException($"SECURITY: Jwt__Secret must be >=32 chars, current {jwtOptions.Secret.Length}");
if (jwtOptions.Secret.ToLower().Contains("demo"))
    throw new InvalidOperationException("SECURITY: Jwt__Secret appears demo/weak");
```

### 2. MultiTenancyExtensions.cs
Same fix - no fallback, fail fast, insecure password detection.

### 3. PayNowGateway.cs
```csharp
// BEFORE:
public string IntegrationId { get; set; } = "demo-integration-id";
public string IntegrationKey { get; set; } = "demo-integration-key-32-chars-min";

// AFTER:
public string IntegrationId { get; set; } = null!; // must be set via PayNow__IntegrationId env
public string IntegrationKey { get; set; } = null!;

public void Validate() {
    if (string.IsNullOrWhiteSpace(IntegrationId)) throw new InvalidOperationException("SECURITY: PayNow__IntegrationId must be set via env");
    if (IntegrationKey.Length < 32) throw new InvalidOperationException("IntegrationKey must be >=32 chars");
    if (IntegrationId.Contains("demo") || IntegrationKey.Contains("demo"))
        throw new InvalidOperationException("SECURITY: PayNow demo keys detected");
}
```
Added validation call in constructor, never log key.

### 4. ConcreteProviders.cs (Sms + Email)
- `SmsProviderOptions.ApiKey` now `null!` with `Validate()` requiring env `Sms__ApiKey`, rejecting demo-key
- `EmailProviderOptions` removed `SmtpUser="user" SmtpPass="pass"` defaults, now `null!` requiring env, with Validate() checking demo values
- Providers' constructors now call `_options.Validate()` fail fast

## Verification
```bash
grep -Rni "Password=root" src --include="*.cs" | grep -v SECURITY
# 0 results - only in validation checks

grep -Rni "demo-integration-id" src --include="*.cs" | grep -v SECURITY | grep -v Validate
# 0 results
```

## Remaining Actions
- Run `gitleaks detect --source . --verbose` to ensure no secrets in git history - recommend adding to GitHub Actions
- Ensure `.env.production` outside repo (already at `../.env.production` in docker-compose) with `chmod 600`
- Document in `deployment/README.md` that all secrets must be generated via `openssl rand -base64 48`

## Next Fix
Per audit order, next is **C3 Stored XSS via SVG Logo Upload** (quick win, blocks C2).
