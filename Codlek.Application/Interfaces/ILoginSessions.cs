using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Auth;

namespace Codlek.Application.Interfaces;

/// <summary>
/// جلسات الدخول — <b>وده الوحيد اللي المداخل بتندهه</b>.
///
/// <para>🔴 <b>ليه حاجة واحدة بتوقّع وتسجّل مع بعض.</b> توكن اتوقّع
/// ومااتسجّلش في الجدول بيشتغل ١٥ دقيقة وبعدين بيترفض تجديده —
/// فالمستخدم بيتطرد ومحدش يعرف ليه. الخطوتين لازم يبقوا خطوة واحدة
/// عشان النسيان يبقى مستحيل.</para>
/// </summary>
public interface ILoginSessions
{
    /// <summary>
    /// بيبدأ جلسة: توكن وصول + توكن تجديد <b>مسجَّل في الجدول</b>.
    /// </summary>
    Task<TokenPair> StartAsync(TokenSubject subject, CancellationToken ct = default);

    /// <summary>
    /// بيجدّد: بيفحص التوكن، بيلغيه، وبيعمل واحد جديد مكانه.
    ///
    /// <para>🔴 <b>وبيعيد قراءة الحساب من القاعدة.</b> ادعاءات التوكن
    /// الجديد بتتبني من الصف الحالي مش من التوكن القديم — عشان ترقية
    /// صلاحية أو تغيير اسم يبانوا في ربع ساعة، مش في أسبوع.</para>
    /// </summary>
    Task<Result<RefreshedSession>> RefreshAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>
    /// بيقفل كل جلسات حساب — خروج من كل الأجهزة، أو إيقاف إداري.
    /// </summary>
    /// <returns>عدد الجلسات اللي اتقفلت.</returns>
    Task<int> EndAllAsync(Guid userId, string reason, CancellationToken ct = default);
}
