namespace Codlek.Application.Contracts.Sync;

/// <summary>
/// شكل ملف <c>reports.json</c> اللي بيتكتب على الراكة — <b>بنستقبله
/// زي ما هو بالظبط</b>.
///
/// <para>🔴 <b>أي تغيير في أسماء الحقول هنا لازم يتغيّر في
/// <c>spics.Models.LaptopReport</c> على الراكة، والعكس.</b>
/// <c>System.Text.Json</c> بيرمي أي خاصية مش موجودة هنا <b>في
/// صمت</b> — مفيش استثناء ومفيش رفض، والحقل بيتمسح على الحدود بين
/// الطرفين. عضّت المشروع مرتين كده (<c>DeviceId</c> على الفحص،
/// وسيريال القرص).</para>
///
/// <para>⚠️ والقراية بـ<c>PropertyNameCaseInsensitive</c> فحتى لو
/// الصيغة اتغيّرت لـcamelCase بعدين، القديم بيفضل شغّال.</para>
/// </summary>
public sealed class ReportFilePayload
{
    public int Version { get; set; } = 1;

    public List<LaptopReportPayload> Reports { get; set; } = [];
}

/// <summary>فحص واحد زي ما الراكة كتبته.</summary>
public sealed class LaptopReportPayload
{
    public Guid Id { get; set; }

    // =================================================================
    //  هوية الفني على السلك
    //
    //  التلاتة بيتبعتوا مع بعض، والسيرفر بيخزّنهم كلهم:
    //
    //    technicianId   — هوية الفني المركزية (GUID)
    //    technicianCode — الكود المقروء
    //    technicianName — لقطة الاسم وقت الفحص
    // =================================================================

    /// <summary>
    /// هوية الفني المركزية — <c>string</c> مش <c>Guid?</c> <b>عن
    /// قصد</b>.
    ///
    /// <para>🔴 الراكات القديمة بتبعت <c>""</c> هنا (وكمان معرّف
    /// الحساب المحلي بتاعها، وهو GUID تاني خالص مالوش وجود على
    /// السيرفر). <c>Guid?</c> كان هيرمي <c>JsonException</c> على
    /// النص الفاضي — يعني <b>الفحص كله يترفض</b> لمجرد إن العميل
    /// قديم. النص بيتقرا هنا وبيتفكّ بـ<c>Guid.TryParse</c> في
    /// الاستقبال، واللي مايتفكّش بيتعامل كـ«من غير هوية
    /// مركزية».</para>
    ///
    /// <para>⚠️ واللي بيتفكّ ويطلع فني في <b>شركة تانية</b> بيترفض —
    /// مش بيتجاهل.</para>
    /// </summary>
    public string TechnicianId { get; set; } = "";

    /// <summary>لقطة الاسم وقت الفحص — <b>مش</b> الاسم الحالي للفني.</summary>
    public string TechnicianName { get; set; } = "";

    public string TechnicianCode { get; set; } = "";
    public string ImportedFrom { get; set; } = "";

    public bool IsDeleted { get; set; }
    public string DeletedReason { get; set; } = "";
    public string DeletedByName { get; set; } = "";
    public DateTime? DeletedAtUtc { get; set; }

    public string RestoredByName { get; set; } = "";
    public string RestoredReason { get; set; } = "";
    public DateTime? RestoredAtUtc { get; set; }

    public List<EditEntryPayload> Edits { get; set; } = [];

    public DateTime StartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public long DurationMs { get; set; }

    public DeviceSpecsPayload Specs { get; set; } = new();
    public BackgroundResultsPayload Background { get; set; } = new();
    public List<StepResultPayload> Steps { get; set; } = [];

    public string GeneralNote { get; set; } = "";

    // ⚠️ أسماء الحقول دي لازم تطابق `LaptopReport` على الراكة
    //    بالحرف. اسم مختلف بيتقرا null **في صمت** — مفيش استثناء
    //    ومفيش رفض، والفني بيعلّم على صيانة وهي مابتوصلش.
    public string? ScreenGrade { get; set; }
    public string? ScreenRepair { get; set; }
    public string? HousingPaint { get; set; }
    public string? HousingCrack { get; set; }
    public string? BatteryService { get; set; }
    public string? Disassembly { get; set; }

