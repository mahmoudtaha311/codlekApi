using Codlek.Core.Entities.Auth;

namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// حسابات لوحة التحكم.
///
/// <para>🔴 <b>دي <c>AspNetUsers</c> مش <c>Technicians</c>.</b> فيه
/// جدولين اسمهم بيتلخبط: الفنيين بيدخلوا على الراكة بحسابات
/// <c>Technicians</c>، ومستخدمي اللوحة هنا. والخلط بينهم بيعمل عطل
/// صامت: «اعمل حساب لأحمد» بيتنفّذ في الجدول الغلط، والحساب بيتعمل
/// بنجاح — وأحمد مش عارف يدخل من المكان اللي هو محتاجه.</para>
/// </summary>
public interface IUserAccountRepository
{
    /// <summary>
    /// حسابات الشركة، <b>الفنيين تحت</b>.
    ///
    /// <para>⚠️ نفس ترتيب الصفحة القديمة عشان اللي بيبص على الاتنين
    /// مايحسش إنهم قايمتين مختلفتين.</para>
    /// </summary>
    Task<IReadOnlyList<ApplicationUser>> ListAsync(
        Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// الصف <b>جوّه شركة الطالب وبس</b> — ومتتبَّع عشان التعديل يتكتب.
    ///
    /// <para>🔴 <b>شرط الشركة هنا هو قفل العزل.</b> المعرّف بييجي من
    /// العميل، ومن غير الشرط ده أي مدير يقدر يبعت معرّف حساب في شركة
    /// تانية ويوقفه.</para>
    /// </summary>
    Task<ApplicationUser?> FindAsync(
        Guid tenantId, Guid id, CancellationToken ct = default);

    /// <summary>
    /// 🔴 <b>الفحص على مستوى النظام كله، مش جوّه الشركة.</b>
    ///
    /// <para>مفتاح الدخول عالمي، فاسم متاخد في شركة تانية هيرفضه
    /// الفهرس وقت الحفظ. من غير الفحص ده، المدير بيشوف استثناء قاعدة
    /// بيانات خام بدل رسالة مفهومة.</para>
    /// </summary>
    Task<bool> UsernameTakenAnywhereAsync(
        string normalizedUsername, CancellationToken ct = default);

    Task<bool> CodeTakenAsync(Guid tenantId, string code, CancellationToken ct = default);

    void Add(ApplicationUser user);
}
