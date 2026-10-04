namespace Codlek.Core.Reports;

/// <summary>
/// نبرة نتيجة المرحلة.
///
/// <para>⚠️ نبرة مش اسم كلاس CSS — الواجهة بتترجمها لألوانها
/// هي.</para>
/// </summary>
public static class StepOutcomeTone
{
    public const string Good = "good";
    public const string Bad = "bad";

    /// <summary>مش واضح — «مش موجود» و«تعذّر التنفيذ».</summary>
    public const string Unclear = "unclear";

    public const string Neutral = "neutral";
}
