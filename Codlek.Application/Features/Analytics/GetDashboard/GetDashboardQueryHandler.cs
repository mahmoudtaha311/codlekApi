using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Analytics;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Analytics;
using MediatR;

namespace Codlek.Application.Features.Analytics.GetDashboard;

public sealed class GetDashboardQueryHandler(
    IAnalyticsRepository analytics,
    ICurrentUser me)
    : IRequestHandler<GetDashboardQuery, Result<DashboardResponse>>
{
    /// <summary>
    /// ⚠️ تمن صفوف في كل قايمة — القايمة الأطول بتزقّ الرسم البياني
    /// تحت الشاشة.
    /// </summary>
    private const int ListSize = 8;

    public async Task<Result<DashboardResponse>> Handle(
        GetDashboardQuery query, CancellationToken cancellationToken)
    {
        var period = AnalyticsPeriod.Resolve(query.Range, query.From, query.To);
        string? technician = AnalyticsScope.TechnicianCode(me);

        var (kpis, counts) = await analytics.KpisAsync(
            me.TenantId, technician, period,

            // 🔴 عدّاد الراكات على مستوى الشركة — للمدير وبس.
            includeStations: me.IsManagerOrAbove,
            cancellationToken);

        var trend = await analytics.TrendAsync(
            me.TenantId, technician, period, cancellationToken);

        var (attention, recent) = await analytics.ListsAsync(
            me.TenantId, technician, period, ListSize, cancellationToken);

        return Result.Success(new DashboardResponse(
            AnalyticsScope.Info(period), kpis, counts, trend, attention, recent));
    }
}
