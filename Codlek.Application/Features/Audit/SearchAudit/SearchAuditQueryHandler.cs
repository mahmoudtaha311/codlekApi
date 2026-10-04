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
    /// <summary>
    /// ⚠️ <b>٤٠ مش ٢٥</b> — ده سجل بيتقرا بالتمرير، والصفحة
    /// الصغيرة بتخلّي اللي بيراجع يدوس «بعده» عشرين مرة.
    /// </summary>
    private const int DefaultPageSize = 40;

    public async Task<Result<PagedResult<AuditEventItem>>> Handle(
        SearchAuditQuery query, CancellationToken cancellationToken)
    {
        var (page, size) = Paging.Clamp(query.Page, query.PageSize, DefaultPageSize);

        string search = (query.Search ?? "").Trim();

        var filter = new AuditFilter
        {
            // 🔴 الحدود بأيام القاهرة زي باقي الشاشات.
            FromUtc = CairoDay.StartUtc(query.From),
            ToUtc = CairoDay.AfterUtc(query.To),

            Action = (query.Action ?? "").Trim(),
            EntityType = (query.EntityType ?? "").Trim(),
            ActorName = (query.Actor ?? "").Trim(),

            // ⚠️ ومفيش توحيد عربي — القديم بيهرّب وبس.
            SearchPattern = search.Length == 0 ? null : SearchPattern.Contains(search),

            Page = page,
            PageSize = size,
        };

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
