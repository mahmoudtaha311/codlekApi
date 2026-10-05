namespace Codlek.Core.Sync;

/// <summary>
/// قواعد استقبال الفحوص — <b>الجزء النقي</b>.
///
/// <para>🔴 <b>والمبدأ الأساسي هنا: التكرار مش بيعمل ضرر.</b> الفني
/// ممكن يرفع نفس الملف مرتين، أو النت يقطع في نص الرفع فيعيد. كل
/// فحص ليه <c>Id</c> بيتولّد <b>على الراكة</b>، فالسجل بيتحدّث مش
/// بيتكرر — ولو اتكرر كان عدد الأجهزة هيزيد غلط، وده رقم بيتبني عليه
/// تقييم الفني.</para>
/// </summary>
public static class ReportIngestRules
{
    /// <summary>
    /// 🔴 كود رفض: الهوية دي لفني في <b>شركة تانية</b>.
    ///
    /// <para>الشركة بتتقرا من مفتاح الراكة، والراكة مالهاش رأي فيها.
    /// فالنسبة العابرة للشركات مش «خطأ في البيانات» — دي <b>محاولة
    /// كتابة في شركة تانية</b>.</para>
    /// </summary>
    public const string TechnicianTenantMismatch = "TechnicianTenantMismatch";

    /// <summary>
    /// كود رفض: الهوية والكود اللي وصلوا <b>مش لنفس الفني</b>.
    ///
    /// <para>⚠️ كود الفني ثابت بعد الإنشاء، فاختلافه معناه إن الحمولة
    /// اتلغبطت في الطريق — وقبولها بيدّي فحص <b>بهويتين</b>.</para>
    /// </summary>
    public const string TechnicianCodeMismatch = "TechnicianCodeMismatch";

    /// <summary>الفحص ده يستاهل يتخزّن؟ <c>null</c> = تمام.</summary>
    ///
    /// <remarks>
    /// ⚠️ الفحوص دي <b>قبل</b> أي استعلام: معرّف فاضي أو تاريخ بداية
    /// فاضي مش بيحتاجوا قاعدة عشان نرفضهم، ورفضهم بدري بيوفّر رحلة.
    /// </remarks>
    public static string? Reject(Guid id, DateTime startedAtUtc)
    {
        if (id == Guid.Empty) return "فحص من غير رقم تعريف — اتجاهل";

        if (startedAtUtc == default)
            return $"فحص {id} من غير تاريخ بداية — اتجاهل";

        return null;
    }

