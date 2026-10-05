using Codlek.Core.Entities;
using Codlek.Core.Enums;

namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// صيانة الإقلاع — اللي القديم كان بيعمله مع كل تشغيل
/// (<c>CodlekWeb/Program.cs:250-346</c>).
///
/// <para>🔴 <b>القراية بإسقاط مش بكيانات، والكتابة تحديث مباشر.</b>
/// اللفّات دي بتمشي على كل الفحوص اليتيمة وكل الأجهزة، والاستضافة رامها
/// قليل. القديم كان بيحمّل الصفوف متتبّعة بكل أعمدتها (والنسخة الخام
/// ~٢٠ كيلوبايت للفحص)؛ هنا كل دفعة بتجيب العمودين اللي محتاجهم وبس،
/// ومفيش متتبّع بيكبر بين الدفعات.</para>
///
/// <para>⚠️ <b>والتحديث بينزل فوراً — مش مع وحدة العمل.</b> وده مقصود:
/// كل دفعة بتتحفظ لوحدها زي القديم (<c>SaveChanges</c> بعد كل دفعة)،
/// فوقوف البرنامج في النص بيسيب اللي خلص محفوظ والتشغيلة الجاية بتكمّل.</para>
///
/// <para>⚠️ <b>وكل تحديث شايل شرطه تاني</b> (مثلاً «لسه من غير
/// جهاز»). الراكة ممكن تكتب على نفس الصف بين القراية والكتابة، والشرط
/// ده اللي بيمنع اللفّة تدهس كتابة أحدث منها.</para>
///
/// <para>⚠️ <b>كل دالة بتاخد الشركة</b> — ماعدا قايمة الشركات نفسها
/// وسؤال «فيه شركة أصلاً؟» اللي مالهمش معنى غير على مستوى النظام.</para>
/// </summary>
public interface IMaintenanceRepository
{
    // ── الشركات ────────────────────────────────────────────────────

    /// <summary>فيه أي شركة في القاعدة؟ — سؤال أول تشغيل.</summary>
    Task<bool> AnyTenantAsync(CancellationToken ct = default);

    void AddTenant(Tenant tenant);

    /// <summary>كل الشركات — الصيانة بتمشي على كل واحدة لوحدها.</summary>
    Task<IReadOnlyList<Guid>> TenantIdsAsync(CancellationToken ct = default);

    // ── جهات التسليم ───────────────────────────────────────────────

    /// <summary>
    /// أكواد كل مواقع الشركة — <b>بالموقوف كمان</b>.
    ///
    /// <para>⚠️ الموقع الموقوف قرار حد، ورجوعه كصف جديد بيلغي القرار
    /// ده. القديم بيقارن بكل الصفوف بنفس الطريقة.</para>
    /// </summary>
    Task<IReadOnlyList<string>> LocationCodesAsync(Guid tenantId, CancellationToken ct = default);

    void AddLocation(Location location);

    // ── أسامي أكواد المصنّع ─────────────────────────────────────────

    /// <summary>
    /// أجهزة الشركة اللي اسمها التجاري مصدره <c>SystemFamily</c> — دفعة
    /// بعد <paramref name="after"/>.
    /// </summary>
    Task<IReadOnlyList<CommercialNameRow>> SystemFamilyDeviceNamesAsync(
        Guid tenantId, Guid after, int take, CancellationToken ct = default);

    /// <summary>بيمسح الاسم والمصدر — على الصفوف اللي لسه مصدرها <c>SystemFamily</c> بس.</summary>
    Task<int> ClearDeviceCommercialModelAsync(
        Guid tenantId, IReadOnlyCollection<Guid> deviceIds, CancellationToken ct = default);

    /// <inheritdoc cref="SystemFamilyDeviceNamesAsync"/>
    Task<IReadOnlyList<CommercialNameRow>> SystemFamilyReportNamesAsync(
        Guid tenantId, Guid after, int take, CancellationToken ct = default);

    /// <inheritdoc cref="ClearDeviceCommercialModelAsync"/>
    Task<int> ClearReportCommercialModelAsync(
        Guid tenantId, IReadOnlyCollection<Guid> reportIds, CancellationToken ct = default);

    // ── الفحوص اليتيمة ──────────────────────────────────────────────

