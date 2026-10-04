namespace Codlek.Application.Contracts.Handover;

/// <summary>
/// لاب <b>ينفع</b> يتسلّم.
///
/// <para>🔴 <b>وجوده في القايمة دي مش تصريح.</b> القايمة اقتراح،
/// والسيرفر بيعيد التحقّق من أهلية كل لاب وقت التسليم مهما كانت
/// القايمة جات منين.</para>
/// </summary>
/// <param name="ReadyAtUtc">
/// 🔴 <c>null</c> = <b>محدش راجعه</b>.
///
/// <para>«عدّى الفحص» حكم آلي، و«جاهز يمشي» حكم بني آدم (اتنضّف؟
/// اتغلّف؟ اتلزق عليه ليبل؟) — والحاجات دي مالهاش أثر في أي بيانات،
/// فمحدش غير بني آدم يقدر يقول عليها.</para>
/// </param>
/// <param name="ReadyByName">
/// ⚠️ ومين قاله — مش مجرد علم صح/غلط.
/// </param>
public sealed record HandoverCandidate(
    Guid Id,
    string PublicCode,
    string Manufacturer,
    string Model,
    string ContainerCode,
    string Stage,
    string StageText,
    string LocationName,
    DateTime? LastTestAtUtc,
    DateTime? ReadyAtUtc,
    string ReadyByName);
