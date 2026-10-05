namespace Codlek.Application.Contracts.Auth;

/// <summary>
/// «أنا مين» — <b>نفس شكل القديم بالحرف، ونفس الترتيب</b>.
///
/// <para>🔴 <b>واللوحة بتقرا الأعلام دي مباشرةً.</b> شاشة المراجعة
/// بتتفتح بـ<c>isOwner</c>، وأزرار الموافقة بـ<c>isRepairApprover</c>،
/// والقوايم بـ<c>isManagerOrAbove</c>. نسخة سابقة من المشروع ده كانت
/// بترجّع <c>userId</c> و<c>role</c> وبس — فبعد التحويل المالك كان
/// هيلاقي شاشاته مقفولة في وشّه من غير أي خطأ. اتلقط بتشغيل فحوص
/// القديم على الجديد.</para>
///
/// <para>⚠️ والأعلام نفسها من <c>ICurrentUser</c> — نفس اللي الحواجز
/// بتفحصه، فاللوحة مابتعرضش زرار السيرفر هيرفضه.</para>
/// </summary>
/// <param name="MustChangePassword">
/// ⚠️ <b>زيادة على القديم</b> — الكوكي القديمة كانت بتحوّل لصفحة
/// الحساب من السيرفر؛ هنا اللوحة محتاجة تعرفها بنفسها. الحقل الزيادة
/// مابيكسرش قارئ قديم.
/// </param>
public sealed record MeResponse(
    Guid UserId,
    Guid TenantId,
    string Username,
    string DisplayName,
    string Code,
    string Role,
    string RoleText,
    bool IsManagerOrAbove,
    bool IsOwner,
    bool IsRepairApprover,
    bool MustChangePassword);
