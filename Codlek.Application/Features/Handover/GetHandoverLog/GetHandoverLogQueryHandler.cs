using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Common;
using Codlek.Application.Contracts.Handover;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Paging;
using Codlek.Core.Text;
using Codlek.Core.Time;
using MediatR;

namespace Codlek.Application.Features.Handover.GetHandoverLog;

public sealed class GetHandoverLogQueryHandler(
    IHandoverRepository handover,
    ICurrentUser me)
    : IRequestHandler<GetHandoverLogQuery, Result<PagedResult<HandoverLogItem>>>
{
    public async Task<Result<PagedResult<HandoverLogItem>>> Handle(
        GetHandoverLogQuery query, CancellationToken cancellationToken)
    {
        var (page, size) = Paging.Clamp(query.Page, query.PageSize);

        string receiver = (query.Receiver ?? "").Trim();

        var filter = new HandoverLogFilter
        {
            FromUtc = CairoDay.StartUtc(query.From),
            ToUtc = CairoDay.AfterUtc(query.To),
            DestinationId = query.Destination,

            /*
              ⚠️ **ومفيش توحيد عربي على اسم المستلم — منقول زي ما
              هو.**

              القديم بيهرّب النص وبس. والتوحيد كان بيلاقي أسماء
              القديم مابيلاقيهاش، فنفس البحث يدّي نتايج مختلفة من
              الشاشتين وهما على نفس القاعدة.
            */
            ReceiverPattern = receiver.Length == 0
                ? null
                : SearchPattern.Contains(receiver),

            Page = page,
            PageSize = size,
        };

        var (rows, total) = await handover.LogAsync(me.TenantId, filter, cancellationToken);

        return Result.Success(new PagedResult<HandoverLogItem>(
            Items: rows,
            Page: page,
            PageSize: size,
            TotalItems: total,
            TotalPages: Paging.TotalPages(total, size)));
    }
}
