using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Handover;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Handover;
using MediatR;

namespace Codlek.Application.Features.Handover.GetCandidateIds;

public sealed class GetHandoverCandidateIdsQueryHandler(
    IHandoverRepository handover,
    ICurrentUser me)
    : IRequestHandler<GetHandoverCandidateIdsQuery, Result<HandoverCandidateIds>>
{
    public async Task<Result<HandoverCandidateIds>> Handle(
        GetHandoverCandidateIdsQuery query, CancellationToken cancellationToken)
    {
        /*
          🔴 **نفس بنّاء الفلتر بتاع القايمة بالحرف.**

          لو اتنين اختلفوا، «اختر كل اللي طلع» بيختار حاجة غير اللي
          الشاشة عارضاها — وده أسوأ من إنه مايشتغلش.
        */
        var filter = HandoverFilters.Candidates(query.Search, query.Container, query.Review);

        // ⚠️ القيمة بتتقص على السقف فمفيش طريق تطلب أكتر منه.
        int cap = HandoverPolicy.Cap(query.Take);

        var (ids, total) = await handover.CandidateIdsAsync(
            me.TenantId, filter, cap, cancellationToken);

        /*
          ⚠️ **ولما نقص، بنقول إننا قصّينا.**

          القص الصامت بيبان كأنك اخترت الكل وإنت لأ.
        */
        return Result.Success(new HandoverCandidateIds(ids, total, total > ids.Count));
    }
}
