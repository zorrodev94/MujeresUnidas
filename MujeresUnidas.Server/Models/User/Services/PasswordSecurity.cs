using System.Security.Cryptography;
namespace MujeresUnidas.Server.Models.User.Services
{
/// <summary>PBKDF2-HMAC-SHA256 password hashing; passwords are never persisted in cleartext.</summary>
public static class PasswordSecurity
{
    private const int Iterations = 210_000;
    public static (byte[] Salt, byte[] Hash) Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        return (salt, Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32));
    }
    public static bool Verify(string password, byte[] salt, byte[] expected)
    {
        byte[] actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }
    public static string NewToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    public static byte[] TokenDigest(string token) => SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
}
}
