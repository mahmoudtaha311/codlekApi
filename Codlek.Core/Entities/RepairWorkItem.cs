using System.ComponentModel.DataAnnotations;
using Codlek.Core.Enums;

namespace Codlek.Core.Entities;

/// <summary>
/// أمر صيانة.
///
/// <para>🔴 <b>ده مش فحص تاني.</b> الفحص بيقول «إيه اللي فيه»؛ أمر
/// الصيانة بيقول «مين بيصلّحه، وصلّح إيه». خلطهم بيخلّي كل تصليحة
/// تتحسب فحص في الإحصائيات، وبيخلّي نتيجة الفحص الأصلية تتغيّر بأثر
/// رجعي لما الصيانة تنجح — وده بيمسح الدليل.</para>
///
/// <para>⚠️ <b>ونتايج الفحص الأصلية مابتتلمسش من هنا خالص.</b> لو
/// الصيانة صلّحت السماعة، الفحص القديم يفضل يقول إنها كانت بايظة —
/// ده اللي حصل. الإثبات إنها اتصلّحت بيبقى فحص جديد.</para>
/// </summary>
public class RepairWorkItem
{
    /// <summary>
    /// معرّف بيتولّد عند المنشأ.
    ///
    /// <para>⚠️ <c>Guid</c> مش <c>int</c> عن قصد: الأمر ده بيتفتح على
    /// الراكة وهي أوفلاين، فلازم يبقى ليه معرّف نهائي قبل ما يشوف
    /// السيرفر — زي الجهاز والفحص بالظبط.</para>
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    /// <summary>
    /// رقم الأمر اللي الناس بتتكلم بيه — <c>RP-00000001</c>.
    ///
    /// <para>بيتوزّع من <see cref="TenantCounter"/> زي كود الجهاز.
    /// بيفضل فاضي لحد ما السيرفر يوزّعه، فالراكة الأوفلاين مابتخترعش
    /// أرقام ممكن تتصادم.</para>
    /// </summary>
    [MaxLength(20)]
    public string PublicCode { get; set; } = "";

    public Guid DeviceId { get; set; }
    public Device? Device { get; set; }

    /// <summary>
    /// الفحص اللي الأمر ده اتفتح منه — لو اتفتح من فحص.
    ///
    /// <para>⚠️ بيشاور على <c>Report</c> نفسه، <b>مش</b> على
    /// <c>Step</c>. خطوات الفحص بتتمسح وتتعمل من الأول في كل مزامنة
    /// لفحص اتغيّر، فمفتاحها بيتحرق — أي ربط بيها بيبقى مكسور بعد أول
    /// إعادة رفع.</para>
    /// </summary>
    public Guid? SourceReportId { get; set; }
    public Report? SourceReport { get; set; }

    /// <summary>
    /// الفحص اللي اتعمل <b>بعد</b> الصيانة عشان يتأكد إنها نفعت.
    ///
    /// <para>🔴 <b>رابط صريح، مش استنتاج من التوقيت.</b> «أحدث فحص بعد
    /// تاريخ الإنهاء» بيكسر أول ما جهاز يتفحص مرتين في نفس اليوم، أو
    /// يترفع بترتيب مختلف. المعرّف بيتولّد على الراكة قبل ما أي حاجة
    /// تتزامن، فالربط بيفضل صحيح مهما كان ترتيب الوصول.</para>
    ///
    /// <para>⚠️ <b>ومفيش مفتاح أجنبي عليه عن قصد</b> — زي
    /// <c>DeviceWorkflowEvent.RepairWorkItemId</c> بالظبط. الفني بيبدأ
    /// إعادة الفحص والنت مقطوع، فأمر الصيانة ممكن يوصل السيرفر قبل
    /// الفحص بساعات. مفتاح أجنبي هنا كان هيرفض الأمر لحد ما الفحص
    /// يوصل — يعني شغل سليم بيترفض عشان ترتيب.</para>
    /// </summary>
    public Guid? RetestReportId { get; set; }

