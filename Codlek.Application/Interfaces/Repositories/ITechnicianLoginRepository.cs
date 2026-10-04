using Codlek.Core.Entities;

namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// دخول الفني من محطة فحص — <b>وعدّاد محاولاته</b>.
///
/// <para>🔴 <b>وكل دالة هنا بتاخد الشركة من المحطة مش من
/// الطلب.</b> طلب الدخول <b>مالوش</b> شركة خالص: الشركة بتتقرا من
/// صف المحطة اللي اتحققت بمفتاحها. فالدخول العابر للشركات مش
/// «ممنوع» — هو <b>مش موجود كمسار</b>. ولو الطلب أخد
/// <c>tenantId</c>، أي مفتاح محطة مسروق كان بيفتح فنيي كل
/// الشركات.</para>
/// </summary>
public interface ITechnicianLoginRepository
{
    /// <summary>
    /// عدد المحاولات الفاشلة من <b>نفس المحطة على نفس الاسم</b> في
    /// النافذة.
    ///
    /// <para>🔴 <b>والعدّ من القاعدة مش من ذاكرة العملية.</b> الحد
    /// على مستوى HTTP بيقسّم بالمحطة بس — مايقدرش يقرا الاسم من جسم
    /// JSON من غير ما يستهلك المجرى. والقاعدة بتخلّي العدّ يعيش بعد
    /// إعادة تشغيل السيرفر.</para>
    /// </summary>
    Task<int> RecentFailuresAsync(
        Guid rackId, string normalizedUsername, DateTime sinceUtc,
        CancellationToken ct = default);

    /// <summary>
    /// الفني بالاسم المطبَّع <b>جوّه شركة المحطة</b> — ومتتبّع.
    ///
    /// <para>⚠️ متتبّع عشان الدخول الناجح بيكتب
    /// <c>LastSuccessfulLoginUtc</c>، وتغيير الباسورد بيكتب البصمة
    /// والنسخة — والحفظ من وحدة العمل مرة واحدة.</para>
    /// </summary>
    Task<Technician?> FindByLoginKeyAsync(
        Guid tenantId, string normalizedUsername, CancellationToken ct = default);

    /// <summary>
    /// بيضيف صف محاولة — <b>والحفظ من وحدة العمل</b>.
    ///
    /// <para>⚠️ <b>الاسم اللي اتكتب بيتسجّل حتى لو مش موجود.</b>
    /// محاولات كتير على أسماء مش موجودة من نفس المحطة دي إشارة
    /// تخمين، والإشارة بتضيع لو سجّلنا اللي لقينا لهم حساب
    /// بس.</para>
    ///
    /// <para>🔴 <b>ومفيش باسورد ولا بصمة بيتكتبوا هنا ولا في أي
    /// سجل.</b></para>
    /// </summary>
    void AddAttempt(TechnicianLoginAttempt attempt);
}
