using Codlek.Core.Entities;
using Codlek.Core.Text;

namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// أوامر الصيانة.
///
/// <para>⚠️ <b>الواجهة ضيّقة، وكل دالة بتاخد <c>tenantId</c>
/// إجباري.</b> المستودع العام بيرجّع الترشيح بالشركة لكل مكان نداء —
/// وأول واحد ينساه بيفتح شغل شركة تانية.</para>
/// </summary>
public interface IRepairRepository
{
    // =================================================================
    //  الأمر
    // =================================================================

    /// <summary>
    /// الأمر <b>متتبَّع</b> — كل اللي بينده الدالة دي بيكتب.
    ///
    /// <para>⚠️ ولو رجع غير متتبَّع، التعديل بيعدّي والحفظ مابيكتبش
    /// حاجة — نقطة بترجّع نجاح والقاعدة زي ما هي.</para>
    /// </summary>
    Task<RepairWorkItem?> FindAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    /// <summary>الأمر ومعاه قطع الغيار — لصفوف الطابور.</summary>
    Task<RepairWorkItem?> FindWithPartsAsync(
        Guid tenantId, Guid id, CancellationToken ct = default);

    void Add(RepairWorkItem item);

    // =================================================================
    //  الجهاز
    // =================================================================

    /// <summary>
    /// جهاز الأمر — <b>ومعاه حالته في عمر النظام</b>.
    ///
    /// <para>🔴 الجهاز المدموج مش لاب مستقل: أمره بيتفتح على
    /// الكانوني، فالمنادي محتاج يعرف الحالة مش الوجود بس.</para>
    /// </summary>
    Task<Device?> FindDeviceAsync(
        Guid tenantId, Guid deviceId, CancellationToken ct = default);

    /// <summary>
    /// ⚠️ <b>الفحص الممسوح مش موجود.</b> أمر متعلّق بفحص اتمسح معناه
    /// سلسلة مقطوعة.
    /// </summary>
    Task<bool> ReportExistsAsync(
        Guid tenantId, Guid reportId, CancellationToken ct = default);

    // =================================================================
    //  الفني
    // =================================================================

    Task<Technician?> FindTechnicianAsync(
        Guid tenantId, Guid technicianId, CancellationToken ct = default);

