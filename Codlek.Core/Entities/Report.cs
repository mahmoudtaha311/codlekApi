using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Codlek.Core.Entities;

/// <summary>
/// فحص جهاز واحد — نفس الـ Id بتاع البرنامج المكتبي، فالرفع
/// المكرر مبيعملش نسخة تانية.
/// </summary>
public class Report
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    // =================================================================
    //  مين عمل الفحص ده — هوية ثابتة + لقطة وقتها
    //
    //  التلاتة مع بعض، وكل واحد بيجاوب على سؤال تاني:
    //
    //    TechnicianId   — **الهوية المركزية الثابتة**. بتربط الفحص
    //                     بحساب الفني على السيرفر مهما اتغيّر اسمه.
    //    TechnicianCode — الكود المقروء وقت الفحص. ثابت بعد الإنشاء.
    //    TechnicianName — **لقطة الاسم يوم الفحص**، مش الاسم الحالي.
    //
    //  🔴 العرض بياخد اللقطة، مش Technician.DisplayName. المدير لما
    //  يصحّح اسم فني النهارده، تقارير السنة اللي فاتت مالهاش تتغيّر —
    //  التقرير وثيقة اتسلّمت، مش شاشة بتتحدّث.
    // =================================================================

    /// <summary>
    /// الفني اللي عمل الفحص — <b>الهوية المركزية</b>.
    ///
    /// <para><c>null</c> معناها فحص وصل من غير هوية مركزية: نسخة راكة
    /// أقدم من المصادقة المركزية، أو شيت اتستورد بإيد المدير. الصفوف
    /// دي بتفضل مقروءة بلقطة الاسم والكود — <b>مابترفضش</b>.</para>
    ///
    /// <para>⚠️ العلاقة <c>Restrict</c>: مسح فني مايمسحش تاريخه. الفني
    /// بيتوقف (<c>IsActive</c>) مش بيتمسح.</para>
    /// </summary>
    public Guid? TechnicianId { get; set; }
    public Technician? Technician { get; set; }

    [MaxLength(20)]
    public string TechnicianCode { get; set; } = "";

    /// <summary>لقطة الاسم وقت الفحص — <b>مش</b> الاسم الحالي للفني.</summary>
    [MaxLength(120)]
    public string TechnicianName { get; set; } = "";

    public DateTime StartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public long DurationMs { get; set; }

    // --- المواصفات ---
    [MaxLength(80)] public string Manufacturer { get; set; } = "";

    /// <summary>
    /// الموديل الخام — على لينوفو كود زي <c>82B5</c>.
    /// 🔴 دليل خام ومابيتدهسش.
    /// </summary>
    [MaxLength(120)] public string Model { get; set; } = "";

    /// <summary>
    /// الاسم اللي الناس بتعرف بيه اللاب.
    ///
    /// <para>⚠️ <c>null</c> = راكة أقدم من الميزة، أو الأدلة فشلت على
    /// اللاب ده. العرض بيرجع للخام ساعتها.</para>
    /// </summary>
    [MaxLength(160)] public string? CommercialModelName { get; set; }

    [MaxLength(60)] public string? CommercialModelSource { get; set; }
    [MaxLength(40)] public string? MachineType { get; set; }

    [MaxLength(160)] public string Cpu { get; set; } = "";
    [MaxLength(60)] public string RamText { get; set; } = "";
    [MaxLength(120)] public string StorageText { get; set; } = "";
    [MaxLength(160)] public string Gpu { get; set; } = "";
    [MaxLength(200)] public string ScreenSummary { get; set; } = "";
    [MaxLength(120)] public string SerialNumber { get; set; } = "";
    [MaxLength(300)] public string Fingerprint { get; set; } = "";

    /// <summary>
    /// نص البحث المطبَّع — كل الحقول اللي بيتبحث فيها مجمّعة ومعدّية على
    /// <c>ArabicText.Normalize</c>.
    ///
    /// <para><b>ليه عمود متخزّن مش حساب وقت البحث.</b> البحث بيتنفّذ في
    /// SQL (<c>LIKE</c>)، والتطبيع دالة C# مش ممكن SQL Server يترجمها.
    /// لو حسبناه في اللحظة، EF كان هيسحب كل الصفوف للذاكرة ويفلتر بعدين —
    /// وده بيتحوّل من ملّي ثانية لثواني مع أول عشرة آلاف فحص.</para>
    ///
    /// <para>⚠️ بيتحدّث في <c>ReportIngestService.Apply</c>. أي مكان تاني
    /// بيعدّل الحقول دي لازم يحدّثه، وإلا الفحص بيختفي من البحث من غير
    /// أي رسالة.</para>
    /// </summary>
    public string SearchText { get; set; } = "";

    public double ScreenInches { get; set; }
    public int RefreshRate { get; set; }
    public bool IsTouch { get; set; }

    public double BatteryHealthPercent { get; set; }
    public int? BenchmarkScore { get; set; }
    public double? MaxCpuTemp { get; set; }
    public bool ThrottlingDetected { get; set; }

    // --- النتيجة ---
    public int PassCount { get; set; }

    /// <summary>
    /// الأعطال بس.
    ///
    /// <para>⚠️ مكوّن مش موجود <b>مش</b> عطل. لاب من غير هارد داخلي حالة
    /// طبيعية في بضاعة الاستيراد، وحسابها عطل كان بيخلي كل الإحصائيات
    /// غلط — و<c>NeedsRepair</c> بيتبني عليها.</para>
    /// </summary>
    public int FailCount { get; set; }

    public int SkipCount { get; set; }

    /// <summary>مكوّنات مش موجودة في الجهاز — معلومة مواصفات.</summary>
    public int NotPresentCount { get; set; }

    /// <summary>مراحل الفحص نفسه ما اشتغلش فيها — محتاجة إعادة.</summary>
    public int ErrorCount { get; set; }

    /// <summary>
    /// عدد المراحل اللي ماتفحصتش — بتكمّل العدّادات الخمسة فوق.
    ///
    /// <para>محسوب على السيرفر من حالات المراحل، فمابيحتاجش أي حاجة
    /// جديدة من الراكة وبيشتغل على التقارير القديمة كمان.</para>
    /// </summary>
    public int NotRunCount { get; set; }

    /// <summary>
    /// نطاق الفحص: <c>0</c> كامل · <c>1</c> جزئي · <c>null</c> تقرير من
    /// قبل الميزة. ⚠️ <c>null</c> مش «كامل».
    /// </summary>
    public int? Scope { get; set; }

    public string GeneralNote { get; set; } = "";

    // --- المسح والاسترجاع (نفس مبدأ البرنامج: مفيش حذف حقيقي) ---
    public bool IsDeleted { get; set; }
    [MaxLength(400)] public string DeletedReason { get; set; } = "";
    [MaxLength(120)] public string DeletedByName { get; set; } = "";
    public DateTime? DeletedAtUtc { get; set; }

    [MaxLength(120)] public string RestoredByName { get; set; } = "";
    [MaxLength(400)] public string RestoredReason { get; set; } = "";
    public DateTime? RestoredAtUtc { get; set; }

    [MaxLength(200)] public string ImportedFrom { get; set; } = "";

    // =================================================================
    //  الجهاز اللي الفحص ده اتعمل عليه
    //
    //  ده اللي كان ناقص. من غيره كل فحص صف مستقل، وسؤال زي «اللاب ده
    //  اتفحص كام مرة؟» مالوش إجابة — وهو السؤال اللي النظام كله قايم
    //  عليه.
    // =================================================================

    public Guid? DeviceId { get; set; }
    public Device? Device { get; set; }

    /// <summary>الكود وقت الفحص — متخزّن زي ما وصل عشان الليبل القديم يفضل مقروء.</summary>
    [MaxLength(20)]
    public string DeviceCode { get; set; } = "";

    /// <summary>
    /// الفحص ده محتاج بني آدم يقرّر جهازه.
    ///
    /// <para><b>ليه مش بنعمل جهاز تلقائي.</b> الفحوصات القديمة مراسيها
    /// ضعيفة أو وهمية — «Default string» و«To Be Filled By O.E.M.»
    /// بتتكرر على مئات اللابات. توليد جهاز من كل واحد فيهم معناه آلاف
    /// الأجهزة الوهمية اللي محدش هيقدر ينضّفها بعد كده. فبنعلّمها
    /// وبنسيبها للمراجعة.</para>
    /// </summary>
    public bool NeedsDeviceResolution { get; set; }

    // --- لقطة العتاد وقت الفحص ---
    public DateTime? SnapshotCapturedAtUtc { get; set; }

    [MaxLength(40)] public string SnapshotCollectorVersion { get; set; } = "";
    public bool SnapshotRanAsAdministrator { get; set; }
    public bool SnapshotIsPartial { get; set; }
    [MaxLength(1000)] public string SnapshotWarnings { get; set; } = "";

    public List<ReportSnapshotComponent> SnapshotComponents { get; set; } = new();

    // --- من فين وصل ---
    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid? SourceRackId { get; set; }

    /// <summary>
    /// نسخة كاملة من اللي وصل بالظبط.
    ///
    /// الأعمدة فوق هي اللي بيتبني عليها التحليلات، بس لو احتجنا حقل
    /// جديد بعدين مش هنطلب من الفنيين يعيدوا فحص — الداتا الأصلية
    /// كلها هنا.
    /// </summary>
    public string RawJson { get; set; } = "";

    public List<ReportStep> Steps { get; set; } = new();
    public List<ReportPart> Parts { get; set; } = new();
    public List<ReportEdit> Edits { get; set; } = new();

    // =================================================================
    //  درجة الشاشة والصيانة المطلوبة
    // =================================================================
    //
    // ⚠️ كلها nullable: الفحوصات اللي اتعملت قبل الميزة دي مالهاش
    // قيم، و«فاضي» هنا معناه «الفحص ده أقدم من الميزة» مش «الفني بصّ
    // وماعلّمش حاجة». الفرق ده مهم لأي إحصاء بيتبني عليهم.

    /// <summary>درجة الشاشة اللي الفني اختارها — A / B / BB.</summary>
    [MaxLength(4)] public string? ScreenGrade { get; set; }

    /// <summary>صيانة الشاشة — بقع / اسكراتشات.</summary>
    [MaxLength(120)] public string? ScreenRepair { get; set; }

    /// <summary>درجات الرش على الهاوسينج.</summary>
    [MaxLength(60)] public string? HousingPaint { get; set; }

    /// <summary>درجات الكسر على الهاوسينج.</summary>
    [MaxLength(80)] public string? HousingCrack { get; set; }

    /// <summary>البطارية محتاجة صيانة ولا سليمة.</summary>
    [MaxLength(30)] public string? BatteryService { get; set; }

    /// <summary>فك وتركيب مطلوب — كيبورد / سماعة / ماوس / كاميرا.</summary>
    [MaxLength(120)] public string? Disassembly { get; set; }

    // ⛔ PORT-BLOCKER (ملحوظة نقل، مش من الأصل): الخاصية المحسوبة
    // LaptopName بتنادي CodlekWeb.Services.DeviceNaming، وهي مش كيان
    // ولا enum — فمانقلتهاش ومااخترعتهاش. [NotMapped] يعني مفيش أي أثر
    // على الschema. النص الأصلي محفوظ بالحرف تحت، يرجع زي ما هو أول ما
    // DeviceNaming يتنقل.
