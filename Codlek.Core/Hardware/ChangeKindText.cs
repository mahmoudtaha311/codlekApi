namespace Codlek.Core.Hardware;

/// <summary>
/// اسم نتيجة المقارنة ونبرتها.
///
/// <para>🔴 <b>الفرق المؤكّد بس هو اللي بياخد أحمر.</b> «مش
/// مؤكّدة» و«ماتفحصتش» <b>مش</b> تغيير — لونهم رمادي، ولو اتلوّنوا
/// أحمر الشاشة بتقول إن القطعة اتغيّرت وهي مش قادرة تثبت
/// ده.</para>
///
/// <para>⚠️ <b>والنبرة اسم محايد مش اسم كلاس CSS.</b> المشروع
/// القديم كان عنده نسختين: <c>KindCss</c> بترجّع
/// <c>"navy"/"skip"</c> لصفحات Razor، والنقطة بترجّع
/// <c>"info"/"muted"</c> للواجهة الجديدة. اللي اتنقل هو <b>نسخة
/// الواجهة الجديدة</b> بس — هي اللي الداش بورد بتقراها.</para>
/// </summary>
public static class ChangeKindText
{
    public static string Arabic(ChangeKind kind) => kind switch
    {
        ChangeKind.Unchanged => "زي ما هي",
        ChangeKind.Added => "اتضافت",
        ChangeKind.Removed => "اتشالت",
        ChangeKind.SerialChanged => "السيريال اتغيّر",
        ChangeKind.ModelChanged => "اسم الموديل اتغيّر",
        ChangeKind.ConfigurationDifferent => "التكوين مختلف",
        ChangeKind.IdentityUncertain => "الهوية مش مؤكّدة",
        ChangeKind.NotObserved => "ماتفحصتش",

        // ⚠️ قيمة جديدة من نسخة أحدث بتنزل هنا بدل ما ترمي.
        _ => kind.ToString()
    };

    /// <summary>
    /// نبرة العرض: <c>good</c> · <c>bad</c> · <c>info</c> ·
    /// <c>muted</c>.
    /// </summary>
    public static string Tone(ChangeKind kind) => kind switch
    {
        ChangeKind.Unchanged => "good",

        // 🔴 التلاتة دول بس — وكلهم بيحتاجوا دليل قاطع.
        ChangeKind.SerialChanged or ChangeKind.Removed or ChangeKind.Added => "bad",

        ChangeKind.ModelChanged or ChangeKind.ConfigurationDifferent => "info",

        _ => "muted"
    };
}