    public List<string> PartsUsed { get; set; } = [];

    /// <summary>
    /// نطاق الفحص: <c>0</c> كامل · <c>1</c> جزئي · <c>null</c> راكة
    /// أقدم من الميزة.
    ///
    /// <para>🔴 <c>null</c> <b>مش</b> معناها «كامل». معناها إن الراكة
    /// اللي بعتت التقرير ده ماكانتش بتعرف تفرّق أصلاً، والتقرير اتعمل
    /// أيام ما كان مستحيل تسلّم فحص ناقص. خلط الاتنين بيخلّي كل تقرير
    /// قديم يتقال عليه «كامل» من غير دليل.</para>
    ///
    /// <para>⚠️ <c>int?</c> مش enum: السيرفر مالوش <c>TestScope</c>،
    /// والسلك كله بيستخدم أرقام أصلاً.</para>
    /// </summary>
    public int? Scope { get; set; }

    // =================================================================
    //  هوية الجهاز
    //
    //  ⚠️ الحقول دي كانت **ناقصة** من العقد، والراكة كانت بتبعتها.
    //     فالربط بين الفحص والجهاز كان بيتمسح على الحدود بين الطرفين
    //     من غير أي خطأ ولا تحذير.
    // =================================================================

    /// <summary>
    /// الجهاز اللي الفحص ده اتعمل عليه — بيتولّد <b>على الراكة</b>.
    ///
    /// <para><c>null</c> معناها فحص قديم اتعمل قبل هوية الأجهزة، أو
    /// حمولة من نسخة برنامج أقدم. الاتنين بيتقبلوا، وبيتعلّموا
    /// <c>NeedsDeviceResolution</c> عشان المدير يحلّهم — <b>مش</b>
    /// بيتعملهم جهاز تلقائي.</para>
    /// </summary>
    public Guid? DeviceId { get; set; }

    /// <summary>
    /// <c>LP-00018425</c> وقت الفحص. فاضي معناه الراكة لسه ماخدتش
    /// بلوك.
    /// </summary>
    public string DeviceCode { get; set; } = "";

    /// <summary>
    /// كود الراكة زي ما هي عارفاه — <b>للمراجعة بس</b>، السيرفر
    /// بيعتمد على المفتاح.
    /// </summary>
    public string RackCode { get; set; } = "";

    public string ApplicationVersion { get; set; } = "";
    public string TestDefinitionVersion { get; set; } = "";

    public string CompletedByTechnicianCode { get; set; } = "";
    public string CompletedByTechnicianName { get; set; } = "";

    /// <summary>لقطة العتاد وقت الفحص — «كان جوّاه إيه يومها».</summary>
    public HardwareSnapshotPayload? Snapshot { get; set; }
}

/// <summary>
/// لقطة العتاد وقت الفحص.
///
/// <para>دي مش «مواصفات الجهاز» — دي <b>إيه اللي كان جوّاه اليوم
/// ده</b>. والفرق هو اللي بيخلّي «الرامة اتسرقت» سؤال له
/// إجابة.</para>
/// </summary>
public sealed class HardwareSnapshotPayload
{
    public DateTime CapturedAtUtc { get; set; }
    public string CollectorVersion { get; set; } = "";
    public bool RanAsAdministrator { get; set; }
    public bool IsPartial { get; set; }
    public string CollectionWarnings { get; set; } = "";

    public List<SnapshotComponentPayload> Components { get; set; } = [];
}

/// <summary>مكوّن واحد جوّه اللقطة.</summary>
public sealed class SnapshotComponentPayload
{
    public int Type { get; set; }
    public int InstanceIndex { get; set; }
    public string SlotOrPosition { get; set; } = "";

    public string Manufacturer { get; set; } = "";
    public string Model { get; set; } = "";
    public string PartNumber { get; set; } = "";
    public string ManufacturerSerial { get; set; } = "";

