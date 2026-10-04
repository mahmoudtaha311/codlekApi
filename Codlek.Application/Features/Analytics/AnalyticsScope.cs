using Codlek.Application.Contracts.Analytics;
using Codlek.Application.Interfaces;
using Codlek.Core.Analytics;

namespace Codlek.Application.Features.Analytics;

/// <summary>
/// تضييق التحليلات على الفني — <b>مكان واحد</b>.
///
/// <para>🔴 <b>الفني بيشوف شغله هو، والترشيح بيتعمل في السيرفر مش
/// في الواجهة</b> — عشان محدش يوصل لبيانات غيره بتغيير رابط.</para>
///
/// <para>⚠️ وكل معالج تحليلات بينده الدالة دي: لو واحد نساها، فتحة
/// صفحة واحدة بتفتح شغل الورشة كله لفني.</para>
/// </summary>
internal static class AnalyticsScope
{
    /// <summary>
    /// كود الفني اللي بيتضيّق عليه — <c>null</c> للمدير وفوق.
    /// </summary>
    public static string? TechnicianCode(ICurrentUser me) =>
        me.IsManagerOrAbove ? null : me.Code;

    public static PeriodInfo Info(AnalyticsPeriod period) =>
        new(
            period.Key,
            period.Label,
            period.FirstCairoDay.ToString("yyyy-MM-dd"),
            period.LastCairoDay.ToString("yyyy-MM-dd"),
            period.Days,
            period.Hourly);
}
