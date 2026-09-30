using System.Text;
using LearnCloud.PlatformAdmin.Security;
using Xunit;

namespace LearnCloud.PlatformAdmin.Tests;

/// <summary>
/// Checked against the RFC 6238 test vectors, so the codes are the ones a real authenticator
/// app produces rather than merely self-consistent.
/// </summary>
public class TotpTests
{
    // RFC 6238 appendix B: the SHA1 secret is the ASCII "12345678901234567890".
    private static readonly byte[] RfcKey = Encoding.ASCII.GetBytes("12345678901234567890");

    [Theory]
    [InlineData(59L, "287082")]
    [InlineData(1111111109L, "081804")]
    [InlineData(1111111111L, "050471")]
    [InlineData(1234567890L, "005924")]
    [InlineData(2000000000L, "279037")]
    public void Codes_match_the_rfc_6238_vectors(long unixSeconds, string expected) =>
        Assert.Equal(expected, Totp.Generate(RfcKey, unixSeconds / 30));

    [Fact]
    public void Base32_round_trips()
    {
        var secret = Totp.NewSecret();
        Assert.Equal(secret, Totp.ToBase32(Totp.FromBase32(secret)));
    }

    [Fact]
    public void The_current_code_is_accepted_and_a_wrong_one_is_not()
    {
        var secret = Totp.NewSecret();
        var now = DateTimeOffset.UtcNow;
        var code = Totp.Generate(Totp.FromBase32(secret), now.ToUnixTimeSeconds() / 30);

        Assert.True(Totp.IsValid(secret, code, now));
        Assert.False(Totp.IsValid(secret, "000000", now));
        Assert.False(Totp.IsValid(secret, "12345", now));
        Assert.False(Totp.IsValid(secret, null, now));
        Assert.False(Totp.IsValid(secret, "abcdef", now));
    }

    [Fact]
    public void A_code_from_the_step_before_is_still_accepted_but_an_old_one_is_not()
    {
        var secret = Totp.NewSecret();
        var now = DateTimeOffset.UtcNow;
        var key = Totp.FromBase32(secret);
        var step = now.ToUnixTimeSeconds() / 30;

        // A slow clock or a code typed as it rolls over should still work.
        Assert.True(Totp.IsValid(secret, Totp.Generate(key, step - 1), now));
        // Three minutes old should not.
        Assert.False(Totp.IsValid(secret, Totp.Generate(key, step - 6), now));
    }

    [Fact]
    public void The_enrolment_uri_is_the_shape_authenticator_apps_expect()
    {
        var uri = Totp.EnrolmentUri("JBSWY3DPEHPK3PXP", "admin@learncloud.co.zw");

        Assert.StartsWith("otpauth://totp/LearnCloud:admin%40learncloud.co.zw?", uri);
        Assert.Contains("secret=JBSWY3DPEHPK3PXP", uri);
        Assert.Contains("digits=6", uri);
        Assert.Contains("period=30", uri);
    }
}