    /// <summary>
    /// التاريخ الجايّ من الراكة بيتعلّم <c>Utc</c>.
    ///
    /// <para>⚠️ <b>الراكة بتبعت التواريخ من غير منطقة</b> فبتتقرا
    /// <c>Unspecified</c>. والعمود بيتقارن بتواريخ UTC، فسيبها
    /// بلافتتها كان بيزحلق المقارنات.</para>
    /// </summary>
    public static DateTime Utc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };

    /// <summary>
    /// الفاضي بيبقى <c>null</c>.
    ///
    /// <para>⚠️ <b>والفرق مش تجميل:</b> <c>""</c> بتتقري «اتحسبت
    /// وطلعت فاضية» و<c>null</c> بتتقري «مش معروفة». الاتنين
    /// بيتعرضوا بالخام، بس التفرقة بتفضل موجودة لما حد يسأل
    /// بعدين — وأي إحصاء عن حالة اللابات بيخلط الاتنين من
    /// غيرها.</para>
    /// </summary>
    public static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>الفاضي بيبقى <c>null</c>، والطويل بيتقص للحد.</summary>
    public static string? Blank(string? value, int max)
    {
        string? trimmed = Blank(value);

        return trimmed is null || trimmed.Length <= max ? trimmed : trimmed[..max];
    }

    /// <summary>
    /// بصمة الجهاز من مواصفاته — <b>للمقارنة البشرية مش للمطابقة</b>.
    ///
    /// <para>⚠️ <c>To Be Filled By O.E.M.</c> بتتشال: دي قيمة
    /// افتراضية بتتكرر على مئات اللابات، ووجودها في البصمة بيخلّي
    /// لابات مختلفة تبان بنفس البصمة.</para>
    /// </summary>
    public const string OemPlaceholder = "To Be Filled By O.E.M.";

    public static string Fingerprint(params string?[] parts)
    {
        var usable = parts
            .Where(p => !string.IsNullOrWhiteSpace(p) && p != OemPlaceholder)
            .ToArray();

        return usable.Length == 0 ? "UNKNOWN" : string.Join("/", usable);
    }

    /// <summary>
    /// حجم القرص للعرض — <b>بالنظام العشري زي ما المصنّع بيكتبه</b>.
    ///
    /// <para>⚠️ ١٠٠٠ مش ١٠٢٤: القرص اللي مكتوب عليه ٥٠٠ جيجا بيتعرض
    /// ٥٠٠ جيجا، مش ٤٦٦ — الفني بيقارن باللي على الاستيكر.</para>
    /// </summary>
    public static string SizeText(long bytes)
    {
        if (bytes <= 0) return "";

        double gb = bytes / 1_000_000_000d;

        return gb >= 1000 ? $"{gb / 1000:0.#} TB" : $"{gb:0} GB";
    }

    /// <summary>
    /// نص الرام — <b>بالجيجا الثنائية</b>.
    ///
    /// <para>⚠️ هنا ١٠٧٣٧٤١٨٢٤ (ثنائي) مش زي الهارد — لأن الرام
    /// بتتباع وبتتقرا بالثنائي فعلاً: ويندوز بيقول «8.00 GB» على
    /// ٨٥٨٩٩٣٤٥٩٢ بايت.</para>
    ///
    /// <para>⚠️ و«No RAM» مش <c>0GB</c>: صفر معناه إن القراءة فشلت،
    /// واللاب اللي بيشتغل مستحيل يكون بغير رام.</para>
    /// </summary>
    public static string RamText(long totalRamBytes) =>
        totalRamBytes > 0 ? $"{totalRamBytes / 1073741824}GB" : "No RAM";

    /// <summary>
    /// نص وحدات التخزين — <b>مجموعة بـ<c> + </c></b>.
    ///
    /// <para>⚠️ و«No Hard» مش فاضي: لاب من غير هارد حالة حقيقية
    /// (اتسحب منه)، والخانة الفاضية بتتقري «مقريناش».</para>
    /// </summary>
    public static string StorageText(IEnumerable<(long SizeBytes, string MediaType)> disks)
    {
        var list = disks.ToList();

        return list.Count == 0
            ? "No Hard"
            : string.Join(" + ", list.Select(d =>
                $"{SizeText(d.SizeBytes)} {d.MediaType}".Trim()));
    }

    // =================================================================
    //  عدّادات المراحل — أرقام الحالة على السلك
    // =================================================================

    /// <summary>
    /// ⚠️ <b>الأرقام دي على السلك</b> — الراكة بتبعت
    /// <c>StepResult.Status</c> كرقم، فتغييرها معناه إن كل عدّاد في
    /// كل تقرير قديم يبقى غلط.
    /// </summary>
    public const int StepPass = 1;

    public const int StepFail = 2;
    public const int StepSkip = 3;
    public const int StepNotPresent = 4;
    public const int StepError = 5;

    /// <summary>
    /// 🔴 <b>«ماتفحصتش» عدّاد سادس، مش غياب.</b>
    ///
    /// <para>من غيره، المرحلة اللي محدّش لمسها بتختفي من العدّادات
    /// خالص والتقرير بيبان كأنه اتفحص بالكامل. وبيتحسب من الحالات
    /// فبيشتغل على التقارير القديمة كمان من غير أي حاجة جديدة من
    /// الراكة.</para>
    ///
    /// <para>⚠️ <b>وبس للمراحل اللي محتاجة نتيجة.</b> مرحلة التسليم
    /// مش تست، وعدّها كانت بتخلّي كل تقرير يبان ناقص مرحلة.</para>
    /// </summary>
    public static int NotRun(IEnumerable<(bool RequiresResult, int Status)> steps) =>
        steps.Count(s => s.RequiresResult && s.Status == 0);

    // =================================================================
    //  حالة المسح — قرار واحد، وقته آخر مسح أو استرجاع
    // =================================================================

    /// <summary>
    /// وقت قرار المسح: الأحدث بين وقت المسح ووقت الاسترجاع.
    /// <c>null</c> = محدّش قرّر حاجة خالص.
    ///
    /// <para>⚠️ <b>الخانات دي قرار واحد مش خانات منفصلة.</b>
    /// <c>IsDeleted</c> ومين مسح وليه وإمتى ومين رجّع وليه وإمتى —
    /// كلهم بيتكتبوا مع بعض أو بيفضلوا مع بعض. خلط نصّهم من الراكة
    /// ونصّهم من الموقع بيدّي فحص «ممسوح» وسبب مسحه سبب حد تاني.</para>
    /// </summary>
    public static DateTime? DeletionDecisionAt(DateTime? deletedAtUtc, DateTime? restoredAtUtc)
    {
        if (deletedAtUtc is null) return restoredAtUtc;
        if (restoredAtUtc is null) return deletedAtUtc;

        return deletedAtUtc.Value >= restoredAtUtc.Value ? deletedAtUtc : restoredAtUtc;
    }

    /// <summary>
    /// حالة المسح اللي جاية من الراكة تكسب حالة الصف المتخزّن؟
    ///
    /// <para>🔴 <b>بتكسب بس لو قرارها أحدث بالظبط.</b> المالك أو المدير
    /// اللي مسح فحص من الموقع (<c>/reports/{id}/delete</c>) مسحه
    /// <b>على السيرفر بس</b> — والراكة لسه شايلاه «مش ممسوح». فأول
    /// مرة الراكة تعيد إرسال الفحص بعد أي تعديل، القديم كان بينسخ
    /// حالتها فوق الصف، <b>والمسح بيتلغي في صمت</b>. ونفس الكلام
    /// للاسترجاع.</para>
    ///
    /// <list type="bullet">
    /// <item>الصف مالوش قرار ← الراكة تكسب. ودي حالة الإضافة كمان:
    /// الصف الجديد فاضي، فحالة الراكة بتتكتب زي النهارده.</item>
    /// <item>الراكة مالهاش قرار والصف ليه ← الصف يفضل.</item>
    /// <item>الاتنين ليهم ← الأحدث يكسب.</item>
    /// <item>⚠️ <b>والتعادل للصف.</b> نفس الوقت بالظبط غالباً هو نفس
    /// القرار راجع تاني، ومفيش سبب نكتب فوق حاجة ماتغيّرتش.</item>
    /// </list>
    /// </summary>
    public static bool RackDeletionWins(
        DateTime? storedDeletedAtUtc, DateTime? storedRestoredAtUtc,
        DateTime? rackDeletedAtUtc, DateTime? rackRestoredAtUtc)
    {
        var stored = DeletionDecisionAt(storedDeletedAtUtc, storedRestoredAtUtc);

        if (stored is null) return true;

        var rack = DeletionDecisionAt(rackDeletedAtUtc, rackRestoredAtUtc);

        return rack is not null && rack.Value > stored.Value;
    }
}
