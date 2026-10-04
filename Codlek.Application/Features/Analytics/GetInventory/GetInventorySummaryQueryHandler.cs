using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Analytics;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using MediatR;

namespace Codlek.Application.Features.Analytics.GetInventory;

public sealed class GetInventorySummaryQueryHandler(
    IAnalyticsRepository analytics,
    ICurrentUser me)
    : IRequestHandler<GetInventorySummaryQuery, Result<InventorySummary>>
{
    public async Task<Result<InventorySummary>> Handle(
        GetInventorySummaryQuery query, CancellationToken cancellationToken) =>
        Result.Success(await analytics.InventoryAsync(me.TenantId, cancellationToken));
}
