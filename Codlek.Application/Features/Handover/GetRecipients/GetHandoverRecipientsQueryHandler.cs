using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Handover;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Time;
using MediatR;

namespace Codlek.Application.Features.Handover.GetRecipients;

public sealed class GetHandoverRecipientsQueryHandler(
    IHandoverRepository handover,
    ICurrentUser me)
    : IRequestHandler<GetHandoverRecipientsQuery, Result<IReadOnlyList<HandoverRecipientItem>>>
{
    public async Task<Result<IReadOnlyList<HandoverRecipientItem>>> Handle(
        GetHandoverRecipientsQuery query, CancellationToken cancellationToken)
    {
        /*
          🔴 **المدى نصف مفتوح وبيحترم التوقيت الصيفي.**

          `CairoDay` بتحسب بداية اليوم بتوقيت القاهرة الحقيقي، مش
          بـ`+2` ثابتة. ومصر بتقدّم الساعة **نص الليل**.
        */
        var rows = await handover.RecipientsAsync(
            me.TenantId,
            CairoDay.StartUtc(query.From),
            CairoDay.AfterUtc(query.To),
            cancellationToken);

        return Result.Success(rows);
    }
}
