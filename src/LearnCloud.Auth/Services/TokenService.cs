using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using LearnCloud.Auth.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LearnCloud.Auth.Services;

public class TokenService : ITokenService
{
    private readonly JwtOptions _jwt;
    private const int MaxPermissionsInJwt = 80; // if grows too large, store hash reference

    public TokenService(IOptions<JwtOptions> jwt) { _jwt = jwt.Value; }

    public string GenerateAccessToken(User user, IList<string> roleCodes, IList<string> permissions)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("uid", user.Id.ToString()),
            new("tid", user.TenantId?.ToString() ?? "0"), // 0 = platform
            new("tv", user.TokenVersion.ToString()), // token version for revocation
            new("ss", user.SecurityStamp), // security stamp
            new("email_verified", user.EmailVerified.ToString())
        };

        foreach (var role in roleCodes)
            claims.Add(new Claim(ClaimTypes.Role, role));
        claims.Add(new Claim("roles", string.Join(',', roleCodes))); // easy parse

        // Permission list vs reference
        if (permissions.Count <= MaxPermissionsInJwt)
        {
            foreach (var perm in permissions)
                claims.Add(new Claim("perm", perm));
            // also space delimited for quick check
            claims.Add(new Claim("perms", string.Join(' ', permissions)));
        }
        else
        {
            // Too large, add hash reference + indicate to fetch from DB
            var hash = ComputePermissionsHash(permissions);
            claims.Add(new Claim("perms_hash", hash));
            claims.Add(new Claim("perms_ref", "db")); // signal handler to load from cache
        }

        var expires = DateTime.UtcNow.AddMinutes(_jwt.AccessTokenLifetimeMinutes);

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public (string RawToken, string HashedToken) GenerateRefreshToken()
    {
        var raw = GenerateOpaqueToken(64);
        var hashed = HashToken(raw);
        return (raw, hashed);
    }

    public string GenerateOpaqueToken(int bytes = 64)
    {
        var random = RandomNumberGenerator.GetBytes(bytes);
        return Base64UrlEncode(random);
    }

    public string HashToken(string rawToken)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(rawToken);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant(); // 64 chars hex
    }

    private static string ComputePermissionsHash(IList<string> perms)
    {
        var ordered = perms.OrderBy(p => p);
        var joined = string.Join('|', ordered);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(joined));
        return Convert.ToHexString(hash)[..16].ToLower(); // short hash
    }

    private static string Base64UrlEncode(byte[] input)
    {
        return Convert.ToBase64String(input).Replace('+','-').Replace('/','_').TrimEnd('=');
    }
}
