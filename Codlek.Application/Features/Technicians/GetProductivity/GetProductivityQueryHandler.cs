using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Technicians;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using MediatR;

namespace Codlek.Application.Features.Technicians.GetProductivity;

public sealed class GetProductivityQueryHandler(
    ITechnicianProductivityRepository productivity,
    ICurrentUser me)
    : IRequestHandler<GetProductivityQuery, Result<IReadOnlyList<TechnicianListItem>>>
{
    public async Task<Result<IReadOnlyList<TechnicianListItem>>> Handle(
        GetProductivityQuery query, CancellationToken cancellationToken)
    {
        var window = TechnicianProductivity.Window(query.Range, query.From, query.To);

        var testing = await productivity.TestingAsync(me.TenantId, window, cancellationToken);
        var repairs = await productivity.RepairsAsync(me.TenantId, window, cancellationToken);

        var rows = TechnicianProductivity.Rows(testing, repairs);

        /*
          ⚠️ **الترتيب بيتعمل على السيرفر** عشان الملف المصدَّر يطلع
          بنفس ترتيب الشاشة بالظبط — نفس الدالة بتتنده من التصدير.
        */
        var items = TechnicianProductivity.Sorted(
            TechnicianProductivity.Matching(rows, query.Search), query.Sort);

        return Result.Success<IReadOnlyList<TechnicianListItem>>(items);
    }
}
