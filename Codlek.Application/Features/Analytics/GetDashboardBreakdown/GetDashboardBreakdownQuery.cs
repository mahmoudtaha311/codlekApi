using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Analytics;
using MediatR;

namespace Codlek.Application.Features.Analytics.GetDashboardBreakdown;

/// <summary>تفصيل اللوحة — خمس تجميعات في نداء تاني.</summary>
public sealed record GetDashboardBreakdownQuery(string? Range, DateTime? From, DateTime? To)
    : IRequest<Result<DashboardBreakdownResponse>>;
