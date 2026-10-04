namespace Codlek.Application.Contracts.Repairs;

/// <summary>
/// صف في قايمة أوامر الصيانة.
///
/// <para>🔴 <b>ترتيب الحقول عقد — وفيه فخّين.</b></para>
///
/// <para>الفخ الأول: <c>OpenAgeHours</c> و<c>RepairAgeHours</c>
/// متجاورين، نفس النوع (<c>double?</c>)، والاتنين افتراضيهم
/// <c>null</c>. قلبهم بيتترجم من غير ولا تحذير، والشاشة بتعرض «قعدة
/// الطابور» مكان «تحت إيد فني». أمر اتفتح من أسبوعين وبدأ من ساعة
/// عنده <c>OpenAgeHours = 336</c> و<c>RepairAgeHours = 1</c>.</para>
///
/// <para>الفخ التاني: <c>AssignedTechnicianId</c> وبعديه
/// <c>AssignedTechnicianName</c> — <c>Guid?</c> وبعده <c>string</c>،
/// والمترجم مابيحميش من قلب زوج تاني لو اتزاد.</para>
/// </summary>
public sealed record RepairListItem(
    Guid Id,
    string PublicCode,
    Guid DeviceId,
    string DeviceCode,
    string DeviceName,

    /// <summary>
    /// اسم قيمة الـenum كنص — <c>"WaitingForRepair"</c>.
    ///
    /// <para>⚠️ الواجهة بتفلتر عليه، فالاسم ده عقد زي الرقم.</para>
    /// </summary>
    string Status,

    string StatusText,
    string FaultSummary,
    Guid? AssignedTechnicianId,
    string AssignedTechnicianName,
    DateTime OpenedAtUtc,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,

    /// <summary>
    /// المدة من البدء للإنهاء — <c>null</c> لو لسه ماخلصتش.
    ///
    /// <para>⚠️ <b>دي مش وقت شغل فعلي.</b> اقراها «قد إيه قعد
    /// مفتوح» — الفني بيسيب اللاب ويرجعله.</para>
    /// </summary>
    long? DurationMs,

    string LocationName,
    int IssueCount,
    int PartCount,

    /// <summary>من <b>الفتح</b> لدلوقتي — للمفتوح بس.</summary>
    double? OpenAgeHours = null,

    /// <summary>
    /// من <b>البدء</b> لدلوقتي — للمفتوح بس، و<c>null</c> لو محدش
    /// بدأ.
    /// </summary>
    double? RepairAgeHours = null,

    /// <summary>
    /// قرار المحاسب — <b>محور مستقل عن <see cref="Status"/></b>.
    ///
    /// <para>🔴 مش قيمة جوّه الحالة: أمر «مستني موافقة» ممكن يكون
    /// <c>New</c> أو <c>WaitingForRepair</c>.</para>
    /// </summary>
    int Approval = 0,

    string ApprovalText = "");
