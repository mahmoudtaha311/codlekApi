using Codlek.Core.Entities;

namespace Codlek.Core.Hardware;

/// <summary>صف واحد في المقارنة.</summary>
public sealed class ComponentDiff
{
    public int Type { get; init; }

    public ChangeKind Kind { get; init; }

    /// <summary>الجهة الأقدم — <c>null</c> لو القطعة ظهرت في الأحدث بس.</summary>
    public ReportSnapshotComponent? A { get; init; }

    /// <summary>الجهة الأحدث — <c>null</c> لو القطعة اختفت.</summary>
    public ReportSnapshotComponent? B { get; init; }

    /// <summary>
    /// إزاي اتطابقوا — <b>للشفافية، عشان القارئ يحكم بنفسه</b>.
    ///
    /// <para>⚠️ ده مش تفصيل تجميلي: «سيريال» و«بصمة/موديل» فرقهم هو
    /// الفرق بين دليل واحتمال، واللي بيقرا الشاشة لازم يشوفه.</para>
    /// </summary>
    public string MatchedBy { get; init; } = "";

    /// <summary>
    /// شرح بلغة بني آدم — <b>بيوزن الكلام حسب قوة الدليل</b>.
    /// </summary>
    public string Explanation { get; init; } = "";

    /// <summary>
    /// الطرفين ثقة أ؟
    ///
    /// <para>🔴 ده الشرط الوحيد اللي بيسمح بكلام عن «قطعة
    /// اتغيّرت».</para>
    /// </summary>
    public bool StrongBothSides { get; init; }
}
