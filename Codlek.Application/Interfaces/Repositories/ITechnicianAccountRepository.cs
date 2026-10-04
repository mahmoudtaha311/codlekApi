using Codlek.Core.Entities;

namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// حسابات الفنيين — <b>المكان الوحيد اللي بيلمس الجدول
/// للكتابة</b>.
///
/// <para>🔴 <b>والصفوف بتترجّع <u>متتبّعة</u>:</b> كل إجراء بيعدّل
/// الصف في مكانه، والحفظ من وحدة العمل — عشان السجل والتعديل ينزلوا
/// في حفظة واحدة.</para>
///
/// <para>⚠️ <b>ومفيش دالة واحدة هنا بتاخد معرّف شركة من
/// الطلب.</b> الشركة بتيجي من التوكن دايماً — ولو أخدها من الطلب،
/// مدير شركة كان هيقدر يعمل فني في شركة تانية.</para>
/// </summary>
public interface ITechnicianAccountRepository
{
    /// <summary>كل فنيي الشركة، مرتّبين بالاسم.</summary>
    Task<IReadOnlyList<Technician>> ListAsync(
        Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// فني بعينه — <b>متتبّع</b>.
    ///
    /// <para>⚠️ <c>null</c> هنا معناها «مش في الشركة دي» — وممكن
    /// يكون موجود في شركة تانية. والرسالة مابتفرّقش عن قصد.</para>
    /// </summary>
    Task<Technician?> FindAsync(
        Guid tenantId, Guid technicianId, CancellationToken ct = default);

    /// <summary>
    /// اسم الدخول محجوز؟ — <b>جوّه الشركة، مش عالمياً</b>.
    ///
    /// <para>🔴 وده <b>عكس</b> مستخدمي اللوحة بالظبط. الفني بيدخل من
    /// راكة متحققة بمفتاحها، والشركة معروفة <b>من الراكة</b> قبل ما
    /// الاسم يتقرا — فشركتين ينفع يبقى عندهم «ahmed».</para>
    /// </summary>
    Task<bool> UsernameTakenAsync(
        Guid tenantId, string normalizedUsername, CancellationToken ct = default);

    /// <summary>الكود محجوز؟ — لحلقة توليد الكود.</summary>
    Task<bool> CodeTakenAsync(
        Guid tenantId, string code, CancellationToken ct = default);

    void Add(Technician technician);

    // =================================================================
    //  الماركات
    // =================================================================

    /// <summary>ربط الماركات لكل فنيي الشركة — <b>قراية واحدة</b>.</summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> BrandLinksAsync(
        Guid tenantId, CancellationToken ct = default);

    /// <summary>ربط ماركات فني واحد — <b>متتبّع</b> عشان الاستبدال.</summary>
    Task<IReadOnlyList<TechnicianBrand>> BrandLinksForAsync(
        Guid tenantId, Guid technicianId, CancellationToken ct = default);

    /// <summary>
    /// كام ماركة من دول موجودة فعلاً في قايمة الشركة.
    ///
    /// <para>⚠️ العدّ بدل القراية: المنادي عايز يعرف إن <b>كلهم</b>
    /// موجودين، مش مين الناقص — والرسالة واحدة.</para>
    /// </summary>
    Task<int> CountKnownBrandsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> brandIds, CancellationToken ct = default);

    /// <summary>أسماء ماركات — لنص السجل.</summary>
    Task<IReadOnlyList<string>> BrandNamesAsync(
        Guid tenantId, IReadOnlyCollection<Guid> brandIds, CancellationToken ct = default);

    void RemoveBrandLinks(IEnumerable<TechnicianBrand> links);

    void AddBrandLink(TechnicianBrand link);
}
