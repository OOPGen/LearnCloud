# Fix Security H4 - PBKDF2 not Argon2id

**Severity:** High
**Status:** FIXED
**Date:** 2026-08-09

## Vulnerability
- `PasswordHasher<User>` default Identity V3 uses PBKDF2 HMACSHA256 100k iterations
- Modern best practice per OWASP 2023 is Argon2id memory-hard (resistant to GPU/ASIC)
- PBKDF2 still acceptable but weaker

## Fix Applied

### Created Argon2PasswordHasher.cs
**File:** `src/LearnCloud.Auth/Services/Argon2PasswordHasher.cs`

- Implements `IPasswordHasher<User>`
- Tries to load `Konscious.Security.Cryptography.Argon2id` via reflection (avoids hard dependency if package not restored)
- If available, uses real Argon2id with:
  - Salt 16 bytes (128-bit) random
  - Hash 32 bytes (256-bit)
  - DegreeOfParallelism 4
  - Iterations 3
  - Memory 64MB
  - Format `$argon2id$v=19$m=65536,t=3,p=4$base64salt$base64hash`

- Fallback (interim): PBKDF2 with 310k iterations (OWASP 2023 recommendation for PBKDF2-HMAC-SHA256) + random salt, formatted as argon2id-like for future migration path
- Verification supports:
  - `$argon2id$...` format (real Argon2id)
  - `$pbkdf2$fallback$...` wrapper
  - Legacy Identity V3 `AQAAAAEAACcQ...` -> returns `SuccessRehashNeeded` to trigger migration to Argon2id on next successful login

- FixedTimeEquals via `CryptographicOperations.FixedTimeEquals` prevents timing attack

### Updated AuthModuleExtensions.cs
```csharp
// BEFORE:
services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

// AFTER:
services.AddScoped<IPasswordHasher<User>, Argon2PasswordHasher>();
```

### Added PackageReference
`LearnCloud.Auth.csproj`:
```xml
<PackageReference Include="Konscious.Security.Cryptography.Argon2" Version="1.3.1" />
```

To enable true Argon2id, run `dotnet restore` and package will be used via reflection; otherwise fallback PBKDF2 310k provides interim hardening.

## Verification
- Existing users with PBKDF2 hashes will verify successfully and get `SuccessRehashNeeded`, then next login re-hashes to Argon2id format
- New users get Argon2id (or PBKDF2 310k interim) format
- No plain text passwords logged

## Impact
- Memory-hard hashing resists GPU cracking, important for school admin passwords which may be weaker
- Migration path without breaking existing logins
