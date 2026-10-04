using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Analytics;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Analytics;
using Codlek.Core.Enums;
using Codlek.Core.Time;
using MediatR;

namespace Codlek.Application.Features.Analytics.GetDailyReport;

public sealed class GetDailyReportQueryHandler(
    IAnalyticsRepository analytics,
    ICurrentUser me)
    : IRequestHandler<GetDailyReportQuery, Result<DailyReportResponse>>
{
    /// <summary>
    /// ⚠️ عشرة صفوف في القوايم و٢٥ فني و١٢ عطل — ده تقرير بيتطبع
    /// ويتسلّم، مش شاشة.
    /// </summary>
    private const int ListSize = 10;
    private const int TechnicianTake = 25;
    private const int FailureTake = 12;
    private const int PartTake = 12;

    public async Task<Result<DailyReportResponse>> Handle(
        GetDailyReportQuery query, CancellationToken cancellationToken)
    {
        var target = (query.Day ?? CairoDay.Today).Date;
        var period = AnalyticsPeriod.SingleDay(target);

        string? technician = AnalyticsScope.TechnicianCode(me);

        var (kpis, counts) = await analytics.KpisAsync(
            me.TenantId, technician, period,
            includeStations: me.IsManagerOrAbove, cancellationToken);

        // ⚠️ فترة يوم واحد = تجميع بالساعة، وده اللي بيخلّي «الشغل
        // بيتعمل امتى» سؤال له إجابة.
        var hourly = await analytics.TrendAsync(
            me.TenantId, technician, period, cancellationToken);

        var (attention, recent) = await analytics.ListsAsync(
            me.TenantId, technician, period, ListSize, cancellationToken);

        long totalMs = await analytics.TotalDurationMsAsync(
            me.TenantId, technician, period, cancellationToken);

        /*
          🔴 **عدد الممسوح للمدير وبس.**

          ده رقم إداري — «المدير مسح كام فحص النهاردة» — وكان بيتحسب
          على الشركة كلها من غير تضييق، فالفني كان بيشوفه جمب صفحة
          بتقول له «بتشوف فحوصاتك إنت بس».
        */
        int deleted = me.IsManagerOrAbove
            ? await analytics.DeletedCountAsync(me.TenantId, period, cancellationToken)
            : 0;

        var technicians = await analytics.TechnicianActivityAsync(
            me.TenantId, technician, period, TechnicianTake, cancellationToken);

        var racks = await analytics.RackActivityAsync(
            me.TenantId, technician, period, cancellationToken);

        var failures = await analytics.FailureHotspotsAsync(
            me.TenantId, technician, period, FailureTake, cancellationToken);

        var parts = await analytics.ReportPartsAsync(
            me.TenantId, technician, period, PartTake, cancellationToken);

        return Result.Success(new DailyReportResponse(
            AnalyticsScope.Info(period),
            ArabicDate.Long(target),
            target.AddDays(-1).ToString("yyyy-MM-dd"),

            // ⚠️ بكرة مالوش بيانات — فزرار «اللي بعده» بيتقفل بـ`null`.
            target < CairoDay.Today ? target.AddDays(1).ToString("yyyy-MM-dd") : null,

            kpis,
            counts,
            deleted,

            // ⚠️ بالساعات لأن ده رقم بيتسلّم للإدارة؛ المللي ثانية
            // مالهاش معنى في تقرير مطبوع.
            Math.Round(totalMs / 3_600_000.0, 1),

            hourly,
            technicians,

            racks
                .Select(r => new RackActivityItem(
                    r.Id, r.Code, r.Name,
                    r.Status.ToString(), RackStatusText.Arabic(r.Status),
                    r.Reports, r.LastSeenAtUtc))
                .ToList(),

            failures,
            parts,
            attention,
            recent));
    }
}
