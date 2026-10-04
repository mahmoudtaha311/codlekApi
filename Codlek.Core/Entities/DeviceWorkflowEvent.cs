using System.ComponentModel.DataAnnotations;
using Codlek.Core.Enums;

namespace Codlek.Core.Entities;

/// <summary>
/// السجل التشغيلي — <b>الحقيقة الوحيدة عن مكان الجهاز وحائزه</b>.
///
/// <para>🔴 <b>إضافة بس.</b> مفيش تعديل ومفيش مسح. الصف الغلط بيتصحّح
/// بصف جديد بيقول إنه تصحيح، والاتنين بيفضلوا ظاهرين — لأن «مين قال
/// إنه استلمه وطلع غلط» جزء من الإجابة مش ضوضاء.</para>
///
/// <para>🔴 <b>وسجل واحد للحيازة والموقع مع بعض.</b> الوجهة ممكن تبقى
/// شخص أو مكان أو الاتنين. جدولين كانوا هيقدروا يختلفوا على مكان نفس
/// اللاب، وساعتها الميزة اللي اتعملت عشان تجاوب على «اللاب فين» بتبقى
/// هي نفسها مصدر السؤال.</para>
///
/// <para>⚠️ <b>وكاش المرحلة على <c>Device</c> مابيتكتبش من غير صف
/// هنا في نفس المعاملة.</b> لو اتكتب لوحده، الكاش بيبقى ادعاء مالوش
/// سند.</para>
/// </summary>
public class DeviceWorkflowEvent
{
    public long Id { get; set; }

    /// <summary>
    /// معرّف الحدث من المنشأ — لمنع التكرار في المزامنة.
    ///
    /// <para>الراكة بترفع نفس الحدث تاني بعد انقطاع؛ ده بيخلّي السيرفر
    /// يعرف إنه هو هو. نفس نمط <see cref="AuditEvent.EventId"/>.</para>
    /// </summary>
    public Guid? EventId { get; set; }

    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public Guid DeviceId { get; set; }
    public Device? Device { get; set; }

    public DeviceWorkflowEventType EventType { get; set; }

    // ── المرحلة قبل وبعد ─────────────────────────────────────────────
    //
    // الاتنين متخزّنين عشان السطر يفسّر نفسه من غير ما حد يقرا اللي
    // قبله. إعادة بناء المرحلة من أول السجل كل مرة استعلام بطيء وهشّ.

    public DeviceOperationalStage FromStage { get; set; } = DeviceOperationalStage.Unknown;
    public DeviceOperationalStage ToStage { get; set; } = DeviceOperationalStage.Unknown;

    // ── الوجهة: شخص و/أو مكان ────────────────────────────────────────

    public Guid? FromTechnicianId { get; set; }
    public Guid? ToTechnicianId { get; set; }

    public Guid? FromLocationId { get; set; }
    public Guid? ToLocationId { get; set; }

    // ── المنفّذ ──────────────────────────────────────────────────────
    //
    // 🔴 نوع + معرّف، زي `AuditEvent` بالظبط. الحركة ممكن يعملها فني
    // من الراكة أو مدير من الموقع، والاتنين جدولين مختلفين مالهمش
    // مفتاح مشترك موثوق. عمود واحد كان هيجبرنا نختار واحد ونضيّع
    // التاني.

    [MaxLength(20)]
    public string ActorType { get; set; } = "";

    public Guid? ActorUserId { get; set; }
    public Guid? ActorTechnicianId { get; set; }

    /// <summary>اسم المنفّذ وقت الحركة — لقطة.</summary>
    [MaxLength(120)]
    public string ActorName { get; set; } = "";

    // ── الوقت ────────────────────────────────────────────────────────

    /// <summary>وقت الحركة الحقيقي — من الراكة لو اتعملت أوفلاين.</summary>
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>وقت وصولها للسيرفر — بيفرق عن اللي فوق في الأوفلاين.</summary>
    public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;

    // ── روابط ────────────────────────────────────────────────────────

    public Guid? RepairWorkItemId { get; set; }

    /// <summary>
    /// الشخص اللي <b>استلم</b> اللاب في التسليم.
    ///
    /// <para>🔴 <b>خانة مستقلة، مش نص حر في السبب.</b>
    /// «هاني سلّم لمين الشهر ده؟» سؤال لازم يبقى ليه إجابة — لو
    /// الاسم اتحط جوّه <c>Reason</c> السؤال ده مالوش إجابة غير
    /// بالقراءة بالعين.</para>
    ///
    /// <para>⚠️ فاضي في كل الأحداث اللي مش تسليم.</para>
    /// </summary>
    [MaxLength(120)]
    public string ReceivedByName { get; set; } = "";

    /// <summary>
    /// لقطة العتاد وقت الخروج — أساس المقارنة لما الجهاز يرجع.
    ///
    /// <para>🔴 بتتاخد <b>قبل الخروج مباشرةً</b>، مش «آخر فحص موجود».
    /// من غير كده المقارنة بتتهم قطعة اتغيّرت من أسبوعين إنها اتغيّرت
    /// عند العميل.</para>
    /// </summary>
    public Guid? BaselineReportId { get; set; }

    /// <summary>
    /// اللقطة دي اتاخدت وقت الخروج فعلاً؟
    ///
    /// <para>⚠️ <c>false</c> معناها إن اللقطة ماتاخدتش ومدير أذن
    /// بالخروج من غيرها، والنظام رجع لآخر فحص سابق. الفرق ده لازم
    /// يفضل ظاهر في شاشة المقارنة — مقارنة بأساس قديم مقارنة أضعف،
    /// وإخفاء ده بيحوّلها لاتهام.</para>
    /// </summary>
    public bool BaselineIsFresh { get; set; }

    [MaxLength(400)]
    public string Reason { get; set; } = "";

    public string Notes { get; set; } = "";
}
