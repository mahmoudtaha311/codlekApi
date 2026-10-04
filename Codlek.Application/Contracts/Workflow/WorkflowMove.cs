using Codlek.Core.Enums;

namespace Codlek.Application.Contracts.Workflow;

/// <summary>
/// حركة جهاز واحدة — <b>اللي المنادي عايز يحصل</b>.
///
/// <para>⚠️ <b>الحقول الاختيارية معناها «ماتلمسش» مش «فضّي».</b>
/// <c>ToStage = null</c> معناها «المرحلة زي ما هي»، و
/// <c>ToTechnicianId = null</c> معناها «الحائز زي ما هو». واللي بيفضّي
/// الحائز هو <see cref="ClearsHolder"/> بالاسم.</para>
/// </summary>
public sealed record WorkflowMove
{
    public required Guid DeviceId { get; init; }

    public required DeviceWorkflowEventType EventType { get; init; }

    public required WorkflowActor Actor { get; init; }

    /// <summary>⚠️ <c>null</c> = المرحلة زي ما هي.</summary>
    public DeviceOperationalStage? ToStage { get; init; }

    /// <summary>⚠️ <c>null</c> = الحائز زي ما هو. والتفضية بـ<see cref="ClearsHolder"/>.</summary>
    public Guid? ToTechnicianId { get; init; }

    public Guid? ToLocationId { get; init; }

    /// <summary>
    /// 🔴 <b>فضّي الحائز.</b>
    ///
    /// <para>ودي بتسبق <see cref="ToTechnicianId"/>: لو الاتنين
    /// مبعوتين، الحائز بيتفضّى. والسبب إن «اللاب رجع الرف» حركة
    /// حقيقية مختلفة عن «اللاب انتقل لفني تاني».</para>
    /// </summary>
    public bool ClearsHolder { get; init; }

    public Guid? RepairWorkItemId { get; init; }

    public Guid? BaselineReportId { get; init; }

    public bool BaselineIsFresh { get; init; }

    /// <summary>
    /// ⚠️ <b>وقت الحركة ممكن يبقى أقدم من وقت التسجيل بأيام</b> —
    /// راكة اشتغلت أوفلاين. <c>null</c> = دلوقتي.
    /// </summary>
    public DateTime? OccurredAtUtc { get; init; }

    /// <summary>
    /// معرّف الحركة من الراكة — <b>لمنع التكرار</b>.
    ///
    /// <para>🔴 إعادة رفع نفس الحركة بعد انقطاع <b>مش</b> حركة جديدة.
    /// ومن غير المعرّف ده، كل إعادة محاولة بتكتب سطر تاني في سجل
    /// الحركة.</para>
    /// </summary>
    public Guid? EventId { get; init; }

    public string Reason { get; init; } = "";

    public string Notes { get; init; } = "";

    public string ReceivedByName { get; init; } = "";
}
