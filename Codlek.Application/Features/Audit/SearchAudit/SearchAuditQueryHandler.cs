using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Audit;
using Codlek.Application.Contracts.Common;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Paging;
using Codlek.Core.Text;
using Codlek.Core.Time;
using MediatR;

namespace Codlek.Application.Features.Audit.SearchAudit;

public sealed class SearchAuditQueryHandler(
    IAuditRepository audit,
    ICurrentUser me)
    : IRequestHandler<SearchAuditQuery, Result<PagedResult<AuditEventItem>>>
{
    public async Task<Result<PagedResult<AuditEventItem>>> Handle(
        SearchAuditQuery query, CancellationToken cancellationToken)
    {
        /*
          🔴 **الفلتر بيتبني من مكان مشترك مع التصدير** — الملف
          اللي بيطلع بصفوف غير اللي قدام المالك أسوأ من مفيش ملف.
        */
        var filter = AuditSearchFilters.Build(
            query.From, query.To, query.Action, query.EntityType,
            query.Actor, query.Search, query.Page, query.PageSize);

        int page = filter.Page;
        int size = filter.PageSize;

        var (rows, total) = await audit.SearchAsync(me.TenantId, filter, cancellationToken);

        var items = rows
            .Select(e => new AuditEventItem(
                e.Id,
                e.OccurredAtUtc,
                e.ActorType,
                AuditLabels.Actor(e.ActorType),
                e.ActorName,
                e.Action,

                /*
                  ⚠️ **الكود المجهول بيرجع بنفسه، مش بـ«غير معروف».**

                  سطر سجل مالوش ترجمة لازم يفضل **مقروء** — إخفاؤه
                  بيخلّي الرقابة ناقصة من غير ما حد يعرف.
                */
                AuditLabels.Action(e.Action),

                e.EntityType,
                AuditLabels.Entity(e.EntityType),
                e.EntityCode,
                e.EntityId,
                e.Summary,
                e.Ip))
            .ToList();

        return Result.Success(new PagedResult<AuditEventItem>(
            Items: items,
            Page: page,
            PageSize: size,
            TotalItems: total,
            TotalPages: Paging.TotalPages(total, size)));
    }
}
