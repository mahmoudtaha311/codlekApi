using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Repairs;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Enums;
using MediatR;

namespace Codlek.Application.Features.Repairs.GetAssignableTechnicians;

public sealed class GetAssignableTechniciansQueryHandler(
    IRepairRepository repairs,
    ICurrentUser me)
    : IRequestHandler<GetAssignableTechniciansQuery,
        Result<IReadOnlyList<RepairTechnicianOption>>>
{
    public async Task<Result<IReadOnlyList<RepairTechnicianOption>>> Handle(
        GetAssignableTechniciansQuery query, CancellationToken cancellationToken)
    {
        var rows = await repairs.AssignableTechniciansAsync(me.TenantId, cancellationToken);

        return Result.Success<IReadOnlyList<RepairTechnicianOption>>(
            rows.Select(t => new RepairTechnicianOption(
                    t.Id,
                    t.Code,
                    t.DisplayName,
                    (int)t.Specialty,
                    TechnicianSpecialtyText.Arabic(t.Specialty)))
                .ToList());
    }
}
