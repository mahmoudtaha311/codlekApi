namespace Codlek.Application.Contracts.Sync;

/// <summary>
/// أمر صيانة جايّ من الراكة.
///
/// <para>🔴 <b>والحقول دي الراكة بتكتبها حرف بحرف</b> — اسم مختلف
/// بيتقرا قيمته الافتراضية في صمت.</para>
///
/// <para>⚠️ <b>ومفيش <c>PublicCode</c> هنا عن قصد.</b> رقم الأمر اللي
/// الناس بتتكلّم بيه وبيتكتب في ورقة لازم يبقى من مصدر واحد — السيرفر.
/// راكتين أوفلاين بيوزّعوا أرقام لوحدهم = رقمين متكررين في نفس
/// الشركة. ومفيش الموافقة كمان: السيرفر بيملكها زي الإسناد.</para>
/// </summary>
public sealed class RepairWorkItemSyncPayload
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }

    public Guid? SourceReportId { get; set; }
    public Guid? RetestReportId { get; set; }

    public int Status { get; set; }
    public int RequiredSpecialty { get; set; }

    public Guid? AssignedTechnicianId { get; set; }
    public Guid? CompletedByTechnicianId { get; set; }
    public Guid? OpenedByTechnicianId { get; set; }

    public string? OpenedByName { get; set; }

    public DateTime OpenedAtUtc { get; set; }
    public DateTime? ClaimedAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    public string? FaultSummary { get; set; }
    public string? RepairActions { get; set; }
    public string? Notes { get; set; }
    public string? OutcomeReason { get; set; }

    public List<RepairIssueSyncPayload>? Issues { get; set; }
    public List<RepairPartSyncPayload>? Parts { get; set; }
}

/// <summary>عطل على أمر صيانة — <b>معرّفه من الراكة وبيعيش</b>.</summary>
public sealed class RepairIssueSyncPayload
{
    public Guid Id { get; set; }
    public string? IssueCode { get; set; }
    public string? IssueTitleSnapshot { get; set; }
    public string? Category { get; set; }
    public bool Resolved { get; set; }
}

/// <summary>قطعة اتركّبت — <b>معرّفها من الراكة وبيعيش</b>.</summary>
public sealed class RepairPartSyncPayload
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? InventoryCode { get; set; }
    public int Quantity { get; set; }
    public string? SerialNumber { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// حركة جهاز جاية من الراكة.
///
/// <para>🔴 <b>و<c>EventId</c> هو مفتاح عدم التكرار.</b> الراكة بتعيد
/// الرفع لما الرد يتأخر، والانقطاع بعد ما السيرفر ثبّت بيخلّيها تعيد
/// نفس الحمولة — فالحركة بتتطابق بمعرّف حدثها، والإعادة بترجّع نفس
/// الصف مش صف تاني. من غير كده، «سلّمت الجهاز» واحدة كانت بتتسجّل
/// مرتين.</para>
/// </summary>
public sealed class DeviceWorkflowEventSyncPayload
{
    public Guid EventId { get; set; }
    public Guid DeviceId { get; set; }

    public int EventType { get; set; }

    public int? ToStage { get; set; }

    public Guid? ToTechnicianId { get; set; }
    public Guid? ToLocationId { get; set; }
    public bool ClearsHolder { get; set; }

    /// <summary>
    /// ⚠️ <b>مفيش مفتاح أجنبي على ده عن قصد</b> — حركة وصلت قبل أمرها
    /// بتتقبل والرابط بيتحل لما الأمر يوصل.
    /// </summary>
    public Guid? RepairWorkItemId { get; set; }

    public Guid? BaselineReportId { get; set; }
    public bool BaselineIsFresh { get; set; }

    public Guid? ActorTechnicianId { get; set; }
    public string? ActorName { get; set; }

    public DateTime OccurredAtUtc { get; set; }

    public string? Reason { get; set; }
    public string? Notes { get; set; }
}
