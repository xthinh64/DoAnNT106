using System.Security.Cryptography;

namespace OnlineChessGame.Auth;

public static class PasswordHasher
{
    private const string Prefix = "pbkdf2-sha256$v1$600000$";
    public static string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        var salt = RandomNumberGenerator.GetBytes(16);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, 600_000, HashAlgorithmName.SHA256, 32);
        return Prefix + Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(key);
    }

    public static bool Verify(string password, string encoded)
    {
        if (password is null || encoded is null || !encoded.StartsWith(Prefix, StringComparison.Ordinal)) return false;
        var parts = encoded.Split('$');
        if (parts.Length != 5) return false;
        try
        {
            var salt = Convert.FromBase64String(parts[3]);
            var expected = Convert.FromBase64String(parts[4]);
            if (salt.Length != 16 || expected.Length != 32) return false;
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, 600_000, HashAlgorithmName.SHA256, 32);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException) { return false; }
    }
}
