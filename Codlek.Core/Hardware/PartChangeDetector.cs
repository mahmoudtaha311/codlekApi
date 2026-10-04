namespace Codlek.Core.Hardware;

/// <summary>
/// «فيه جهاز اتغيّرت فيه قطعة» — <b>الحكم، من غير قاعدة بيانات</b>.
///
/// <para>🔴 <b>الكلاس ده بيبني اتهام.</b> السطر اللي بيطلع منه بيظهر
/// كأيقونة تحذير فوق في اللوحة، ومعناه العملي إن حد فتح اللاب وغيّر
/// فيه حاجة. فالقواعد هنا متشددة عن قصد، وكل استثناء مكتوب
/// سببه.</para>
///
/// <para>⚠️ <b>ودالة نقية وبرّه الاستقبال عن قصد</b> — عشان تتجرّب
/// على لقطتين في الذاكرة من غير سيرفر ولا قاعدة. الحساب وقت
/// الاستقبال قرار أداء، بس الحكم نفسه مالوش علاقة بالتوقيت ده.</para>
/// </summary>
public static class PartChangeDetector
{
    /// <summary>
    /// أنواع الفروقات اللي معناها <b>قطعة اتبدّلت فعلاً</b>.
    ///
    /// <para>🔴 <c>ModelChanged</c> <b>مش</b> منهم — المقارنة نفسها
    /// بتقول إنه «غالباً فرق في القراءة أو الفيرموير».
    /// و<c>IdentityUncertain</c> و<c>NotObserved</c>
    /// و<c>ConfigurationDifferent</c> كلهم معناهم «مقدرناش نتأكد»،
    /// والتحذير المبني على «مقدرناش نتأكد» <b>اتهام</b>.</para>
    ///
    /// <para>⚠️ و<c>MajorIdentityWarning</c> مش المقياس: هو بيغطّي
    /// معلومات النظام واللوحة الأم بس — المعالج والرام والهارد
    /// والبطارية وكارت الشاشة كلهم برّه. هو سؤال «ده نفس الجهاز
    /// أصلاً؟» مش «قطعة اتبدّلت؟».</para>
    /// </summary>
    private static readonly HashSet<ChangeKind> RealChange =
    [
        ChangeKind.Added,
        ChangeKind.Removed,
        ChangeKind.SerialChanged,
    ];

    /// <summary>أطول ملخّص — <b>نفس طول عمود <c>PartChangeSummary</c></b>.</summary>
    public const int MaxSummary = 300;

    /// <summary>نتيجة الحكم.</summary>
    /// <param name="Count">عدد الفروقات اللي معناها تبديل. صفر = مفيش تحذير.</param>
    /// <param name="Summary">وصف قصير للعرض، أو فاضي.</param>
    public readonly record struct Verdict(int Count, string Summary)
    {
        public bool Changed => Count > 0;
    }

    /// <summary>قارن لقطتين وقول: فيه قطعة اتبدّلت ولا لأ.</summary>
    /// <param name="previous">لقطة الفحص الأقدم.</param>
    /// <param name="current">لقطة الفحص الأحدث.</param>
    /// <param name="previousPartial">اللقطة الأقدم ناقصة؟</param>
    /// <param name="currentPartial">اللقطة الأحدث ناقصة؟</param>
    public static Verdict Compare(
        IReadOnlyList<Entities.ReportSnapshotComponent> previous,
        IReadOnlyList<Entities.ReportSnapshotComponent> current,
        bool previousPartial,
        bool currentPartial)
    {
        /*
          🔴 **لقطة ناقصة عمرها ما تتهم.**

          اللقطة الناقصة معناها إن البرنامج اشتغل من غير صلاحيات
          مسؤول أو إن قراءة فشلت، والغياب فيها مايتقاسش عليه.
          `SnapshotComparison` بيحترم الأعلام دي، بس بنقف هنا كمان
          عشان الحكم يبقى **صريح** مش نتيجة جانبية لتفاصيل في ملف
          تاني.
        */
        if (previousPartial || currentPartial) return new Verdict(0, "");

        /*
          ⚠️ **مفيش لقطة على ناحية = مفيش مقارنة، مش «اتشال كل
          حاجة».**

          🔴 **والشرط ده زيادة فعلاً — قسناه.**
          `SnapshotComparison` بيقف لوحده: الفئة اللي مافيهاش صفوف في
          الناحية التانية بتدّي `NotObserved` مش `Removed`. شيلنا
          الشرط ده بتحوير مقصود والفحوص كلها عدّت.

          ⚠️ **وسايبينه بردو** عشان الحكم يبقى صريح هنا مش نتيجة
          جانبية لملف تاني — بس **الشرط اللي فوقه مش زيادة**: شيل
          حارس اللقطة الناقصة وهي بتطلّع `Removed` فعلاً (التحوير
          اتلقط). فما تفتكرش إن الاتنين نفس الحاجة.
        */
        if (previous.Count == 0 || current.Count == 0) return new Verdict(0, "");

        var diffs = SnapshotComparison
            .Compare(previous, current, previousPartial, currentPartial)
            .Where(d => RealChange.Contains(d.Kind))
            .ToList();

        return diffs.Count == 0
            ? new Verdict(0, "")
            : new Verdict(diffs.Count, Describe(diffs));
    }

    /// <summary>
    /// وصف قصير: «اتشال: هارد · اتضاف: رام».
    ///
    /// <para>⚠️ <b>مقصوص على <see cref="MaxSummary"/></b> عشان يطابق
    /// طول العمود. والقص بيحصل هنا مش عند الحفظ، عشان مايرميش
    /// استثناء في نص الاستقبال.</para>
    /// </summary>
    public static string Describe(IReadOnlyList<ComponentDiff> diffs)
    {
        var parts = diffs
            .GroupBy(d => d.Kind)
            .OrderBy(g => (int)g.Key)
            .Select(g => KindText(g.Key) + ": " + string.Join("، ",
                g.Select(d => TypeText(d.Type)).Distinct().OrderBy(t => t, StringComparer.Ordinal)));

        string text = string.Join(" · ", parts);

        return text.Length <= MaxSummary ? text : text[..(MaxSummary - 3)] + "…";
    }

    private static string KindText(ChangeKind kind) => kind switch
    {
        ChangeKind.Added => "اتضاف",
        ChangeKind.Removed => "اتشال",
        ChangeKind.SerialChanged => "اتبدّل",
        _ => "اتغيّر",
    };

    /// <summary>
    /// اسم الفئة بالعربي — <b>وبكلام الفني مش بعناوين الصفحة</b>.
    ///
    /// <para>⚠️ <b>ودي خريطة لوحدها مش <see cref="ComponentType.Arabic"/>
    /// عن قصد.</b> صفحة العتاد بتقول «وحدات التخزين» و«الشاشات» —
    /// عناوين أقسام. والتحذير ده سطر واحد جوّه أيقونة، فبيقول
    /// «الهارد» و«الشاشة» زي ما الفني بيقول. خلطهم بيطوّل السطر
    /// ويخلّيه يتقص.</para>
    /// </summary>
    public static string TypeText(int type) => type switch
    {
        ComponentType.Cpu => "المعالج",
        ComponentType.Memory => "الرام",
        ComponentType.Storage => "الهارد",
        ComponentType.Battery => "البطارية",
        ComponentType.Display => "الشاشة",
        ComponentType.Gpu => "كارت الشاشة",
        ComponentType.Network => "كارت الشبكة",
        _ => "قطعة",
    };
}