    /// <summary>
    /// الفنيون اللي ينفع يستلموا صيانة دلوقتي.
    ///
    /// <para>🔴 <b>نشط <u>و</u> يقدر يصلّح.</b> الموقوف مايظهرش حتى
    /// لو قدرته شغّالة — إسناد شغل لحد مش قادر يدخل معناه أمر
    /// بيقعد.</para>
    /// </summary>
    Task<IReadOnlyList<Technician>> AssignableTechniciansAsync(
        Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// ماركات الفني — <b>فاضية معناها مفيش قيد</b>.
    ///
    /// <para>⚠️ مش معناها «ممنوع من كل حاجة». ولو اتفهمت غلط، أول
    /// فني بيتعمل مايقدرش يمسك ولا لاب.</para>
    /// </summary>
    Task<IReadOnlyCollection<Guid>> TechnicianBrandsAsync(
        Guid tenantId, Guid technicianId, CancellationToken ct = default);

    // =================================================================
    //  الماركات
    // =================================================================

    /// <summary>
    /// قواعد الماركات كبيانات — عشان <see cref="BrandToken"/> تفضل
    /// دالة نقية.
    ///
    /// <para>⚠️ <b>بتتقرا في كل مرة، مش من كاش.</b> الكاش بيبقى قديم
    /// لما صاحب الشغل يضيف اسم بديل، والقرار اللي بيمنع فني لازم
    /// يبقى على آخر قاعدة — مش على نسخة محفوظة من إمبارح.</para>
    /// </summary>
    Task<IReadOnlyList<BrandRule>> BrandRulesAsync(
        Guid tenantId, CancellationToken ct = default);

    /// <summary>المصنّع الخام للجهاز — مدخل <see cref="BrandToken.Resolve"/>.</summary>
    Task<string> DeviceManufacturerAsync(
        Guid tenantId, Guid deviceId, CancellationToken ct = default);

    // =================================================================
    //  الطابور
    // =================================================================

    /// <summary>
    /// الأوامر المستنية قرار — <b>الأقدم الأول</b>.
    ///
    /// <para>⚠️ دي طابور شغل مش قايمة أخبار.</para>
    /// </summary>
    Task<IReadOnlyList<RepairWorkItem>> PendingAsync(
        Guid tenantId, CancellationToken ct = default);

    // =================================================================
    //  القرايات — القايمة والتفاصيل
    // =================================================================

    /// <summary>
    /// القايمة المصفّحة + العدد الكلي <b>قبل</b> التصفيح.
    ///
    /// <para>🔴 <b>الترتيب لازم يبقى فيه فاصل تعادل ثابت</b>
    /// (<c>ThenBy(Id)</c>): <c>OpenedAtUtc</c> مش فريد — دفعة أوامر
    /// اتفتحت من نفس الفحص بتاخد نفس المللي ثانية، ومن غير الفاصل
    /// الصف بيتكرر في صفحتين أو بيختفي بين <c>Skip</c>
    /// و<c>Take</c>.</para>
    /// </summary>
    Task<(IReadOnlyList<RepairListRow> Rows, int TotalItems)> ListAsync(
        Guid tenantId, RepairListFilter filter, CancellationToken ct = default);

    /// <summary>
    /// نفس الفلتر ونفس الترتيب، <b>بلا تصفيح</b> — للتصدير.
    /// </summary>
    /// <param name="cap">
    /// 🔴 <b>سقف الصفوف — والمستودع بيجيب <c>cap + 1</c>.</b>
    ///
    /// <para>الصف الزيادة هو اللي بيخلّي المنادي يعرف إن فيه قص
    /// ويقوله <b>جوّه الملف</b>. ملف مقصوص في صمت بيتقري على إنه كل
    /// البيانات — والمدير بيبني عليه قرار جرد.</para>
    /// </param>
    Task<IReadOnlyList<RepairListRow>> ExportAsync(
        Guid tenantId, RepairListFilter filter, int cap, CancellationToken ct = default);

    /// <summary>
    /// كام أمر واقف على المحاسب — <b>في الشركة كلها</b>.
    ///
    /// <para>🔴 <b>برّه الفلاتر وبرّه التصفيح عن قصد.</b> لو اتحسب
    /// من الصفوف المعروضة، «فيه ٣ مستنيين» بتختفي لو التلاتة في
    /// صفحة ٣ — والمدير على صفحة ١ بيفتكر إن مفيش حاجة واقفة.</para>
    /// </summary>
    Task<int> CountAwaitingApprovalAsync(
        Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// كل أوامر جهاز واحد — <b>من غير تصفيح ومن غير فلاتر</b>،
    /// الأحدث فتحاً الأول.
    /// </summary>
    Task<IReadOnlyList<RepairListRow>> ListForDeviceAsync(
        Guid tenantId, Guid deviceId, CancellationToken ct = default);

    /// <summary>
    /// الأمر للعرض — <c>AsNoTracking</c> ومعاه الأعطال والقطع.
    /// </summary>
    Task<RepairWorkItem?> FindDetailAsync(
        Guid tenantId, Guid id, CancellationToken ct = default);

    /// <summary>
    /// حركات السجل التشغيلي المرتبطة بالأمر، بترتيب الحدوث.
    /// </summary>
    Task<IReadOnlyList<RepairWorkflowFacts>> ListWorkflowAsync(
        Guid tenantId, Guid workItemId, CancellationToken ct = default);

    /// <summary>
    /// أسماء فنيي الشركة كقاموس — <b>قراية واحدة بدل N+1</b> على
    /// شاشة التفاصيل.
    ///
    /// <para>⚠️ الفني المحذوف أو المجهول مش في القاموس، والشاشة
    /// بتعرض اسم فاضي عشان تفضل تفتح.</para>
    /// </summary>
    Task<IReadOnlyDictionary<Guid, string>> TechnicianNamesAsync(
        Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// الجهاز موجود في الشركة دي؟ — لتبويب الصيانة على صفحة الجهاز.
    ///
    /// <para>⚠️ الترشيح بالشركة هو اللي بيخلّي جهاز شركة تانية
    /// <c>404</c> مش <c>403</c>.</para>
    /// </summary>
    Task<bool> DeviceExistsAsync(
        Guid tenantId, Guid deviceId, CancellationToken ct = default);

    /// <summary>
    /// بيانات عرض اللاب — <c>null</c> لو مش موجود.
    /// </summary>
    Task<RepairDeviceFacts?> DeviceFactsAsync(
        Guid tenantId, Guid deviceId, CancellationToken ct = default);
}
