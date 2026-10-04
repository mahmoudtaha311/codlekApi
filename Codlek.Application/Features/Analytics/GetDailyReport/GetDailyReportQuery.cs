using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Analytics;
using MediatR;

namespace Codlek.Application.Features.Analytics.GetDailyReport;

/// <summary>
/// تقرير يوم واحد.
///
/// <para>⚠️ <c>null</c> = النهاردة بتوقيت القاهرة.</para>
/// </summary>
public sealed record GetDailyReportQuery(DateTime? Day)
    : IRequest<Result<DailyReportResponse>>;
