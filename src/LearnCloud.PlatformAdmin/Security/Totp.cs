using System.Security.Cryptography;
using System.Text;

namespace LearnCloud.PlatformAdmin.Security;

/// <summary>
/// Time-based one-time passwords (RFC 6238): 6 digits, 30-second steps, HMAC-SHA1, which is
/// what Google Authenticator, Authy and 1Password produce by default.
///
/// The platform console can suspend a school and impersonate its users, so it asks for a
/// code from the operator's phone as well as a password. The check used to accept the
/// literal string "123456".
/// </summary>
public static class Totp
{
    private const int Digits = 6;
    private const int StepSeconds = 30;
    /// <summary>One step either side, so a code is not rejected by a slow clock.</summary>
    private const int AllowedDrift = 1;

    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string NewSecret(int bytes = 20) => ToBase32(RandomNumberGenerator.GetBytes(bytes));

    /// <summary>The otpauth:// URI an authenticator app scans.</summary>
    public static string EnrolmentUri(string secretBase32, string account, string issuer = "LearnCloud") =>
        $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(account)}"
        + $"?secret={secretBase32}&issuer={Uri.EscapeDataString(issuer)}&algorithm=SHA1&digits={Digits}&period={StepSeconds}";

    /// <summary>True when the code matches the secret for the current time, within the drift window.</summary>
    public static bool IsValid(string secretBase32, string? code, DateTimeOffset? now = null)
    {
        if (string.IsNullOrWhiteSpace(secretBase32) || string.IsNullOrWhiteSpace(code)) return false;
        code = code.Trim().Replace(" ", "");
        if (code.Length != Digits || !code.All(char.IsAsciiDigit)) return false;

        byte[] key;
        try { key = FromBase32(secretBase32); }
        catch (FormatException) { return false; }

        var step = (now ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds() / StepSeconds;
        for (var drift = -AllowedDrift; drift <= AllowedDrift; drift++)
        {
            var candidate = Generate(key, step + drift);
            // Fixed time: a timing difference would leak which digits were right.
            if (CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(candidate), Encoding.ASCII.GetBytes(code)))
                return true;
        }
        return false;
    }

    public static string Generate(byte[] key, long step)
    {
        var counter = BitConverter.GetBytes(step);
        if (BitConverter.IsLittleEndian) Array.Reverse(counter);

        var hash = HMACSHA1.HashData(key, counter);
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (binary % (int)Math.Pow(10, Digits)).ToString(new string('0', Digits));
    }

    public static string ToBase32(byte[] data)
    {
        var output = new StringBuilder();
        for (var bitIndex = 0; bitIndex < data.Length * 8; bitIndex += 5)
        {
            var groupIndex = bitIndex / 8;
            var value = data[groupIndex] << 8 | (groupIndex + 1 < data.Length ? data[groupIndex + 1] : 0);
            var shifted = (value >> (11 - bitIndex % 8)) & 31;
            output.Append(Base32Alphabet[shifted]);
        }
        return output.ToString();
    }

    public static byte[] FromBase32(string input)
    {
        input = input.Trim().TrimEnd('=').ToUpperInvariant().Replace(" ", "");
        var bits = 0;
        var value = 0;
        var output = new List<byte>(input.Length * 5 / 8);
        foreach (var c in input)
        {
            var index = Base32Alphabet.IndexOf(c);
            if (index < 0) throw new FormatException($"'{c}' is not valid base32");
            value = (value << 5) | index;
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)(value >> (bits - 8)));
                bits -= 8;
            }
        }
        return output.ToArray();
    }
}
