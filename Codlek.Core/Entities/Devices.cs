using System.ComponentModel.DataAnnotations;
using Codlek.Core.Enums;

namespace Codlek.Core.Entities;

// =====================================================================
//  هوية الجهاز (اللاب)
// =====================================================================

/// <summary>
/// اللاب نفسه — الحاجة اللي بتعيش عبر كل الفحوصات.
///
/// <para><b>مفيش مواصفات هنا.</b> لا معالج ولا رام ولا هارد. دي «إيه كان
/// جوّاه يومها» ومكانها لقطة الهاردوير. لو بقت أعمدة هنا، الموديل بيتحوّل
/// في صمت لـ«آخر كتابة تكسب» والتاريخ بيضيع.</para>
/// </summary>
public class Device
{
    /// <summary>بيتولّد على الراكة عشان يشتغل أوفلاين.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }

    /// <summary>LP-00018425. فاضي معناه الراكة كانت أوفلاين وبلوكها خلص.</summary>
    [MaxLength(20)]
    public string PublicCode { get; set; } = "";

    /// <summary>
    /// حالة الكود: 0 مستنّي · 1 مبدئي من بلوك الراكة · 2 مؤكّد من السيرفر.
    ///
    /// <para>الراكة بتدّي الكود وهي أوفلاين من بلوك محجوز ليها، فالسيرفر
    /// بيأكّده وبس — عادةً <c>no-op</c> لأن الرقم كان محجوز ليها أصلاً.</para>
    /// </summary>
    public int CodeState { get; set; }

    public DeviceIdentityConfidence Confidence { get; set; } = DeviceIdentityConfidence.None;

    [MaxLength(200)]
    public string IdentityBasis { get; set; } = "";

    /// <summary>أول فني شاف الجهاز ده — للمراجعة.</summary>
    [MaxLength(20)]
    public string FirstSeenByTechnicianCode { get; set; } = "";

    /// <summary>الراكة اللي عرّفت الجهاز ده أول مرة.</summary>
    public Guid? FirstSeenByRackId { get; set; }

    public List<Report> Reports { get; set; } = new();

    public DeviceLifecycleStatus Status { get; set; } = DeviceLifecycleStatus.Active;

    public Guid? MergedIntoDeviceId { get; set; }

    public DateTime FirstSeenAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAtUtc { get; set; } = DateTime.UtcNow;

    // ===========================================================
    //  الاسم التجاري — للعرض بس
    // ===========================================================
    //
    // 🔴 <b>مش مرساة هوية.</b> الأسماء دي بتتغيّر وبتتصلّح وبتتكتب
    // بالإيد؛ المطابقة بتفضل على UUID/BIOS/Board/الهارد وبس.
    //
    // ⚠️ <b>وبتتملى من دليل متخزّن بس.</b> P0 حطّ الاسم التجاري على
    // الفحص (Report) ومحطّهوش على الجهاز، فصفحة الأجهزة فضلت تعرض
    // الكود الخام (LENOVO 81FK) رغم إن الفحص نفسه شايل
    // «ideapad 330-15ICH». الحقول دي بتقفل الفجوة دي.

    /// <summary>الاسم اللي الناس بتعرف بيه اللاب، أو <c>null</c> لو مفيش دليل.</summary>
    [MaxLength(160)] public string? CommercialModelName { get; set; }

    /// <summary>منين جه الاسم — للمراجعة وقت الشك.</summary>
    [MaxLength(60)] public string? CommercialModelSource { get; set; }

    /// <summary>كود المصنع (<c>82B5</c>) لما الخام يبقى كود مش اسم.</summary>
    [MaxLength(40)] public string? MachineType { get; set; }

    /// <summary>للعرض والبحث بس — الاسم فيه LastKnown عشان محدش يطابق عليه.</summary>
    [MaxLength(80)]
    public string LastKnownManufacturer { get; set; } = "";

    [MaxLength(120)]
    public string LastKnownModel { get; set; } = "";

    /// <summary>نص البحث المطبَّع — نفس منطق <c>Report.SearchText</c>.</summary>
    public string SearchText { get; set; } = "";

