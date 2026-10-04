using System.Security.Cryptography;
using System.Text;

namespace Codlek.Infrastructure.Auth;

/// <summary>
/// نفس خوارزمية البرنامج المكتبي بالظبط (spics.Accounts.AccountStore):
/// PBKDF2-SHA256، ١٠٠ ألف دورة، ملح ١٦ بايت، بصمة ٣٢ بايت، والاتنين Base64.
///
/// التطابق ده مش تفصيلة — هو اللي بيخلّي المدير يرفع ملف accounts.json
/// من الراكة فالحسابات تشتغل على الموقع بنفس الباسوردات، من غير ما حد
/// يعيد ضبط أي حاجة ومن غير ما الباسوردات تتكتب في أي مكان.
/// </summary>
public static class PasswordHasher
{
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public static (string Hash, string Salt) Create(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        return (Convert.ToBase64String(Hash(password, salt)), Convert.ToBase64String(salt));
    }

    public static bool Verify(string password, string storedHash, string storedSalt)
    {
        try
        {
            byte[] salt = Convert.FromBase64String(storedSalt);
            byte[] expected = Convert.FromBase64String(storedHash);
            byte[] actual = Hash(password, salt);

            // مقارنة ثابتة الزمن — منمنعش تخمين الباسورد بقياس وقت الرد
            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        catch
        {
            return false;
        }
    }

    private static byte[] Hash(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password ?? ""),
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashSize);

    /// <summary>كود فني جديد — ٦ أرقام عشوائية آمنة.</summary>
    public static string NewTechnicianCode() =>
        RandomNumberGenerator.GetInt32(100_000, 1_000_000).ToString();

    /// <summary>مفتاح راكة — بيتعرض مرة واحدة بس وقت الإنشاء.</summary>
    public static string NewApiKey() =>
        "ck_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
}
