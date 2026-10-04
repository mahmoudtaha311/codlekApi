using Codlek.Application.Interfaces;

namespace Codlek.Infrastructure.Auth;

/// <summary>
/// ⚠️ تمريرة على <see cref="PasswordHasher"/> — مفيش نسخة تانية من
/// البصمة، عشان المفاتيح اللي اتصدرت قبل كده تفضل تتحقق.
/// </summary>
public sealed class RackKeys : IRackKeys
{
    public (string Key, string Hash, string Salt) Issue()
    {
        string key = PasswordHasher.NewApiKey();
        var (hash, salt) = PasswordHasher.Create(key);

        return (key, hash, salt);
    }

    public bool Verify(string key, string storedHash, string storedSalt) =>
        PasswordHasher.Verify(key, storedHash, storedSalt);
}
