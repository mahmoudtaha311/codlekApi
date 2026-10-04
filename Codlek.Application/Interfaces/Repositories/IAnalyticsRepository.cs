using Codlek.Application.Contracts.Analytics;
using Codlek.Application.Contracts.Reports;
using Codlek.Core.Analytics;

namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// أرقام اللوحة.
///
/// <para>🔴 <b>قاعدة واحدة تحكم الواجهة دي كلها: التجميع في
/// SQL.</b> أي قراية صفوف فحوص قبل التجميع معناها إن اللوحة بتسحب
/// الجدول كله للذاكرة — شغّالة النهاردة على خمس فحوص، وبتقع على
/// عشرة آلاف. كل تجميعة هنا بتنزل SQL كـ<c>GROUP BY</c>، واللي
/// بيترجع هو الصفوف المجمّعة وبس.</para>
///
/// <para>🔴 <b>والتضييق على الفني بيتعمل جوّه كل دالة.</b> الفني
/// بيشوف شغله هو — نفس قاعدة الصفحة الرئيسية. ومفيش فلتر عام
/// بيفرضها، فأول دالة تنساه بتفتح شغل ورشة كامل لفني.</para>
///
/// <para>⚠️ <b>ومفيش حرف عربي في ولا استعلام.</b> النصوص
/// (<c>StatusText</c> · <c>Label</c>) بتتحسب في الـHandler بعد
/// القراية.</para>
/// </summary>
public interface IAnalyticsRepository
{
    /// <summary>
    /// الأرقام الرئيسية + عدّادات الخطوات — <b>تجميعة واحدة</b>.
    /// </summary>
    /// <param name="includeStations">
    /// 🔴 <c>false</c> للفني: عدّاد الراكات على مستوى الشركة. وبيرجع
    /// <c>0</c>، <b>والاستعلام نفسه مابيتنفّذش</b> — مش إخفاء في
    /// الواجهة.
    /// </param>
    Task<(DashboardKpis Kpis, TestCounts Counts)> KpisAsync(
        Guid tenantId, string? technicianCode, AnalyticsPeriod period,
        bool includeStations, CancellationToken ct = default);

    /// <summary>
    /// منحنى الحجم — بالساعة ليوم واحد، وباليوم لأي فترة أطول،
    /// <b>والفجوات مملّية بصفر</b>.
    /// </summary>
    Task<IReadOnlyList<TrendPoint>> TrendAsync(
        Guid tenantId, string? technicianCode, AnalyticsPeriod period,
        CancellationToken ct = default);

    /// <summary>نشاط الفنيين — بالكود، مش بالهوية المركزية.</summary>
    Task<IReadOnlyList<TechnicianActivityItem>> TechnicianActivityAsync(
        Guid tenantId, string? technicianCode, AnalyticsPeriod period, int take,
        CancellationToken ct = default);

    /// <summary>
    /// حِمل الراكات — <b>الراكات كلها، واللي ماشتغلتش بصفر</b>.
    ///
    /// <para>⚠️ بترجّع الحالة كـenum؛ النص العربي بيتحسب في
    /// الـHandler.</para>
    /// </summary>
    Task<IReadOnlyList<RackLoadFacts>> RackActivityAsync(
        Guid tenantId, string? technicianCode, AnalyticsPeriod period,
        CancellationToken ct = default);

    /// <summary>
    /// أكتر خطوات الفحص فشلاً.
    ///
    /// <para>🔴 الحالة <c>٢</c> = «فيه مشكلة». والخطوات اللي ما
    /// اشتغلتش (<c>٥</c>) <b>مش</b> فشل: دي مشكلة في الفحص نفسه مش
    /// في الجهاز، وخلطهم بيخلّي «الشاشة بايظة» و«ماقدرناش نفحص
    /// الشاشة» رقم واحد.</para>
    /// </summary>
    Task<IReadOnlyList<NamedCountItem>> FailureHotspotsAsync(
        Guid tenantId, string? technicianCode, AnalyticsPeriod period, int take,
        CancellationToken ct = default);

    /// <summary>
    /// عدد الفحوص في كل شريحة مدة — <b>بترتيب الشرايح</b>.
    /// </summary>
    Task<IReadOnlyList<int>> DurationBucketsAsync(
        Guid tenantId, string? technicianCode, AnalyticsPeriod period,
        CancellationToken ct = default);

    /// <summary>أجهزة جديدة مقابل إعادة فحص.</summary>
    Task<DeviceMix> DeviceMixAsync(
        Guid tenantId, string? technicianCode, AnalyticsPeriod period,
        CancellationToken ct = default);

    /// <summary>
    /// قايمة «محتاج مراجعة» وقايمة «آخر الفحوص» — <b>في مرة
    /// واحدة</b>.
    ///
    /// <para>⚠️ الاتنين بيتقراوا مع بعض عشان أكواد الراكات والأجهزة
    /// تتجاب باستعلام واحد للاتنين، مش واحد لكل قايمة.</para>
    /// </summary>
    Task<(IReadOnlyList<ReportListItem> Attention, IReadOnlyList<ReportListItem> Recent)>
        ListsAsync(
            Guid tenantId, string? technicianCode, AnalyticsPeriod period, int take,
            CancellationToken ct = default);

    /// <summary>مجموع مدة فحوص الفترة بالمللي ثانية.</summary>
    Task<long> TotalDurationMsAsync(
        Guid tenantId, string? technicianCode, AnalyticsPeriod period,
        CancellationToken ct = default);

    /// <summary>
    /// كام فحص <b>ممسوح</b> في الفترة.
    ///
    /// <para>⚠️ استعلام مستقل لأن كل القرايات التانية بتستبعد
    /// الممسوح أصلاً.</para>
    /// </summary>
    Task<int> DeletedCountAsync(
        Guid tenantId, AnalyticsPeriod period, CancellationToken ct = default);

    /// <summary>القطع المكتوبة على تقارير فحص الفترة.</summary>
    Task<IReadOnlyList<NamedCountItem>> ReportPartsAsync(
        Guid tenantId, string? technicianCode, AnalyticsPeriod period, int take,
        CancellationToken ct = default);

    /// <summary>عدّادات التحذيرات — <b>بلا فترة</b>.</summary>
    Task<AlertsSummary> AlertsAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>جرد الورشة — <b>بلا فترة</b>.</summary>
    Task<InventorySummary> InventoryAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// قطع الغيار اللي اتركّبت في أوامر الفترة.
    ///
    /// <para>⚠️ <b>الفترة بتتقاس على فتح أمر الصيانة</b> مش على وقت
    /// تركيب القطعة — القطعة مالهاش تاريخ خاص بيها في الكيان. يعني
    /// أمر اتفتح الشهر اللي فات واتصلّح النهاردة بيتحسب على الشهر
    /// اللي فات.</para>
    /// </summary>
    Task<IReadOnlyList<FittedPartFacts>> FittedPartsAsync(
        Guid tenantId, AnalyticsPeriod period, CancellationToken ct = default);

    /// <summary>
    /// أسماء القطع المكتوبة على تقارير الفحص في الفترة — <b>خام،
    /// غير مجمّعة</b>.
    ///
    /// <para>⚠️ التجميع بيحصل في الـHandler بعد التطبيع العربي، وده
    /// مالوش ترجمة لـSQL.</para>
    /// </summary>
    Task<IReadOnlyList<string>> NotedPartNamesAsync(
        Guid tenantId, AnalyticsPeriod period, CancellationToken ct = default);
}
