using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Handover;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using MediatR;

namespace Codlek.Application.Features.Handover.GetDestinations;

public sealed class GetHandoverDestinationsQueryHandler(
    IHandoverRepository handover,
    ICurrentUser me)
    : IRequestHandler<GetHandoverDestinationsQuery, Result<IReadOnlyList<HandoverDestination>>>
{
    public async Task<Result<IReadOnlyList<HandoverDestination>>> Handle(
        GetHandoverDestinationsQuery query, CancellationToken cancellationToken)
    {
        // ⚠️ النشطة بس: جهة موقوفة مابتتعرضش في القايمة، والتسليم
        // ليها بيترفض برسالة باسمها لو حد بعت معرّفها بنفسه.
        var rows = await handover.DestinationsAsync(me.TenantId, cancellationToken);

        return Result.Success(rows);
    }
}
