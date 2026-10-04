using Codlek.Core.Entities;
using Codlek.Core.Enums;

namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// استقبال الفحوص من المحطة.
///
/// <para>🔴 <b>الصفوف بتترجّع <u>متتبّعة</u> هنا</b> — الاستقبال
/// بيعدّل الفحص الموجود في مكانه وبيكتب على الجهاز، والحفظ من وحدة
/// العمل عشان كله ينزل مرة واحدة.</para>
/// </summary>
public interface IReportIngestRepository
{
    /// <summary>
    /// الفحوص اللي موجودة من الدفعة دي — <b>بأولادها</b>.
    ///
    /// <para>🔴 <b>و<c>AsSplitQuery</c> ضرورية.</b> أربع مجموعات في
    /// استعلام واحد بتتضرب في بعض (مراحل × قطع × تعديلات × لقطة)،
    /// وفي رفعة فيها ٥٠٠ فحص ده بيطلّع <b>عشرات الآلاف</b> من الصفوف
    /// المكررة على الشبكة.</para>
    /// </summary>
    Task<Dictionary<Guid, Report>> ExistingAsync(
        Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken ct = default);

    /// <summary>
    /// أنهي أجهزة من اللي الفحوص بتشاور عليها موجودة فعلاً.
    ///
    /// <para>⚠️ استعلام واحد <b>للدفعة كلها</b> بدل واحد لكل
    /// فحص.</para>
    /// </summary>
    Task<HashSet<Guid>> KnownDeviceIdsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken ct = default);

    /// <summary>
    /// هويات الفنيين اللي الحمولة بتشاور عليهم — <b>من غير ترشيح
    /// بالشركة</b>.
    ///
    /// <para>🔴 <b>وغياب الترشيح ده مقصود.</b> من غيره «فني شركة
    /// تانية» و«فني مش موجود» بيبقوا <b>نفس الحالة</b> — والأولى
    /// لازم <b>ترفض</b> (محاولة كتابة في شركة تانية) والتانية لازم
    /// <b>تعدّي</b> (راكة قديمة بتبعت معرّف حسابها المحلي). الترشيح
    /// كان هيحوّل الاتنين لتجاهل صامت.</para>
    /// </summary>
    Task<IReadOnlyList<TechnicianIdentityRow>> TechnicianIdentitiesAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken ct = default);

    /// <summary>
    /// المعرّف الكانوني لجهاز اتدمج — <c>null</c> لو مش مستعار.
    ///
    /// <para>🔴 <b>والترجمة دي قبل أي حاجة.</b> الراكة بتبعت بمعرّفها
    /// المحلي، واللي ممكن يكون اتعرّف عليه كجهاز موجود وقت مزامنة
    /// الأجهزة. من غير الخطوة دي، الفحص بيروح <b>لجهاز مكرر</b> بدل
    /// الكانوني.</para>
    /// </summary>
    Task<Guid?> CanonicalForAliasAsync(
        Guid tenantId, Guid aliasDeviceId, CancellationToken ct = default);

    /// <summary>
    /// الأجهزة اللي المرساة دي بتشاور عليها — <b>اتنين كفاية</b>.
    ///
    /// <para>⚠️ واحد = تطابق. أكتر من واحد = عيب بيانات، والفحص
    /// بيتسيب للمراجعة بدل ما نختار واحد عشوائي.</para>
    /// </summary>
    Task<IReadOnlyList<Guid>> DevicesByIdentifierAsync(
        Guid tenantId, DeviceIdentifierKind kind, string normalizedValue,
        CancellationToken ct = default);

    void Add(Report report);

    /// <summary>
    /// بيشيل أولاد الفحص قبل ما يتكتبوا من تاني.
    ///
    /// <para>⚠️ <b>بدل ما نحاول نوفّق بينهم.</b> المراحل والقطع
    /// والتعديلات واللقطة كلهم ملك الراكة بالكامل، والتوفيق صف بصف
    /// كان بيحتاج مفتاح ثابت لكل واحد — ومفيش.</para>
    /// </summary>
    void RemoveChildren(Report report);

    /// <summary>الأجهزة اللي وصلها فحص — <b>متتبّعة</b>.</summary>
    Task<IReadOnlyList<Device>> DevicesAsync(
        Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken ct = default);

    /// <summary>
    /// أدلة الاسم التجاري من فحوص الجهاز — <b>من الأحدث للأقدم</b>.
    ///
    /// <para>⚠️ الفحوص الممسوحة واللي مالهاش اسم تجاري بيتشالوا في
    /// الاستعلام.</para>
    /// </summary>
    Task<IReadOnlyList<ModelEvidenceRow>> ModelEvidenceAsync(
        Guid deviceId, CancellationToken ct = default);

    /// <summary>
    /// كل فحوص الأجهزة دي — <b>بأربع خانات بس</b>.
    ///
    /// <para>⚠️ <b>بنجيب القايمة ونختار آخر اتنين في الذاكرة</b> بدل
    /// <c>GroupBy</c> فيه <c>FirstOrDefault</c> جوّاه: الشكل التاني
    /// ترجمته في EF هشّة، والإسقاط هنا أربع خانات رقمية على فهرس
    /// موجود — فالرحلة رخيصة والسلوك مضمون.</para>
    /// </summary>
    Task<IReadOnlyList<ReportCursorRow>> ReportCursorsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> deviceIds, CancellationToken ct = default);

    /// <summary>لقطات الفحوص دي — استعلام واحد.</summary>
    Task<IReadOnlyList<ReportSnapshotComponent>> SnapshotComponentsAsync(
        IReadOnlyCollection<Guid> reportIds, CancellationToken ct = default);
}

/// <summary>
/// هوية فني زي ما القاعدة شايفاها.
///
/// <para>🔴 <b>وفيها <c>TenantId</c> عن قصد</b> — هي اللي بيتقرّر
/// بيها «الفني ده بتاع الورشة دي ولا لأ».</para>
/// </summary>
public sealed class TechnicianIdentityRow
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public string Code { get; init; } = "";
}

/// <summary>دليل اسم تجاري من فحص.</summary>
public sealed class ModelEvidenceRow
{
    public string? CommercialModelName { get; init; }
    public string? CommercialModelSource { get; init; }
    public string? MachineType { get; init; }
    public DateTime StartedAtUtc { get; init; }
}

/// <summary>صف فحص مختصر — للمقارنة بين آخر فحصين.</summary>
public sealed class ReportCursorRow
{
    public Guid DeviceId { get; init; }
    public Guid ReportId { get; init; }
    public DateTime StartedAtUtc { get; init; }
    public bool SnapshotIsPartial { get; init; }
}