//     /// <summary>
//     /// الاسم اللي الناس بتعرف بيه اللاب.
//     ///
//     /// <para>🔴 كان بيستعمل <see cref="Model"/> الخام، يعني كل صفحة
//     /// بتعرضه كانت بتكتب <c>LENOVO 82B5</c> — كود مصنع مالوش أي معنى
//     /// للبايع ولا للمدير — رغم إن نفس الفحص شايل
//     /// <c>Legion 5 15ARH05</c> في العمود اللي جنبه.</para>
//     ///
//     /// <para>⚠️ عشر أماكن كانت بتعرضه: التقرير اليومي، الصفحة
//     /// الرئيسية، قايمة الفحوص، تفاصيل الفحص، صفحة الفني، والسجل.
//     /// السطر ده بيصلّحهم كلهم.</para>
//     ///
//     /// <para>نفس قاعدة <c>DeviceSpecs.ModelDisplay</c> على الراكة
//     /// بالحرف — الاسم التجاري، والخام لو مفيش.</para>
//     /// </summary>
//     [NotMapped]
//     public string LaptopName =>
//         CodlekWeb.Services.DeviceNaming.Compose(
//             Manufacturer,
//             CodlekWeb.Services.DeviceNaming.Display(CommercialModelName, Model));

    /// <summary>الجهاز فيه مشكلة؟ ده اللي بيحدد «محتاج صيانة».</summary>
    [NotMapped]
    public bool NeedsRepair => FailCount > 0;

    [NotMapped]
    public string ResultText => FailCount > 0 ? "فيه أعطال" : "سليم";
}
