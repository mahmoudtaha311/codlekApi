namespace Codlek.Core.Analytics;

/// <summary>
/// شرايح مدة الفحص — <b>الحدود والأسماء</b>.
///
/// <para>⚠️ <b>الحدود بالمللي ثانية عشان الاستعلام يقدر يقسّم جوّه
/// SQL.</b> أي تحويل لدقايق في الاستعلام بيمنع استعمال الفهرس.</para>
///
/// <para>⚠️ <b>والفحوص اللي مالهاش مدة مابتدخلش خالص.</b> الفحوص
/// المستوردة من شيتات قديمة مدّتها صفر، وإدخالها في الشريحة الأولى
/// كان بيقول إن «أغلب الفحوص أقل من ٥ دقايق».</para>
/// </summary>
public static class DurationBucket
{
    public const long UnderFive = 300_000;
    public const long UnderTen = 600_000;
    public const long UnderTwenty = 1_200_000;

    /// <summary>
    /// 🔴 <b>المفتاح للواجهة والنص للعرض — والترتيب عقد.</b>
    /// الواجهة بترسم الأعمدة بالترتيب اللي بيوصلها.
    /// </summary>
    public static IReadOnlyList<(string Key, string Label)> All() =>
    [
        ("under5", "أقل من ٥ دقايق"),
        ("5to10", "٥ – ١٠ دقايق"),
        ("10to20", "١٠ – ٢٠ دقيقة"),
        ("over20", "أكتر من ٢٠ دقيقة"),
    ];
}
