using System.ComponentModel.DataAnnotations;
using Codlek.Core.Enums;

namespace Codlek.Core.Entities;

/// <summary>
/// موقع تشغيلي — مخزن، قسم، نقطة بيع.
///
/// <para>⚠️ <b>مش <see cref="Department"/>.</b> القسم وحدة تنظيمية
/// بتملك ناس (<c>WebUser.DepartmentId</c>)؛ الموقع مكان بيشيل أجهزة.
/// وساعات الاتنين بنفس الاسم — «قسم الصيانة» قسم وكمان مكان — بس ده
/// تشابه أسماء مش نفس الكيان: الجهاز بينتقل لموقع، الموظف بينتمي لقسم.
/// وكمان <c>Technician.DepartmentId</c> مالوش لا مفتاح أجنبي ولا فهرس
/// لحد دلوقتي، فماينفعش نبني عليه حيازة أجهزة.</para>
/// </summary>
public class Location
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    /// <summary>كود قصير للاختصار في القوايم — ممكن يفضل فاضي.</summary>
    [MaxLength(20)]
    public string Code { get; set; } = "";

    [MaxLength(120)]
    public string Name { get; set; } = "";

    public LocationKind Kind { get; set; } = LocationKind.Warehouse;

    /// <summary>
    /// الموقع بيتوقف، مابيتمسحش.
    ///
    /// <para>⚠️ حركات قديمة بتشاور عليه؛ مسحه بيقطع تاريخ.</para>
    /// </summary>
    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// حاوية استيراد — الشحنة اللي اللاب جه فيها.
///
/// <para>🔴 <b>دي مش مكان تخزين.</b> صاحب الشغل وضّحها بالنص:
/// «الحاوية دي اللاب كان جاي من الاستيراد فيها، وبتاخد رمز ومش
/// بتتغير». يعني صفة ثابتة للاب نفسه، مش حالة بتتنقل.</para>
///
/// <para>⚠️ <b>وعشان كده مفيش جدول حركة ولا تاريخ نقل.</b> الربط
/// عمود واحد على <c>Device</c> بيتكتب <b>مرة واحدة</b> — سجل حركة
/// لحاجة ثابتة تعقيد بلا مقابل.</para>
///
/// <para>⚠️ <b>وليه مش <see cref="Location"/>.</b> <c>LocationKind</c>
/// بيوصف خمس محطات في الورشة واللاب بيبقى في واحدة منهم؛ الحاوية
/// صندوق بيتعمل بالمئات. حطّها كصفوف <c>Warehouse</c> كان هيكبّر
/// الجدول بلا حدود ويفقد <c>Kind</c> معناه. الشكل مستعار، مش
/// الصفوف.</para>
/// </summary>
public class ImportContainer
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    /// <summary>
    /// رمز الحاوية زي ما الفني كتبه — <c>CN-2026-014</c>.
    ///
    /// <para>🔴 <b>فريد داخل الشركة</b> بفهرس مفلتر على
    /// <c>[Code] &lt;&gt; ''</c> — نفس نمط الأكواد التانية في المخطّط.
    /// من غير التفرّد، «نفس الحاوية» بتبقى صفّين والمحتوى بينقسم.</para>
    /// </summary>
    [MaxLength(40)]
    public string Code { get; set; } = "";

    /// <summary>
    /// الشكل المطبّع للمقارنة — <c>ArabicText.Normalize</c> + حروف صغيرة.
    ///
    /// <para>⚠️ <b>هو اللي عليه فهرس التفرّد، مش <see cref="Code"/>.</b>
    /// الفني بيكتب <c>cn-2026-014</c> و<c>CN-2026-014</c> و
    /// <c>CN‑2026‑014</c> بمسافات زيادة — من غير التطبيع دول تلات
    /// حاويات.</para>
    /// </summary>
    [MaxLength(40)]
    public string NormalizedCode { get; set; } = "";

    /// <summary>اسم وصفي اختياري — «شحنة مارس».</summary>
    [MaxLength(120)]
    public string Name { get; set; } = "";

    /// <summary>
    /// الحاوية بتتوقف، مابتتمسحش.
    ///
    /// <para>⚠️ فيه لابات بتشاور عليها؛ مسحها بيقطع نسب.</para>
    /// </summary>
    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }

    /// <summary>مين عملها — «مزامنة الراكة» لو اتعملت من فحص.</summary>
    [MaxLength(120)]
    public string CreatedByName { get; set; } = "";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// تعريف عطل معروف — عشان الأعطال تبقى قابلة للاستعلام مش نص حر.
