using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Export;
using Codlek.Application.Features.Repairs;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Export;
using Codlek.Core.Repairs;
using Codlek.Core.Spreadsheets;
using MediatR;

namespace Codlek.Application.Features.Export.ExportRepairs;

/// <summary>
/// تصدير الصيانة.
///
/// <para>🔴 <b>الفلتر من <see cref="RepairFilters"/> — نفس اللي
/// القايمة بتستعمله</b>، والترتيب كمان.</para>
/// </summary>
public sealed class ExportRepairsQueryHandler(
    IRepairRepository repairs, ICurrentUser me)
    : IRequestHandler<ExportRepairsQuery, Result<ExportWorkbook>>
{
    public async Task<Result<ExportWorkbook>> Handle(
        ExportRepairsQuery query, CancellationToken cancellationToken)
    {
        var filter = RepairFilters.Build(
            query.Search, query.Status, query.Approval, query.Technician,
            query.From, query.To, query.Sort);

        var rows = await repairs.ExportAsync(
            me.TenantId, filter, ExportLimits.MaxRows, cancellationToken);

        var sheet = new Sheet("الصيانة", ExportColumns.Repairs,
            ExportLimits.Capped(rows, w => new object?[]
            {
                w.PublicCode,
                w.DeviceCode,
                w.FaultSummary,
                RepairStatusRules.Text(w.Status),

                // ⚠️ «مش متسند» مش خانة فاضية — الفرق بين أمر محدّش
                // واخده وأمر الفني اتشال منه بيبان في الملف.
                w.AssignedTechnicianName.Length > 0 ? w.AssignedTechnicianName : "مش متسند",

                ExportValues.Cairo(w.OpenedAtUtc),
                ExportValues.Cairo(w.StartedAtUtc),
                ExportValues.Cairo(w.CompletedAtUtc),
            }));

        return Result.Success(new ExportWorkbook("الصيانة", [sheet]));
    }
}
