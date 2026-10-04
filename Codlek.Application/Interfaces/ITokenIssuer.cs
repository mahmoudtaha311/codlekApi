namespace Codlek.Application.Interfaces;

/// <summary>التوكنين اللي الدخول بيرجّعهم.</summary>
/// <param name="AccessToken">قصير العمر — بيتبعت مع كل طلب.</param>
/// <param name="RefreshToken">أطول — بيتبدّل بواحد جديد لما الأول ينتهي.</param>
/// <param name="ExpiresInSeconds">عمر توكن الوصول، عشان العميل يعرف يجدّد قبله.</param>
public sealed record TokenPair(string AccessToken, string RefreshToken, int ExpiresInSeconds);

/// <summary>ملخّص المستخدم اللي بيتحط في التوكن.</summary>
public sealed record TokenSubject(
    Guid UserId,
    Guid TenantId,
    string Username,
    string DisplayName,
    string Code,
    string Role,
    int CredentialVersion,
    bool MustChangePassword);

/// <summary>ناتج فك توكن التجديد.</summary>
public sealed record RefreshClaims(Guid UserId, int CredentialVersion);

/// <summary>
/// بيعمل التوكنات وبيفكّها.
///
/// <para>🔴 <b>ليه مفيش جدول للـrefresh tokens.</b> الطريقة المعتادة
/// إن توكن التجديد يتخزّن في جدول عشان ينفع يتلغي. بس المشروع ده
/// بيتوصّل على <b>قاعدة الإنتاج زي ما هي</b> ومش بيعمل هجرات عليها —
/// فجدول جديد مش متاح.</para>
///
/// <para><b>والحل:</b> توكن التجديد نفسه موقّع وشايل
/// <c>CredentialVersion</c>. ووقت التجديد السيرفر بيقرا الحساب من
/// القاعدة ويقارن: لو الرقم اتغيّر (إيقاف أو إعادة تعيين باسورد) أو
/// الحساب بقى موقوف، التجديد بيترفض. يعني الإلغاء شغّال **من غير
/// جدول** — والقاعدة نفسها هي السجل.</para>
///
/// <para>⚠️ والفرق عن الجدول: توكن تجديد مسروق يفضل شغّال لحد ما
/// الباسورد يتغيّر. مع الجدول كان ينفع يتلغي هو لوحده. ده تنازل
/// مقصود مقابل إن الإنتاج مايتلمسش.</para>
/// </summary>
public interface ITokenIssuer
{
    TokenPair Issue(TokenSubject subject);

    /// <summary>
    /// بيفك توكن التجديد ويتأكد من توقيعه.
    ///
    /// <para>⚠️ بيرجّع <c>null</c> لو التوقيع غلط أو انتهى أو مش توكن
    /// تجديد أصلاً. <b>ومابيبصّش للقاعدة</b> — ده شغل المنادي.</para>
    /// </summary>
    RefreshClaims? ReadRefresh(string refreshToken);
}