    public string HardwareFingerprint { get; set; } = "";
    public string PnPDeviceId { get; set; } = "";

    /// <summary>
    /// قرينا الهوية دي منين. <b>رقم مش نص</b> — الراكة بتسلسل
    /// <c>IdentityMethod</c> كقيمة عددية، ونص هنا كان هيفضل فاضي
    /// للأبد من غير أي خطأ.
    /// </summary>
    public int IdentityMethod { get; set; }

    public int IdentityConfidence { get; set; }

    public bool IsPresent { get; set; } = true;

    // ⚠️ كلها nullable زي الموديل على الراكة بالظبط. «مفيش قراءة»
    //    و«صفر» مش نفس المعنى — قرص سعته صفر معناه إن القراءة فشلت،
    //    مش إن القرص فاضي.
    public long? CapacityBytes { get; set; }
    public int? SpeedMhz { get; set; }
    public int? HealthPercent { get; set; }
    public int? PowerOnHours { get; set; }

    public string Source { get; set; } = "";

    /// <summary>تفاصيل إضافية زي ما القارئ رجّعها.</summary>
    public string AttributesJson { get; set; } = "";
}

public sealed class EditEntryPayload
{
    public DateTime AtUtc { get; set; }
    public string ByName { get; set; } = "";
    public string Field { get; set; } = "";
    public string OldValue { get; set; } = "";
    public string NewValue { get; set; } = "";
    public string Reason { get; set; } = "";
}

public sealed class StepResultPayload
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public int Status { get; set; }
    public string Note { get; set; } = "";
    public string SkipReason { get; set; } = "";
    public string Detail { get; set; } = "";
    public long DurationMs { get; set; }

    /// <summary>
    /// المرحلة دي محتاجة نتيجة عشان تتحسب في النطاق؟
    ///
    /// <para>🔴 <b>الراكة بتبعت الحقل ده من زمان — السيرفر هو اللي
    /// كان بيرميه.</b> ومن غيره، «المراحل اللي ماتفحصتش» بتعدّ مرحلة
    /// التسليم — وهي مش تست أصلاً — فكل تقرير بيبان ناقص
    /// مرحلة.</para>
    ///
    /// <para>⚠️ والافتراضي <c>true</c>: لو حمولة قديمة مافيهاش
    /// الحقل، بنعدّ كل المراحل بدل ما نعدّ ولا واحدة.</para>
    /// </summary>
    public bool RequiresResult { get; set; } = true;
}

public sealed class DeviceSpecsPayload
{
    public string Manufacturer { get; set; } = "";

    /// <summary>
    /// الموديل الخام — على لينوفو بيبقى كود زي <c>82B5</c>.
    ///
    /// <para>🔴 ده <b>دليل خام ومابيتدهسش</b>. الرقم ده مكتوب على
    /// استيكر تحت اللاب والفني بيدوّر بيه في قطع الغيار.</para>
    /// </summary>
    public string Model { get; set; } = "";

    /// <summary>
    /// الاسم اللي الناس بتعرف بيه اللاب — <c>Legion 5 15ARH05</c>.
    ///
    /// <para>⚠️ <c>null</c> معناها إن الراكة أقدم من الميزة دي، أو إن
    /// الأدلة كلها فشلت على اللاب ده. <b>مش</b> معناها إن اللاب مالوش
    /// اسم تجاري — عشان كده العرض بيرجع للخام بدل ما يسيب الخانة
    /// فاضية.</para>
    /// </summary>
    public string? CommercialModelName { get; set; }

    /// <summary>منين جه الاسم — للمراجعة وقت الشك.</summary>
    public string? CommercialModelSource { get; set; }

    public string? SystemFamily { get; set; }
    public string? SystemSku { get; set; }

    /// <summary>كود المصنع لما الخام يبقى كود مش اسم.</summary>
    public string? MachineType { get; set; }

    public string SerialNumber { get; set; } = "";
    public string BoardSerial { get; set; } = "";
    public string SystemUuid { get; set; } = "";

    public string Cpu { get; set; } = "";
    public int CpuCores { get; set; }
    public int CpuThreads { get; set; }

