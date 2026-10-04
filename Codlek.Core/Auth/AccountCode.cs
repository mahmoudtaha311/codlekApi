using System.Security.Cryptography;

namespace Codlek.Core.Auth;

/// <summary>
/// كود حساب — <b>٦ أرقام عشوائية آمنة</b>.
///
/// <para>🔴 <b>الكود ده بيربط الحساب بالفحوصات</b>، فتكراره بيخلط شغل
/// ناس ببعضه. واللي بيستعمله بيجرّب لحد ما يلاقي كود غير مستخدم —
/// العشوائية مش كفاية لوحدها.</para>
///
/// <para>⚠️ <b>و<c>RandomNumberGenerator</c> مش <c>Random</c>.</b>
/// <c>Random</c> بيتزرع بالوقت، فطلبين في نفس اللحظة بيدّوا نفس الرقم
/// — ودي بالظبط الحالة اللي الكود المكرر بيحصل فيها.</para>
///
/// <para>⚠️ ومكانه <c>Core</c> عشان طبقة التطبيق تستعمله من غير ما
/// تشاور على البنية التحتية.</para>
/// </summary>
public static class AccountCode
{
    public static string New() =>
        RandomNumberGenerator.GetInt32(100_000, 1_000_000).ToString();
}
