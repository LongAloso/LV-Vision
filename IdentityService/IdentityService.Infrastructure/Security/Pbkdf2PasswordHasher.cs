using System.Security.Cryptography;
using System.Text;
using IdentityService.Domain.Interfaces;

namespace IdentityService.Infrastructure.Security;

public class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;
    private const char Delimiter = '.';

    public string HashPassword(string password)
    {
        using var rng = RandomNumberGenerator.Create();
        var salt = new byte[SaltSize];
        rng.GetBytes(salt);

        var key = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            KeySize);

        return string.Join(Delimiter, Convert.ToBase64String(salt), Convert.ToBase64String(key), Iterations);
    }

    public bool VerifyHashedPassword(string hashedPassword, string providedPassword)
    {
        var parts = hashedPassword.Split(Delimiter);
        if (parts.Length != 3) return false;

        var salt = Convert.FromBase64String(parts[0]);
        var key = Convert.FromBase64String(parts[1]);
        var iterations = int.Parse(parts[2]);

        var keyToCheck = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(providedPassword),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            key.Length);

        return CryptographicOperations.FixedTimeEquals(key, keyToCheck);
    }
}
