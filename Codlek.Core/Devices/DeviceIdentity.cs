using Codlek.Core.Enums;
using Codlek.Core.Text;

namespace Codlek.Core.Devices;

/// <summary>نتيجة محاولة التعرّف على الجهاز.</summary>
public enum IdentityOutcome
{
    /// <summary>مفيش دليل هوية معتبر — إنشاء جهاز جديد مسموح.</summary>
    NoEvidence = 0,

    /// <summary>اتعرّفنا على جهاز واحد موجود.</summary>
    Resolved = 1,

    /// <summary>
    /// 🔴 مراسي قوية بتشاور على أجهزة مختلفة — <b>محتاج قرار بني
    /// آدم</b>.
    /// </summary>
    Ambiguous = 2,
}

/// <summary>مرساة واحدة بعد التطبيع، ومين بتشاور عليه.</summary>
public sealed record AnchorProbe(
    DeviceIdentifierKind Kind,
    string Raw,
    string Normalized,
    bool IsStrong,
    string Quality,
    IReadOnlyList<Guid> MatchedDevices)
{
    public bool Usable => Normalized.Length > 0;
}

/// <summary>قرار التعرّف + كل الأدلة اللي اتبني عليها.</summary>
public sealed record IdentityDecision(
    IdentityOutcome Outcome,
    Guid? DeviceId,
    string Reason,
    IReadOnlyList<AnchorProbe> Probes)
{
    /// <summary>سطور جاهزة للعرض في التشخيص وفي سجل المراجعة.</summary>
    public IEnumerable<string> Trace()
    {
        foreach (var probe in Probes)
        {
            string matched = probe.MatchedDevices.Count switch
            {
                0 => "مفيش مطابقة",
                1 => "جهاز واحد: " + probe.MatchedDevices[0],
                _ => $"⚠ {probe.MatchedDevices.Count} أجهزة",
            };

            yield return $"{probe.Kind,-14} raw=[{probe.Raw}] norm=[{probe.Normalized}] "
                + $"{(probe.IsStrong ? "قوية" : "مساعدة")} جودة={probe.Quality} ← {matched}";
        }

        yield return $"القرار: {Outcome}" + (DeviceId.HasValue ? $" → {DeviceId}" : "");
        yield return $"السبب: {Reason}";
    }
}

/// <summary>
/// التعرّف على الجهاز من مراسي الهوية — <b>القرار، من غير قاعدة
/// بيانات</b>.
///
/// <para>🔴 <b>ليه ده موجود أصلاً.</b> التعرّف كان بيحصل <b>على
/// الراكة وبس</b>، في قاعدتها المحلية. راكة ماشافتش اللاب ده قبل كده
/// مابتلاقيش أي مطابقة — فبتعمل جهاز جديد بكود جديد من بلوكها،
/// والسيرفر كان بياخد الجهاز ده زي ما هو من غير ما يقارن أي
/// مرساة.</para>
///
/// <para><b>واتقاس في الإنتاج:</b> نفس اللاب (LENOVO 81FK) اتفحص على
/// راكة فبقى <c>LP-00000501</c>، وبعدين على راكة تانية فبقى
/// <c>LP-00004001</c> — والاتنين مراسيهم <b>متطابقة بالحرف</b>:
/// UUID وBIOS وBoard والهارد. مفيش أي اختلاف يبرّر جهازين.</para>
///
/// <para>⚠️ <b>والمراسي زي ما هي — مفيش تغيير في معمارية الهوية.</b>
/// System UUID · BIOS · Board · سيريال الهارد. الـMAC والموديل
/// التجاري والرام والبطارية <b>مش</b> مراسي هوية ومش داخلين
/// هنا.</para>
/// </summary>
public static class DeviceIdentity
{
    /// <summary>
    /// المراسي القوية: دي اللي بتحدّد <b>اللاب نفسه</b>.
    ///
    /// <para>⚠️ سيريال الهارد <b>مش</b> فيها عن قصد. الهارد بيتبدّل،
    /// واللاب يفضل نفس الجهاز لما الهارد يتغيّر طول ما
    /// الـUUID/BIOS/Board لسه بيعرّفوه. لو عددناه قوي، <b>تبديل هارد
    /// كان هيبقى «تعارض» ويقفل الفحص</b>.</para>
    /// </summary>
    public static readonly IReadOnlyList<DeviceIdentifierKind> StrongKinds =
    [
        DeviceIdentifierKind.SystemUuid,
        DeviceIdentifierKind.BiosSerial,
        DeviceIdentifierKind.BoardSerial,
    ];

    public static bool IsStrong(DeviceIdentifierKind kind) => StrongKinds.Contains(kind);

    /// <summary>
    /// ⚠️ <b>خمسة كفاية.</b> إحنا بنفرّق بين «واحد» و«أكتر من واحد»
    /// — والعدّ الكامل رحلة زيادة على مرساة ممكن تكون على مية جهاز.
    /// </summary>
    public const int ProbeTake = 5;

    // =================================================================
    //  جودة القيمة
    // =================================================================

    public const string GradeValid = "صالحة";
    public const string GradeEmpty = "فاضية";
    public const string GradePlaceholder = "قيمة مصنع وهمية";
    public const string GradeEmptyAfterNormalize = "فاضية بعد التطبيع";
    public const string GradeBadUuid = "شكل UUID غير صالح";
    public const string GradeZeroUuid = "UUID أصفار";
    public const string GradeTooShort = "قصيرة جداً";

