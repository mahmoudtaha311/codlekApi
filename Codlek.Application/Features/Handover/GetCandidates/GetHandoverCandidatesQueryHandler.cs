using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Common;
using Codlek.Application.Contracts.Handover;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Enums;
using Codlek.Core.Paging;
using MediatR;

namespace Codlek.Application.Features.Handover.GetCandidates;

public sealed class GetHandoverCandidatesQueryHandler(
    IHandoverRepository handover,
    ICurrentUser me)
    : IRequestHandler<GetHandoverCandidatesQuery, Result<PagedResult<HandoverCandidate>>>
{
    public async Task<Result<PagedResult<HandoverCandidate>>> Handle(
        GetHandoverCandidatesQuery query, CancellationToken cancellationToken)
    {
        var filter = HandoverFilters.Candidates(
            query.Search, query.Container, query.Review, query.Page, query.PageSize);

        var (rows, total) = await handover.CandidatesAsync(
            me.TenantId, filter, cancellationToken);

        /*
          ⚠️ **النصوص العربية بتتحسب هنا، بعد القراية.**

          أي ترجمة جوّه الإسقاط بتترجم عادي وبتعدّي فحوص الوحدة،
          وبترمي على قاعدة حقيقية.
        */
        var items = rows
            .Select(r => new HandoverCandidate(
                Id: r.Id,
                PublicCode: r.PublicCode,
                Manufacturer: r.Manufacturer,
                Model: r.Model,
                ContainerCode: r.ContainerCode,

                // ⚠️ الاسم كنص للواجهة، والنص العربي للعرض.
                Stage: r.Stage.ToString(),
                StageText: DeviceOperationalStageText.Arabic(r.Stage),

                LocationName: r.LocationName,
                LastTestAtUtc: r.LastTestAtUtc,
                ReadyAtUtc: r.ReadyAtUtc,
                ReadyByName: r.ReadyByName))
            .ToList();

        return Result.Success(new PagedResult<HandoverCandidate>(
            Items: items,
            Page: filter.Page,
            PageSize: filter.PageSize,
            TotalItems: total,
            TotalPages: Paging.TotalPages(total, filter.PageSize)));
    }
}
