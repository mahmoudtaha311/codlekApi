using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Racks;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Enums;
using MediatR;

namespace Codlek.Application.Features.Racks.GetStations;

/// <summary>
/// قايمة محطات الفحص.
///
/// <para>🔴 <b>النص العربي بيتحسب هنا، بعد <c>ToListAsync</c>.</b>
/// <c>RackStatusText.Arabic(...)</c> جوّه إسقاط EF بيترجم عادي
/// وبيعدّي فحوص الوحدة — وبيرمي على قاعدة حقيقية.</para>
/// </summary>
public sealed class GetStationsQueryHandler(
    IRackRepository racks, ICurrentUser me)
    : IRequestHandler<GetStationsQuery, Result<IReadOnlyList<StationListItem>>>
{
    public async Task<Result<IReadOnlyList<StationListItem>>> Handle(
        GetStationsQuery query, CancellationToken cancellationToken)
    {
        var rows = await racks.ListAsync(me.TenantId, cancellationToken);

        var items = rows.Select(r => new StationListItem(
            r.Id,
            r.RackCode,
            r.Name,
            r.Location,

            /*
              ⚠️ **الحالة بالإنجليزي جنب العربي — الاتنين.**

              اللوحة بتلوّن الصف من `status` (الملغية أحمر والموقوفة
              أصفر)، وبتعرض `statusText` للمستخدم. ولو العربي بس
              اللي رجع، التلوين كان بيعتمد على مقارنة نص عربي —
              وأول تعديل في كلمة بيكسره في صمت.
            */
            r.Status.ToString(),
            RackStatusText.Arabic(r.Status),

            r.ReportsReceived,
            r.RegisteredAtUtc,
            r.LastSeenAtUtc,
            r.AppVersion)).ToList();

        return Result.Success<IReadOnlyList<StationListItem>>(items);
    }
}