    /// <summary>
    /// جودة القيمة — <b>للتشخيص، مش لقرار المطابقة</b>.
    ///
    /// <para>⚠️ والقرار نفسه بياخد القيم اللي عدّت فلتر القيم
    /// الوهمية وبس — فالنصوص دي بتتعرض في التشخيص وفي سجل
    /// المراجعة، والمنطق بيقارن بـ<see cref="GradeValid"/>
    /// بس.</para>
    /// </summary>
    public static string Grade(DeviceIdentifierKind kind, string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return GradeEmpty;

        if (IdentityValues.IsPlaceholder(raw)) return GradePlaceholder;

        string norm = IdentityValues.Normalize(raw);

        if (norm.Length == 0) return GradeEmptyAfterNormalize;

        if (kind == DeviceIdentifierKind.SystemUuid)
        {
            if (!Guid.TryParse(norm, out var parsed)) return GradeBadUuid;
            if (parsed == Guid.Empty) return GradeZeroUuid;
        }

        return norm.Length < 4 ? GradeTooShort : GradeValid;
    }

    /// <summary>القيمة دي تنفع تتبني عليها هوية؟</summary>
    public static bool Usable(DeviceIdentifierKind kind, string? raw) =>
        Grade(kind, raw) == GradeValid;

    /// <summary>القيمة اللي المطابقة بتتعمل عليها — أو <c>""</c>.</summary>
    public static string MatchValue(DeviceIdentifierKind kind, string? raw) =>
        Usable(kind, raw) ? IdentityValues.Normalize(raw) : "";

    // =================================================================
    //  القرار
    // =================================================================

    /// <summary>
    /// بيحدّد الجهاز من نتايج المراسي.
    ///
    /// <para><b>وترتيب القرار:</b></para>
    ///
    /// <list type="number">
    /// <item>مرساة قوية واحدة بتشاور على جهاز واحد، ومفيش مرساة قوية
    /// تانية بتشاور على غيره ← <b>Resolved</b>.</item>
    ///
    /// <item>مراسي قوية بتشاور على أجهزة مختلفة، أو مرساة واحدة
    /// بتشاور على أكتر من جهاز ← <b>Ambiguous</b>. <b>ومابنعملش
    /// جهاز تالت.</b></item>
    ///
    /// <item>مفيش مرساة قوية بس الهارد بيشاور على جهاز واحد ←
    /// <b>Resolved</b> بدليل أضعف.</item>
    ///
    /// <item>مفيش أي دليل ← <b>NoEvidence</b>.</item>
    /// </list>
    /// </summary>
    public static IdentityDecision Decide(IReadOnlyList<AnchorProbe> probes)
    {
        var strong = probes.Where(p => p.IsStrong && p.Usable).ToList();

        /*
          🔴 **مرساة واحدة بتشاور على أكتر من جهاز = تكرار قايم
          بالفعل.**

          مابنختارش واحد عشوائي ومابنعملش جديد — بنوقف ونطلب قرار.
        */
        var split = strong.FirstOrDefault(p => p.MatchedDevices.Count > 1);

        if (split is not null)
        {
            return new IdentityDecision(
                IdentityOutcome.Ambiguous, null,
                $"المرساة {split.Kind} بتشاور على {split.MatchedDevices.Count} أجهزة موجودة — "
                + "محتاج قرار صريح قبل ربط أي فحص.",
                probes);
        }

        var strongHits = strong
            .Where(p => p.MatchedDevices.Count == 1)
            .Select(p => p.MatchedDevices[0])
            .Distinct()
            .ToList();

        // 🔴 مراسي قوية بتشاور على أجهزة مختلفة.
        if (strongHits.Count > 1)
        {
            return new IdentityDecision(
                IdentityOutcome.Ambiguous, null,
                $"مراسي قوية بتشاور على {strongHits.Count} أجهزة مختلفة — "
                + "ممنوع إنشاء جهاز تالت.",
                probes);
        }

        if (strongHits.Count == 1)
        {
            int agreed = strong.Count(p =>
                p.MatchedDevices.Count == 1 && p.MatchedDevices[0] == strongHits[0]);

            return new IdentityDecision(
                IdentityOutcome.Resolved, strongHits[0],
                $"{agreed} مرساة قوية متفقة على نفس الجهاز.",
                probes);
        }

        // مفيش مرساة قوية طابقت. الهارد دليل أضعف بس لسه معتبر.
        var disks = probes
            .Where(p => p.Kind == DeviceIdentifierKind.DiskSerial && p.Usable)
            .ToList();

        var diskHits = disks
            .Where(p => p.MatchedDevices.Count == 1)
            .Select(p => p.MatchedDevices[0])
            .Distinct()
            .ToList();

        if (disks.Any(p => p.MatchedDevices.Count > 1) || diskHits.Count > 1)
        {
            return new IdentityDecision(
                IdentityOutcome.Ambiguous, null,
                "سيريال الهارد بيشاور على أكتر من جهاز — محتاج قرار صريح.",
                probes);
        }

        if (diskHits.Count == 1)
        {
            return new IdentityDecision(
                IdentityOutcome.Resolved, diskHits[0],
                "سيريال الهارد طابق جهاز واحد، ومفيش مرساة قوية بتعارضه.",
                probes);
        }

        bool anyUsable = probes.Any(p => p.Usable);

        return new IdentityDecision(
            IdentityOutcome.NoEvidence, null,
            anyUsable
                ? "المراسي صالحة بس مفيش جهاز موجود بيطابقها — جهاز جديد."
                : "مفيش أي مرساة صالحة — جهاز جديد بهوية ضعيفة.",
            probes);
    }
}
