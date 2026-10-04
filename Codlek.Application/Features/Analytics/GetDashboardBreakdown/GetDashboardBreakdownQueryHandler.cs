using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Analytics;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Analytics;
using Codlek.Core.Enums;
using MediatR;

namespace Codlek.Application.Features.Analytics.GetDashboardBreakdown;

public sealed class GetDashboardBreakdownQueryHandler(
    IAnalyticsRepository analytics,
    ICurrentUser me)
    : IRequestHandler<GetDashboardBreakdownQuery, Result<DashboardBreakdownResponse>>
{
    private const int TopTake = 8;

    public async Task<Result<DashboardBreakdownResponse>> Handle(
        GetDashboardBreakdownQuery query, CancellationToken cancellationToken)
    {
        var period = AnalyticsPeriod.Resolve(query.Range, query.From, query.To);
        string? technician = AnalyticsScope.TechnicianCode(me);

        var technicians = await analytics.TechnicianActivityAsync(
            me.TenantId, technician, period, TopTake, cancellationToken);

        var racks = await analytics.RackActivityAsync(
            me.TenantId, technician, period, cancellationToken);

        var failures = await analytics.FailureHotspotsAsync(
            me.TenantId, technician, period, TopTake, cancellationToken);

        var buckets = await analytics.DurationBucketsAsync(
            me.TenantId, technician, period, cancellationToken);

        var devices = await analytics.DeviceMixAsync(
            me.TenantId, technician, period, cancellationToken);

        return Result.Success(new DashboardBreakdownResponse(
            AnalyticsScope.Info(period),
            technicians,

            // ⚠️ النص العربي بيتحسب هنا — ترجمة جوّه الإسقاط بترمي
            // على قاعدة حقيقية.
            racks
                .Select(r => new RackActivityItem(
                    r.Id, r.Code, r.Name,
                    r.Status.ToString(), RackStatusText.Arabic(r.Status),
                    r.Reports, r.LastSeenAtUtc))
                .ToList(),

            failures,

            // 🔴 الترتيب عقد: الواجهة بترسم الأعمدة باللي بيوصلها،
            // فالقايمة والأرقام لازم يتلمّوا بنفس الترتيب.
            DurationBucket.All()
                .Select((b, i) => new DurationBucketItem(
                    b.Key, b.Label, i < buckets.Count ? buckets[i] : 0))
                .ToList(),

            devices));
    }
}