    // ── كاش الحالة التشغيلية ─────────────────────────────────────────
    //
    // 🔴 **دي نسخة للسرعة، مش الحقيقة.** الحقيقة في
    // `DeviceWorkflowEvent`. الأعمدة دي موجودة عشان قايمة فيها ٥٠٠
    // جهاز ماتعملش استعلام على السجل لكل صف.
    //
    // ⚠️ **وماتتكتبش من غير صف في السجل في نفس المعاملة.** عمود
    // بيقول «في الصيانة» ومالوش حدث بيفسّره ادعاء، وأول ما حد يسأل
    // «مين وداه» مايلاقيش إجابة.

    /// <summary>المرحلة الحالية — <c>Unknown</c> للأجهزة اللي أقدم من الميزة.</summary>
    public DeviceOperationalStage OperationalStage { get; set; } = DeviceOperationalStage.Unknown;

    public DateTime? StageChangedAtUtc { get; set; }

    /// <summary>المكان الحالي، لو معروف.</summary>
    public Guid? CurrentLocationId { get; set; }
    public Location? CurrentLocation { get; set; }

    /// <summary>الفني الحائز حالياً، لو حد ماسكه.</summary>
    public Guid? CurrentHolderTechnicianId { get; set; }

    // =================================================================
    //  المراجعة قبل التسليم
    // =================================================================
    //
    // 🔴 **الكمبيوتر بيعرف إن الفحص عدّى. مابيعرفش إن اللاب جاهز.**
    //
    // «عدّى الفحص» حكم آلي. «جاهز يتسلّم» حكم بني آدم: اتنضّف؟
    // اتغلّف؟ اتلزق عليه ليبل؟ العمودين دول بيسجّلوا الحكم
    // التاني — **مين قاله وإمتى**، مش مجرد علم صح/غلط.
    //
    // ⚠️ **كلهم nullable والفاضي هو الوضع الطبيعي.** كل جهاز
    // موجود دلوقتي مش مراجَع، وده صحيح — مراجعة ماحصلتش فعلاً.
    // الهجرة مابتفترضش إن اللي فات مراجَع.

    /// <summary>
    /// إمتى حد قال إن الجهاز ده جاهز يتسلّم. <c>null</c> = لسه.
    /// </summary>
    public DateTime? ReadyForHandoverAtUtc { get; set; }

    /// <summary>مين قال «جاهز».</summary>
    public Guid? ReadyByUserId { get; set; }

    /// <summary>
    /// اسمه وقت ما قال.
    ///
    /// <para>⚠️ الاسم متخزّن مع الحركة مش متقرا من جدول المستخدمين
    /// وقت العرض — نفس قاعدة سجل التسليم. المستخدم ممكن يتغيّر
    /// اسمه أو يتشال، والسجل لازم يفضل يقول مين عمل ده ساعتها.</para>
    /// </summary>
    [MaxLength(120)]
    public string? ReadyByName { get; set; }

    // =================================================================
    //  تحذير «قطعة اتغيّرت»
    // =================================================================
    //
    // 🔴 **العمودين دول اتهام، فاقراهم بالمعنى ده.**
    //
    // المقارنة بين لقطتين للجهاز بتقول إن قطعة **اتضافت** أو
    // **اتشالت** أو **سيريالها اتغيّر في نفس المكان الثابت**. تلات
    // الحالات دول معناهم إن حد فتح اللاب. الباقي (`ModelChanged`,
    // `IdentityUncertain`, `NotObserved`) **مش تبديل** — دي فروق
    // قراءة، والكود نفسه بيقول كده.
    //
    // ⚠️ و`MajorIdentityWarning` **مش المقياس**: هو بيغطّي
    // معلومات النظام واللوحة الأم بس، والمعالج والرام والهارد
    // والبطارية كلهم برّه. هو سؤال «ده نفس الجهاز أصلاً؟» مش
    // «قطعة اتبدّلت؟».
    //
    // ⚠️ **بيتحسب وقت الاستقبال مش وقت الطلب.** الأيقونة في
    // ترويسة اللوحة يعني كل فتحة صفحة؛ الحساب عند الطلب كان
    // هيبقى ~١٠٠٠ استعلام في كل مرة على استضافة صغيرة.

