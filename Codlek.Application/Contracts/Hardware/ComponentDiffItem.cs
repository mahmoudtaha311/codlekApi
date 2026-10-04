namespace Codlek.Application.Contracts.Hardware;

/// <summary>صف في جدول المقارنة.</summary>
/// <param name="Kind">
/// 🔴 <b>اسم القيمة كنص</b> — <c>"SerialChanged"</c>. الواجهة
/// بتتفرّع عليه، فإعادة التسمية هي اللي بتكسر مش إعادة الترقيم.
/// </param>
/// <param name="Tone">
/// ⚠️ نبرة مش اسم كلاس CSS: <c>good</c> · <c>bad</c> ·
/// <c>info</c> · <c>muted</c>.
/// </param>
public sealed record ComponentDiffItem(
    int Type,
    string TypeText,
    string Kind,
    string KindText,
    string Tone,
    string MatchedBy,
    string Explanation,
    bool StrongBothSides,
    HardwareComponentItem? Left,
    HardwareComponentItem? Right);