    /// <summary>
    /// فحوص الشركة اللي <b>مالهاش جهاز</b> — دفعة بعد <paramref name="after"/>
    /// بترتيب المعرّف.
    ///
    /// <para>🔴 <b>بالمفتاح مش بالفلتر — وبالمتعلّم كمان.</b> الصف اللي
    /// اتعلّم للمراجعة لازم يتعاد تقييمه: الغموض اللي منع المطابقة (جهازين
    /// بنفس المرساة) بيتحل بعدين. ولو الخروج من اللفة كان بالفلتر، الصفوف
    /// اللي فضلت بلا تطابق كانت هترجع في كل دفعة واللفة ماتخلصش.</para>
    /// </summary>
    Task<IReadOnlyList<OrphanReportRow>> OrphanReportsAsync(
        Guid tenantId, Guid after, int take, CancellationToken ct = default);

    /// <summary>
    /// بيربط الفحوص دي بالجهاز ويشيل علامة المراجعة — <b>على اللي لسه
    /// من غير جهاز بس</b>.
    /// </summary>
    Task<int> LinkReportsAsync(
        Guid tenantId, Guid deviceId, IReadOnlyCollection<Guid> reportIds,
        CancellationToken ct = default);

    /// <summary>
    /// بيعلّم الفحوص دي «محتاجة تحديد جهاز» — <b>على اللي لسه من غير جهاز
    /// ومش متعلّم بس</b>.
    /// </summary>
    Task<int> FlagReportsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> reportIds, CancellationToken ct = default);

    // ── «مشكوك إنه مكرر» ────────────────────────────────────────────

    /// <summary>
    /// الأجهزة اللي شايلة مرساة نشطة <b>جهاز تاني شايلها كمان</b> —
    /// من غير المدموجين.
    ///
    /// <para>🔴 <b>نفس الاستعلامين بتوع القديم بالحرف</b>
    /// (<c>DeviceSyncService.cs:651-667</c>): القيمة «مشتركة» لما
    /// (النوع، القيمة) يبقى على أكتر من جهاز؛ وبعدين أي جهاز شايل
    /// <b>القيمة</b> دي — بأي نوع — بيبقى مشكوك فيه.</para>
    /// </summary>
    Task<HashSet<Guid>> DuplicateSuspectsAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// أجهزة الشركة اللي حالتها «شغّال» أو «مشكوك إنه مكرر» — دفعة بعد
    /// <paramref name="after"/>. <b>المدموج والمتقاعد مش هنا خالص.</b>
    /// </summary>
    Task<IReadOnlyList<DeviceStatusRow>> ReviewableDevicesAsync(
        Guid tenantId, Guid after, int take, CancellationToken ct = default);

    /// <summary>
    /// بينقل الأجهزة دي من حالة لحالة — <b>على اللي لسه في
    /// <paramref name="from"/> بس</b>.
    /// </summary>
    Task<int> MoveStatusAsync(
        Guid tenantId, IReadOnlyCollection<Guid> deviceIds,
        DeviceLifecycleStatus from, DeviceLifecycleStatus to,
        CancellationToken ct = default);

    // ── الاسم التجاري ───────────────────────────────────────────────

    /// <summary>
    /// <b>كل</b> أجهزة الشركة — دفعة بعد <paramref name="after"/>.
    ///
    /// <para>⚠️ بالمدموجين كمان زي القديم: صفوفهم بتفضل ظاهرة في البحث
    /// بالكود المتقاعد، والاسم التجاري بيساعد اللي بيدوّر.</para>
    /// </summary>
    Task<IReadOnlyList<DeviceModelRow>> DeviceModelsAsync(
        Guid tenantId, Guid after, int take, CancellationToken ct = default);

    /// <summary>
    /// أدلة الاسم التجاري لكل جهاز من دول — <b>من الأحدث للأقدم</b>،
    /// بنفس ترتيب وفلتر الاستقبال.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<ModelEvidenceRow>>> ModelEvidenceAsync(
        Guid tenantId, IReadOnlyCollection<Guid> deviceIds, CancellationToken ct = default);

    Task<int> SetCommercialModelAsync(
        Guid tenantId, Guid deviceId, string? name, string? source, string? machineType,
        CancellationToken ct = default);
}
