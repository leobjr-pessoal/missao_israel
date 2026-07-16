using System.Security.Cryptography;
using System.Text;
using MissaoIsrael.Domain;

namespace MissaoIsrael.Application;

public sealed class AuthService(IAdminUserRepository users)
{
    public async Task<AdminUser?> ValidateAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByEmailAsync(email, cancellationToken);
        if (user is null || !user.IsActive) return null;
        return VerifyPassword(password, user.PasswordHash) ? user : null;
    }

    public static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 120000, HashAlgorithmName.SHA256, 32);
        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public static bool VerifyPassword(string password, string passwordHash)
    {
        var parts = passwordHash.Split('.');
        if (parts.Length != 2) return false;
        var salt = Convert.FromBase64String(parts[0]);
        var expected = Convert.FromBase64String(parts[1]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, 120000, HashAlgorithmName.SHA256, 32);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
