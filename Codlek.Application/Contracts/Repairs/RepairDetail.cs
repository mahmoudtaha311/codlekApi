namespace Codlek.Application.Contracts.Repairs;

/// <summary>
/// أمر صيانة بكل تفاصيله.
///
/// <para>🔴 <b>٣٨ حقل بالترتيب (معدودين من رد حقيقي)، وفيه أربع مناطق خطر:</b></para>
/// <list type="number">
///   <item>زوجين فني متجاورين:
///         <c>AssignedTechnicianId · AssignedTechnicianName ·
///         CompletedByTechnicianId · CompletedByTechnicianName</c> —
///         قلب أي زوج بينسب الصيانة لفني غلط.</item>
///   <item>أربع نصوص ورا بعض: <c>FaultSummary · RepairActions ·
///         Notes · OutcomeReason</c>.</item>
///   <item>تلات نصوص موافقة ورا بعض: <c>ApprovalText ·
///         ApprovedByName · ApprovalNote</c>.</item>
///   <item>تلات <c>bool</c> افتراضيهم <c>false</c>:
///         <c>BrandOverride · StartedWithoutApproval</c> و
///         <c>TechnicianOutsideBrand</c>، ومابينهم <c>string
///         Brand</c> بس.</item>
/// </list>
/// </summary>
public sealed record RepairDetail(
    Guid Id,
    string PublicCode,
    Guid DeviceId,
    string DeviceCode,
    string DeviceName,
    string DeviceRawModel,
    Guid? SourceReportId,

    /// <summary>
    /// فحص ما بعد الصيانة لو اتعمل.
    ///
    /// <para>⚠️ ممكن يبقى موجود والفحص نفسه لسه ماوصلش السيرفر —
    /// الراكة بتولّد المعرّف قبل ما الفحص يبدأ، ومفيش مفتاح أجنبي
    /// عليه عن قصد.</para>
    /// </summary>
    Guid? RetestReportId,

    string Status,
    string StatusText,
    int RequiredSpecialty,
    string RequiredSpecialtyText,

    Guid? AssignedTechnicianId,
    string AssignedTechnicianName,
    Guid? CompletedByTechnicianId,
    string CompletedByTechnicianName,
    string OpenedByName,

    DateTime OpenedAtUtc,
    DateTime? ClaimedAtUtc,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    long? DurationMs,

    /// <summary>تشخيص الفني — <b>منفصل عن نتايج الفحص</b>.</summary>
    string FaultSummary,

    string RepairActions,
    string Notes,
    string OutcomeReason,

    IReadOnlyList<RepairIssueItem> Issues,
    IReadOnlyList<RepairPartItem> Parts,
    IReadOnlyList<RepairWorkflowItem> Workflow,

    /// <summary>قرار المحاسب — محور مستقل عن الحالة.</summary>
    int Approval = 0,

    string ApprovalText = "",
    string ApprovedByName = "",
    DateTime? ApprovalDecidedAtUtc = null,
    string ApprovalNote = "",

    /// <summary>
    /// المحاسب عدّى قاعدة «الفني ده لماركات معيّنة».
    ///
    /// <para>🔴 <b>التعدية لازم تبان على الصف نفسه.</b> تعدية
    /// مابتتعرضش معناها إن القاعدة مالهاش أي معنى.</para>
    /// </summary>
    bool BrandOverride = false,

    /// <summary>راكة اشتغلت على الأمر وهو لسه مستني موافقة.</summary>
    bool StartedWithoutApproval = false,

    /// <summary>
    /// ماركة اللاب بعد التوحيد — <b>فاضية معناها مش في
    /// القايمة</b>.
    ///
    /// <para>⚠️ وساعتها القيد مابيتطبّقش على اللاب ده خالص. الشاشة
    /// لازم تقول كده صريح، مش تسيب الفراغ يتقرا «مفيش ماركة».</para>
    /// </summary>
    string Brand = "",

    /// <summary>
    /// الفني المتسند برّه ماركاته؟ — <b>بيتحسب وقت القراية</b>.
    ///
    /// <para>⚠️ مش متخزّن: القواعد بتتغيّر، والسؤال «هل ده مخالف
    /// <b>دلوقتي</b>» مش «هل كان مخالف وقت الفتح».</para>
    /// </summary>
    bool TechnicianOutsideBrand = false);
