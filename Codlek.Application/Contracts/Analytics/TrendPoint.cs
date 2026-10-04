namespace Codlek.Application.Contracts.Analytics;

/// <summary>
/// نقطة في منحنى الحجم.
///
/// <para>🔴 <b>الفجوات بتتملّي بصفر.</b> لو رجّعنا الأيام اللي فيها
/// شغل بس، المنحنى بيوصّل يوم الأحد باللي بعده الخميس بخط مستقيم —
/// والقارئ بيشوف شغل مستمر في أيام ما اشتغلش فيها حد. الصفر الصريح
/// هو الحقيقة.</para>
/// </summary>
/// <param name="Bucket">مفتاح للترتيب: <c>yyyy-MM-dd</c> أو ساعة بخانتين.</param>
/// <param name="Label">نص للعرض: <c>dd/MM</c> أو <c>HH:00</c>.</param>
public sealed record TrendPoint(
    string Bucket,
    string Label,
    int Total,
    int Clean,
    int NeedsReview);