    public RepairStatus Status { get; set; } = RepairStatus.New;

    /// <summary>
    /// التخصص المطلوب — بيحدد مين يشوف الأمر وهو لسه <c>New</c>.
    ///
    /// <para>فاضي = معروض على كل فني صيانة.</para>
    /// </summary>
    public TechnicianSpecialty RequiredSpecialty { get; set; } = TechnicianSpecialty.None;

    // ── الناس ────────────────────────────────────────────────────────
    //
    // ⚠️ كل واحد منهم **فني** (`Technician`) مش مستخدم موقع
    // (`WebUser`). الاتنين جدولين مختلفين عن قصد، ومحدش بيربطهم غير
    // كود من ٦ أرقام مالوش ضمان تفرّد بينهم. الإنتاجية بتتحسب على
    // `TechnicianId` — الكود والاسم لقطة وقت الحدث وبس.

    /// <summary>الفني اللي استلم الأمر — فاضي وهو لسه معروض.</summary>
    public Guid? AssignedTechnicianId { get; set; }
    public Technician? AssignedTechnician { get; set; }

    /// <summary>الفني اللي قفل الأمر — عادةً نفس اللي استلمه.</summary>
    public Guid? CompletedByTechnicianId { get; set; }

    /// <summary>
    /// مين فتح الأمر.
    ///
    /// <para>ممكن يبقى فني (فتحه من الراكة بعد فحص) أو مستخدم موقع
    /// (مدير فتحه من الصفحة). عشان كده نوع + معرّف زي
    /// <see cref="AuditEvent"/> بالظبط، مش عمود واحد بيفترض نوع.</para>
    /// </summary>
    [MaxLength(20)]
    public string OpenedByActorType { get; set; } = "";

    public Guid? OpenedByTechnicianId { get; set; }
    public Guid? OpenedByUserId { get; set; }

    /// <summary>اسم اللي فتح الأمر وقت ما فتحه — لقطة، مش مرجع حي.</summary>
    [MaxLength(120)]
    public string OpenedByName { get; set; } = "";

    // ── الوقت ────────────────────────────────────────────────────────

    public DateTime OpenedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>لحظة الاستلام — <c>New</c> ← <c>WaitingForRepair</c>.</summary>
    public DateTime? ClaimedAtUtc { get; set; }

    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    /// <summary>
    /// آخر لمسة على الصف ده — <b>مؤشّر التغذية النازلة</b>.
    ///
    /// <para>🔴 <b>مابيتكتبش بالإيد في أي مكان.</b> بيتختم في
    /// <c>AppDbContext.SaveChanges</c> لكل صف مضاف أو متعدّل. مكان
    /// واحد بينسى يحدّثه معناه إسناد بيتغيّر على الموقع ومايوصلش
    /// راكة الفني <b>أبداً</b> — والراكة مش هتعرف إنها فوّتته، لأن
    /// السحب التراكمي بيسأل عن اللي بعد آخر وقت شافه.</para>
    ///
    /// <para>⚠️ والصفوف القديمة بتاخد <c>OpenedAtUtc</c> في الهجرة
    /// مش وقت الهجرة نفسه: لو أخدوا وقت الهجرة كلهم، أول سحب من أي
    /// راكة كان هيجيبهم كلهم مرة واحدة بترتيب ملوش معنى.</para>
    /// </summary>
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    // ── المحتوى ──────────────────────────────────────────────────────

    /// <summary>ملخّص الأعطال بالنص — جنب الأكواد المنظّمة تحت، مش بدلها.</summary>
    public string FaultSummary { get; set; } = "";

    /// <summary>اللي اتعمل فعلاً.</summary>
    public string RepairActions { get; set; } = "";

    public string Notes { get; set; } = "";

    /// <summary>سبب «تعذر الإصلاح» أو «ملغاة» — مطلوب في الحالتين دول.</summary>
    [MaxLength(400)]
    public string OutcomeReason { get; set; } = "";