///
/// <para>«السماعة اليمنى مابتشتغلش» مكتوبة بإيد الفني عشرين مرة بعشرين
/// صيغة مابتردش على سؤال «كام لاب فيه عطل سماعة الشهر ده». الكود هنا
/// بيرد.</para>
/// </summary>
public class IssueCatalogItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    /// <summary>كود ثابت — <c>audio.right_speaker_dead</c> مثلاً.</summary>
    [MaxLength(40)]
    public string Code { get; set; } = "";

    [MaxLength(160)]
    public string Title { get; set; } = "";

    /// <summary>المرحلة اللي العطل بيطلع منها — <c>audio</c>، <c>ports</c>.</summary>
    [MaxLength(40)]
    public string Category { get; set; } = "";

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }
}

// =====================================================================

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

/// <summary>
/// عطل مرتبط بأمر صيانة.
///
/// <para>🔴 <b>الكود والعنوان الاتنين متخزّنين هنا بالقيمة.</b> لو
/// خزّنا مفتاح للكتالوج بس، أول ما مدير يعدّل صياغة عطل في الكتالوج
/// هتتغيّر كل أوامر الصيانة القديمة بأثر رجعي — والسجل اللي بيتغيّر
/// ورا ظهرك مش سجل.</para>
/// </summary>
public class RepairWorkItemIssue
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }

    public Guid WorkItemId { get; set; }
    public RepairWorkItem? WorkItem { get; set; }

    [MaxLength(40)]
    public string IssueCode { get; set; } = "";

    /// <summary>لقطة العنوان وقت الفتح.</summary>
    [MaxLength(160)]
    public string IssueTitleSnapshot { get; set; } = "";

    [MaxLength(40)]
    public string Category { get; set; } = "";

    /// <summary>
    /// العطل ده اتصلّح؟
    ///
    /// <para>⚠️ الإجابة هنا بتخصّ أمر الصيانة ده وبس. الفحص الأصلي
    /// اللي اكتشف العطل مابيتعدّلش — يفضل يقول إن العطل كان موجود.</para>
    /// </summary>
    public bool Resolved { get; set; }
}

/// <summary>
/// قطعة اتركّبت في صيانة.
///
/// <para>⚠️ <b>مش <c>ReportPart</c>.</b> ده مربوط بـ<c>ReportId</c>
/// وبيتمسح ويتعمل من الأول في كل مزامنة لفحص اتغيّر — قطعة غيار
/// اتصرفت فعلاً ماينفعش تعيش في صف بيتحرق.</para>
/// </summary>
public class RepairPart
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }

    public Guid WorkItemId { get; set; }
    public RepairWorkItem? WorkItem { get; set; }

    [MaxLength(200)]
    public string Name { get; set; } = "";

    /// <summary>كود المخزون لو الراكة صرفتها من جردها — ممكن يفضل فاضي.</summary>
    [MaxLength(60)]
    public string InventoryCode { get; set; } = "";

    public int Quantity { get; set; } = 1;

    /// <summary>سيريال القطعة الجديدة لو اتقرا — مابيتخترعش.</summary>
    [MaxLength(120)]
    public string SerialNumber { get; set; } = "";

    public string Notes { get; set; } = "";
}

// =====================================================================

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
