using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Technicians;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using MediatR;

namespace Codlek.Application.Features.TechnicianAccounts.GetAccounts;

public sealed class GetTechnicianAccountsQueryHandler(
    ITechnicianAccountRepository accounts,
    ICurrentUser me)
    : IRequestHandler<GetTechnicianAccountsQuery, Result<IReadOnlyList<TechnicianAccount>>>
{
    public async Task<Result<IReadOnlyList<TechnicianAccount>>> Handle(
        GetTechnicianAccountsQuery query, CancellationToken cancellationToken)
    {
        var rows = await accounts.ListAsync(me.TenantId, cancellationToken);

        // ⚠️ قراية واحدة لكل الربط — مش قراية لكل فني.
        var links = await accounts.BrandLinksAsync(me.TenantId, cancellationToken);

        var items = rows
            .Select(t => TechnicianAccountMapping.Account(
                t,

                /*
                  🔴 **القايمة الفاضية مش `null`.**

                  الفني اللي مالوش ربط لازم يرجع `[]` — عشان الواجهة
                  تفرّق بين «محمّلة ومفيش قيد» و«المسار ده مابيحمّلش
                  الربط».
                */
                links.TryGetValue(t.Id, out var ids) ? ids : []))
            .ToList();

        return Result.Success<IReadOnlyList<TechnicianAccount>>(items);
    }
}