    /// <summary>
    /// آخر مرة اتكشف فيها تبديل قطعة. <c>null</c> = مفيش.
    ///
    /// <para>⚠️ عليه فهرس مفلتر عشان العدّاد يبقى <c>CountAsync</c>
    /// واحد — الوضع الطبيعي إن العمود ده فاضي لكل الصفوف.</para>
    /// </summary>
    public DateTime? PartChangedAtUtc { get; set; }

    /// <summary>
    /// وصف قصير لآخر تبديل — «اتشال: هارد · اتضاف: رام».
    ///
    /// <para>⚠️ نص عرض، <b>مش</b> حقل للاستعلام. التفاصيل الكاملة
    /// بتتحسب من <c>/devices/{id}/compare</c> لما حد يفتح.</para>
    /// </summary>
    [MaxLength(300)]
    public string PartChangeSummary { get; set; } = "";

    // =================================================================
    //  حاوية الاستيراد
    // =================================================================

    /// <summary>
    /// الشحنة اللي اللاب جه فيها.
    ///
    /// <para>🔴 <b>بيتكتب مرة واحدة وبس.</b> صاحب الشغل قال
    /// الحاوية «مش بتتغير» — فأول ما العمود ده يتملّي، أي مزامنة
    /// جاية برمز مختلف <b>بتتجاهل</b> ومابتكتبش فوقه. نفس قاعدة
    /// <c>EnsureCodeAnchor</c>: ماتلمسش صف موجود.</para>
    ///
    /// <para>⚠️ و<c>null</c> شرعي: اللابات اللي اتفحصت قبل
    /// الميزة مالهاش حاوية، ومنعها من إعادة الفحص عقاب على تاريخ
    /// مش ذنبها.</para>
    /// </summary>
    public Guid? ContainerId { get; set; }
    public ImportContainer? Container { get; set; }

    public List<DeviceIdentifierRow> Identifiers { get; set; } = new();
}

/// <summary>
/// ربط معرّف جهاز محلي على راكة بالجهاز الحقيقي على السيرفر.
///
/// <para>🔴 <b>ليه ده لازم.</b> الراكة بتشتغل أوفلاين وبتولّد معرّف
/// الجهاز محلياً. راكة تانية بتفحص نفس اللاب بتولّد معرّف تاني خالص.
/// السيرفر بيتعرّف على اللاب من المراسي ويقرّر إن الاتنين نفس الجهاز —
/// بس الراكة التانية هتفضل تبعت بمعرّفها هي في كل مزامنة جاية.</para>
///
/// <para>الصف ده بيخلّي السيرفر يترجم المعرّف ده للجهاز الكانوني
/// <b>من غير ما يطلب من الراكة تغيّر حاجة</b> — وده شرط عشان
/// الأوفلاين يفضل شغّال زي ما هو.</para>
///
/// <para>⚠️ append-only عملياً: مابنغيّرش الربط بعد ما يتعمل إلا في
/// دمج صريح.</para>
/// </summary>
public class DeviceAlias
{
    /// <summary>المعرّف اللي الراكة بتبعت بيه — هو المفتاح.</summary>
    public Guid AliasDeviceId { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>الجهاز الحقيقي على السيرفر.</summary>
    public Guid CanonicalDeviceId { get; set; }
    public Device? CanonicalDevice { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(300)]
    public string Reason { get; set; } = "";

    /// <summary>الراكة اللي جابت المعرّف ده.</summary>
    public Guid? SourceRackId { get; set; }
}

/// <summary>مرساة هوية — تاريخ مش عمود.</summary>
public class DeviceIdentifierRow
{
    public long Id { get; set; }

    public Guid TenantId { get; set; }
    public Guid DeviceId { get; set; }
    public Device? Device { get; set; }

    public DeviceIdentifierKind Kind { get; set; }

    [MaxLength(200)]
    public string RawValue { get; set; } = "";

