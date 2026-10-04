namespace Codlek.Application.Contracts.Racks;

/// <summary>
/// كود تفعيل مستني — <b>من غير الكود نفسه</b>.
///
/// <para>🔴 <b>الكود عمره ما بيتخزّن:</b> بصمته بس اللي في القاعدة،
/// زي الباسورد بالظبط. اللي بيتعرض هنا هو البادئة (٤ حروف) عشان
/// المدير يعرف أنهي صف هو أنهي كود. الكود الكامل بيترجّع مرة واحدة
/// وقت الإنشاء في <see cref="ActivationCodeIssued"/> وخلاص.</para>
///
/// <para>⚠️ <see cref="IsExpired"/> <b>محسوبة في المعالج</b> مش في
/// الاستعلام — ساعة السيرفر مش جزء من الصف.</para>
/// </summary>
public sealed record ActivationCodeRow(
    Guid Id,
    string Prefix,
    string IntendedName,
    string IntendedLocation,
    string CreatedByName,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc,
    bool IsExpired,
    int FailedAttempts);
