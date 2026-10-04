namespace Codlek.Application.Contracts.Devices;

/// <summary>
/// مرساة هوية — <b>زي ما العتاد قالها</b>.
///
/// <para>🔴 <b>و<c>NormalizedValue</c> مش هنا.</b> دي قيمة داخلية
/// للمطابقة مش للعرض، وعرضها كان بيخلّي اللي بيقرا يفتكر إنها
/// السيريال الحقيقي.</para>
/// </summary>
/// <param name="IsActive">
/// 🔴 <c>false</c> = المرساة دي <b>اتلغت</b> — القطعة اتغيّرت أو
/// اتشالت.
///
/// <para>⚠️ <b>والملغية بتفضل بتتعرض.</b> هي جزء من تاريخ اللاب مش
/// زبالة: هي اللي بتفسّر ليه بوردة اتغيّرت أو هارد اتبدّل. إخفاؤها
/// بيخلّي الصفحة تقول حاجة ناقصة.</para>
/// </param>
public sealed record DeviceIdentifierItem(
    string Kind,
    string KindText,
    string RawValue,
    string Source,
    string Confidence,
    bool IsActive,
    DateTime FirstSeenAtUtc,
    DateTime LastSeenAtUtc);