    [MaxLength(200)]
    public string NormalizedValue { get; set; } = "";

    [MaxLength(120)]
    public string Source { get; set; } = "";

    public DeviceIdentityConfidence Confidence { get; set; } = DeviceIdentityConfidence.None;

    public DateTime FirstSeenAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAtUtc { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;

    public DateTime? SupersededAtUtc { get; set; }

    [MaxLength(400)]
    public string SupersededReason { get; set; } = "";

    [MaxLength(120)]
    public string SupersededByName { get; set; } = "";
}

/// <summary>
/// ملاحظة بشرية على جهاز.
///
/// <para><b>بتتضاف وبس.</b> مفيش تعديل ومفيش مسح. التصحيح
/// بيتكتب كملاحظة جديدة — لأن اللي اتكتب عن جهاز في وقته
/// جزء من تاريخه؛ تغييره بعدين بيخلي السجل يقول حاجة ماحصلتش.</para>
///
/// <para>⚠️ <b>دي ملاحظات، مش أوامر شغل.</b> مفيش إسناد ولا حالة
/// ولا قطع غيار ولا مرفقات. أول ما حاجة من دي تتضاف هنا يبقى ده
/// نظام صيانة متخبّي في جدول ملاحظات، ومكانه مرحلة الصيانة.</para>
/// </summary>
public class DeviceNote
{
    public long Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid DeviceId { get; set; }
    public Device? Device { get; set; }

    [MaxLength(2000)]
    public string Body { get; set; } = "";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>مين كتبها — من الجلسة على السيرفر، مش من المتصفح.</summary>
    public Guid CreatedByUserId { get; set; }

    /// <summary>
    /// اسمه وقت الكتابة — لقطة مقصودة.
    ///
    /// <para>لو الحساب اتمسح أو اسمه اتغير، الملاحظة بتفضل
    /// بتقول مين كتبها يومها. ربط بمفتاح أجنبي بس كان هيخلي
    /// السجل يتغير بأثر رجعي.</para>
    /// </summary>
    [MaxLength(120)]
    public string CreatedByName { get; set; } = "";
}

/// <summary>
/// بلوك أرقام مؤجّر لراكة.
///
/// <para><b>المشكلة اللي بيحلّها.</b> السيرفر لازم يملك مساحة الأرقام
/// عشان مايحصلش تكرار. والفني ماسك اللاب على بنش <b>من غير نت</b> ومحتاج
/// يطبع الليبل دلوقتي. الاتنين مش ممكنين مع بعض إلا لو الأرقام اتحجزت
/// مقدّماً.</para>
///
/// <list type="bullet">
/// <item><b>البلوكات حصرية</b> — المدى بيتخصّص لراكة واحدة بس، فالتكرار
/// مستحيل بالتصميم مش «بعيد الاحتمال».</item>
/// <item><b>ممنوع إعادة استخدام المدى.</b> بلوك مستهلك أو ملغي عمره ما
/// بيرجع للمساحة. التدوير معناه كودين لجهازين مختلفين — وده بيهدّ تتبّع
/// العمر كله.</item>
/// <item><b>الفجوات مقبولة.</b> راكة اترمت وفي بلوكها ١٢٠ كود متستخدمش
/// = فجوة للأبد. الـ LP أرقام تعريف مش عدّاد مخزون.</item>
/// </list>
/// </summary>
public class DeviceCodeLease
{
    public long Id { get; set; }

    public Guid TenantId { get; set; }
    public Guid RackId { get; set; }

    /// <summary>أول رقم في المدى (شامل).</summary>
    public int FromNumber { get; set; }

    /// <summary>آخر رقم في المدى (شامل). عمره ما بينزل.</summary>
    public int ToNumber { get; set; }

    /// <summary>آخر رقم الراكة قالت إنها استخدمته.</summary>
    public int ConsumedThrough { get; set; }

    public DeviceCodeLeaseStatus Status { get; set; } = DeviceCodeLeaseStatus.Open;

    public DateTime IssuedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAtUtc { get; set; }

    [MaxLength(200)]
    public string ClosedReason { get; set; } = "";
}