    public long TotalRamBytes { get; set; }
    public string RamType { get; set; } = "";
    public int RamSpeedMhz { get; set; }

    public string Gpu { get; set; } = "";

    public List<DiskInfoPayload> InternalDisks { get; set; } = [];
    public BatteryInfoPayload Battery { get; set; } = new();
    public ScreenInfoPayload Screen { get; set; } = new();
}

public sealed class DiskInfoPayload
{
    public string Model { get; set; } = "";

    /// <summary>
    /// ⚠️ كان <b>ناقص</b> من العقد.
    ///
    /// <para>سيريال القرص أقوى مرساة بنقراها فعلاً من ويندوز، والراكة
    /// بتخزّنها محلياً — بس ماكانتش بتوصل السيرفر أبداً، فالسيرفر كان
    /// <b>أعمى عن أقوى دليل عنده</b>.</para>
    /// </summary>
    public string SerialNumber { get; set; } = "";

    public long SizeBytes { get; set; }
    public string MediaType { get; set; } = "";
    public string BusType { get; set; } = "";
    public string HealthText { get; set; } = "";
    public int? WearPercent { get; set; }
}

public sealed class BatteryInfoPayload
{
    public int DesignCapacity { get; set; }
    public int FullChargeCapacity { get; set; }
    public int CycleCount { get; set; }

    /// <summary>
    /// ⚠️ <b>محسوبة مش متخزّنة — ومسقوفة على ١٠٠.</b> بطارية
    /// بسعة كاملة أكبر من التصميم (بيحصل) كانت بتطلّع ١٠٤٪.
    /// </summary>
    public double HealthPercent =>
        DesignCapacity > 0
            ? Math.Min(100, Math.Round(FullChargeCapacity / (double)DesignCapacity * 100))
            : 0;
}

public sealed class ScreenInfoPayload
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int RefreshRate { get; set; } = 60;
    public double DiagonalInches { get; set; }
    public bool IsTouch { get; set; }

    /// <summary>
    /// ملخّص الشاشة للعرض — <b>بنفس الترتيب والفواصل بالحرف</b>.
    ///
    /// <para>⚠️ النص ده بيتخزّن في عمود وبيتعرض في عشر شاشات؛ تغيير
    /// الفاصل بيخلّي الصفوف القديمة والجديدة شكلين.</para>
    /// </summary>
    public string Summary
    {
        get
        {
            var parts = new List<string>();

            if (DiagonalInches > 0) parts.Add($"{DiagonalInches:0.#}\"");
            if (Width > 0 && Height > 0) parts.Add($"{Width}x{Height}");

            parts.Add($"{RefreshRate}Hz");
            parts.Add(IsTouch ? "Touch" : "Non-Touch");

            return string.Join(" | ", parts);
        }
    }
}

public sealed class BackgroundResultsPayload
{
    public int? BenchmarkScore { get; set; }
    public string BenchmarkDetail { get; set; } = "";
    public string ThermalSummary { get; set; } = "";
    public double? MaxCpuTemp { get; set; }
    public int? MaxFanRpm { get; set; }
    public bool ThrottlingDetected { get; set; }
    public string MemoryTestResult { get; set; } = "";
    public string SurfaceScanResult { get; set; } = "";
    public string StressResult { get; set; } = "";
}

/// <summary>
/// سبب رفض فحص بعينه — <b>عشان مسار الدفعات يقفل الصف بسببه هو</b>.
/// </summary>
/// <param name="Code">كود ثابت للراكة تتفرّع عليه (مش نص عربي).</param>
/// <param name="Retryable">إعادة نفس الحمولة ممكن تنجح؟</param>
public sealed record IngestRejection(string Code, string Message, bool Retryable);

