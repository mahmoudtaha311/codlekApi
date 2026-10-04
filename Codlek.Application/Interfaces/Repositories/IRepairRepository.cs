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
}