    public string SearchText { get; set; } = "";

    // ── الموافقة ─────────────────────────────────────────────────────

    /// <summary>
    /// موافقة المحاسب.
    ///
    /// <para>🔴 <b>الأوامر اللي كانت موجودة قبل الميزة دي بتتحوّل
    /// لـ<c>Approved</c> في الهجرة نفسها.</b> لو اتسابوا على
    /// <c>Pending</c>، كل أمر صيانة مفتوح في الورشة كان هيتجمّد في
    /// لحظة النشر.</para>
    /// </summary>
    public RepairApproval Approval { get; set; } = RepairApproval.Pending;

    /// <summary>مين قرّر — مستخدم موقع، مش فني راكة.</summary>
    public Guid? ApprovedByUserId { get; set; }

    /// <summary>
    /// اسم اللي قرّر — <b>لقطة</b>.
    ///
    /// <para>⚠️ نفس سبب <c>AssignedTechnicianName</c>: الاسم بيتعرض
    /// على الراكة وهي أوفلاين، ومحدش هناك يقدر يسأل عن مستخدم موقع.
    /// وكمان القرار سجل — لو الحساب اتمسح بعدين، «مين وافق» بيفضل
    /// مقروء.</para>
    /// </summary>
    [MaxLength(120)]
    public string ApprovedByName { get; set; } = "";

    public DateTime? ApprovalDecidedAtUtc { get; set; }

    /// <summary>
    /// ملاحظة القرار — <b>إجبارية مع الرفض</b>.
    ///
    /// <para>⚠️ نفس قاعدة <c>RequestStore.Reject</c>: الفني هيشوف
    /// السبب، ورفض من غير سبب بيخلّيه يبعت تاني بنفس الطلب.</para>
    /// </summary>
    [MaxLength(400)]
    public string ApprovalNote { get; set; } = "";

    /// <summary>
    /// المحاسب عدّى قاعدة ماركات الفني.
    ///
    /// <para>🔴 <b>بيتسجّل عشان التعدية تفضل مقروءة.</b> القاعدة
    /// بتمنع الفني، والمحاسب بيقدر يعدّيها — وتعدية مابتتسجّلش معناها
    /// إن القاعدة مالهاش أي معنى.</para>
    /// </summary>
    public bool BrandOverride { get; set; }

    /// <summary>
    /// راكة اشتغلت على الأمر ده وهو لسه مستني موافقة.
    ///
    /// <para>🔴 <b>ليه الحالة دي ممكنة أصلاً.</b> الراكات في الميدان
    /// بتشتغل بنسخ قديمة مش عارفة الموافقة، وبتشتغل <b>أوفلاين</b>.
    /// فني على راكة قديمة بيبدأ عادي، والشغل بيوصل السيرفر بعد ما
    /// يخلص.</para>
    ///
    /// <para>🔴 <b>وليه بنقبل بدل ما نرفض.</b> الرفض في طابور الرفع
    /// <b>نهائي</b> (<c>Outbox.Fail(ValidationError)</c>) — يعني
    /// الشغل اللي اتعمل فعلاً على البنش بيتمسح، والسجل الوحيد اللي
    /// بيقول إن حد فتح اللاب بيضيع. اللاب خلاص اتفك، ورفض الورقة
    /// مابيرجّعهوش. فبنسجّل الحقيقة ونعلّمها.</para>
    ///
    /// <para>⚠️ <b>والعلم ده مش عقوبة ولا حالة.</b> الموافقة بتفضل
    /// <c>Pending</c> زي ما هي — المحاسب لسه بيقرّر، بس بيقرّر وهو
    /// عارف إن الشغل اتعمل خلاص.</para>
    /// </summary>
    public bool StartedWithoutApproval { get; set; }

    public ICollection<RepairWorkItemIssue> Issues { get; set; } = new List<RepairWorkItemIssue>();
    public ICollection<RepairPart> Parts { get; set; } = new List<RepairPart>();
}