/// <summary>
/// نتيجة عملية رفع — <b>بتتعرض للمستخدم وبترجع من الـAPI</b>.
///
/// <para>🔴 <b>ومفيش <c>results</c> هنا عن قصد.</b> قارئ طابور
/// الراكة بيدوّر على <c>results[*].outboxId</c> وبس؛ لو لقى الشكل ده
/// من غير صفّه، بيقرا «اترفع» <b>وبيمسح الصف</b>. فالنقطة دي
/// (<c>/api/sync/reports</c>) عمرها ما تطلّع مصفوفة
/// <c>results</c> — واللي محتاج نتيجة لكل صف بيستعمل
/// <c>/api/v2/sync/batch</c>.</para>
/// </summary>
public sealed class IngestResult
{
    public int Added { get; set; }
    public int Updated { get; set; }
    public int Unchanged { get; set; }
    public int Rejected { get; set; }

    public List<string> Problems { get; set; } = [];

    /// <summary>
    /// المرفوض <b>بمعرّفه</b> — مش بس عدّاد.
    ///
    /// <para>🔴 مسار <c>/api/v2/sync/batch</c> بيقفل كل صف في طابور
    /// الراكة لوحده، فمحتاج يعرف <b>أنهي</b> فحص اترفض وليه. من غير
    /// الخريطة دي كان لازم يفترض إن اللي دخل الاستقبال كله اتقبل —
    /// وده بالظبط اللي بيخلّي فحص مرفوض يتقفل في الطابور
    /// ويضيع.</para>
    /// </summary>
    public Dictionary<Guid, IngestRejection> RejectedById { get; } = [];

    public int Total => Added + Updated + Unchanged + Rejected;

    public string Summary =>
        $"وصل {Total} فحص: {Added} جديد، {Updated} اتحدّث، {Unchanged} زي ما هو"
        + (Rejected > 0 ? $"، {Rejected} مرفوض" : "");
}

/// <summary>
/// جهاز جايّ من الراكة.
///
/// <para>🔴 <b>والراكة هي اللي بتولّد <c>Id</c></b> عشان تشتغل
/// أوفلاين، فالسيرفر بيعمل <c>upsert</c> بالمعرّف ده مش بيولّد واحد
/// جديد. لو عمل، <b>كل راكة كانت هتخلّق نسخة تانية من نفس
/// اللاب</b>.</para>
/// </summary>
public sealed class DeviceSyncPayload
{
    public Guid Id { get; set; }

    public string PublicCode { get; set; } = "";
    public int CodeState { get; set; }

    public int Confidence { get; set; }
    public string IdentityBasis { get; set; } = "";
    public int Status { get; set; }

    public DateTime FirstSeenAtUtc { get; set; }
    public DateTime LastSeenAtUtc { get; set; }

    public string FirstSeenByTechnicianCode { get; set; } = "";

    public string LastKnownManufacturer { get; set; } = "";
    public string LastKnownModel { get; set; } = "";

    /// <summary>
    /// رمز حاوية الاستيراد زي ما الفني كتبه أو اختاره.
    ///
    /// <para>🔴 <b>والاسم ده لازم يطابق اللي على الراكة
    /// بالحرف.</b> الحقل اللي اسمه غلط <b>بيتجاهل في صمت</b> — ودي
    /// عضّت المشروع قبل كده مع <c>DeviceId</c> على الفحص.</para>
    ///
    /// <para>⚠️ وفاضي شرعي: الراكات القديمة مش عارفة الحقل ده أصلاً،
    /// والفاضي معناه «ماتلمسش الحاوية» مش «امسحها».</para>
    /// </summary>
    public string ContainerCode { get; set; } = "";

    public List<DeviceIdentifierPayload> Identifiers { get; set; } = [];
}

/// <summary>مرساة هوية — <b>تاريخ مش عمود</b>.</summary>
public sealed class DeviceIdentifierPayload
{
    public int Kind { get; set; }
    public string RawValue { get; set; } = "";
    public string NormalizedValue { get; set; } = "";
    public string Source { get; set; } = "";
    public int Confidence { get; set; }

    public DateTime FirstSeenAtUtc { get; set; }
    public DateTime LastSeenAtUtc { get; set; }

    public bool IsActive { get; set; } = true;
    public string SupersededReason { get; set; } = "";
    public string SupersededByName { get; set; } = "";
    public DateTime? SupersededAtUtc { get; set; }
}
