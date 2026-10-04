using Codlek.Core.Enums;

namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// أعداد مصادر خط الزمن — <b>من نفس الاستعلامات اللي الصفوف بتتقرا
/// منها</b>.
///
/// <para>🔴 <b>والعدّ مش تزيّد.</b> هو اللي بيحسب عدد الصفحات
/// ويقرّر إمتى الطلب يترد لآخر صفحة — فرقم ناقص معناه صفحة أخيرة
/// فيها أحداث ومحدّش يقدر يوصلها.</para>
///
/// <para>⚠️ <b>ولازم ييجي من <u>نفس</u> الاتحاد اللي الصفوف بتتقرا
/// منه.</b> عدّ من استعلام تاني (ولو بنفس الفلتر على الورق) بيخلّي
/// الرقم والصفوف يختلفوا أول ما فلتر يتعدّل في واحد منهم.</para>
/// </summary>
public sealed record DeviceTimelineCounts(
    int Reports,
    int Notes,
    int RepairMoments,
    int Movements)
{
    /// <summary>
    /// الإجمالي — <b>زائد واحد لحدث الاكتشاف</b>.
    ///
    /// <para>⚠️ حدث الاكتشاف مش صف في جدول: هو مسقط من صف اللاب
    /// نفسه، فمفيش عدّاد بيجيبه ولازم يتزاد بالإيد.</para>
    /// </summary>
    public int Total => Reports + Notes + RepairMoments + Movements + 1;
}

/// <summary>صف فحص على خط الزمن — خام.</summary>
public sealed record TimelineReportRow(
    Guid ReportId,
    DateTime StartedAtUtc,
    DateTime? EndedAtUtc,
    long DurationMs,
    string TechnicianName,
    string TechnicianCode,
    int PassCount,
    int FailCount,
    int ErrorCount,
    int NotPresentCount,
    int SkipCount);

/// <summary>صف ملاحظة على خط الزمن — خام.</summary>
public sealed record TimelineNoteRow(
    long Id,
    DateTime CreatedAtUtc,
    string CreatedByName,
    string Body);

/// <summary>
/// لحظة واحدة من أمر صيانة — <b>معرّف الأمر ووقتها ونوعها</b>.
///
/// <para>⚠️ <b>اللحظة مش صف.</b> أمر الصيانة صف واحد بأربع أعمدة
/// وقت (اتفتح، بدأ، خلص، آخر تعديل)، وخط الزمن محتاج يعرض كل واحد
/// فيهم كسطر مستقل في مكانه الزمني.</para>
/// </summary>
public sealed record TimelineRepairMomentRow(
    Guid RepairId,
    DateTime AtUtc,
    RepairMoment Moment,
    string PublicCode,
    string FaultSummary,
    string RepairActions,
    string OutcomeReason,
    int PartCount,
    string OpenedByActorType,
    string OpenedByName,
    Guid? AssignedTechnicianId,
    Guid? CompletedByTechnicianId);

/// <summary>
/// لحظة في عمر أمر صيانة.
///
/// <para>⚠️ <b>و«تعذّر» منفصل عن «تمت» عن قصد.</b> الاتنين بيقفلوا
/// الأمر في نفس العمود، بس واحد معناه اللاب بقى جاهز والتاني معناه
/// لسه فيه عطل.</para>
/// </summary>
public enum RepairMoment
{
    Opened = 0,
    Started = 1,
    Completed = 2,
    Unable = 3,
    Cancelled = 4,
}

/// <summary>
/// حركة تشغيلية على خط الزمن — خام.
///
/// <para>⚠️ <b>بترجّع الطرفين (من/لـ) كاملين</b> حتى لو مااتغيّروش:
/// المقارنة بتحصل في المعالج، لأن الحركة بتتخزّن بأطرافها كلها —
/// وتسليم الحيازة لنفس الفني بيتخزّن وفيه <c>From == To</c>.</para>
/// </summary>
public sealed record TimelineMovementRow(
    long Id,
    DeviceWorkflowEventType EventType,
    DateTime OccurredAtUtc,
    DateTime RecordedAtUtc,
    DeviceOperationalStage FromStage,
    DeviceOperationalStage ToStage,
    Guid? FromLocationId,
    Guid? ToLocationId,
    Guid? FromTechnicianId,
    Guid? ToTechnicianId,
    string Reason,
    string ActorType,

    /// <summary>
    /// ⚠️ <b>ومفيش كود فاعل على الحركة.</b> الصف شايل الاسم ونوع
    /// الفاعل وبس — مفيش عمود كود. فخانة <c>actorCode</c> في العقد
    /// بتفضل فاضية للحركات، وده سلوك القديم بالحرف.
    /// </summary>
    string ActorName);
