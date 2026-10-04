using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Common;
using Codlek.Application.Contracts.Reports;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Paging;
using MediatR;

namespace Codlek.Application.Features.Reports.GetReports;

public sealed class GetReportsQueryHandler(
    IReportRepository reports,
    ICurrentUser me)
    : IRequestHandler<GetReportsQuery, Result<PagedResult<ReportListItem>>>
{
    public async Task<Result<PagedResult<ReportListItem>>> Handle(
        GetReportsQuery query, CancellationToken cancellationToken)
    {
        var filter = ReportFilters.Build(
            me, query.Search, query.Result, query.Technician,
            query.Container, query.From, query.To, query.Page, query.PageSize);

        var (rows, total) = await reports.ListAsync(me.TenantId, filter, cancellationToken);

        // ⚠️ أكواد الراكات والأجهزة في قراية واحدة لكل جدول — مش
        // واحدة لكل صف.
        var racks = await reports.RackLabelsAsync(
            me.TenantId, rows.Select(r => r.SourceRackId), cancellationToken);

        var deviceCodes = await reports.DeviceCodesAsync(
            me.TenantId, rows.Select(r => r.DeviceId), cancellationToken);

        var items = rows.Select(r => ReportMapping.Row(r, racks, deviceCodes)).ToList();

        return Result.Success(new PagedResult<ReportListItem>(
            Items: items,
            Page: filter.Page,
            PageSize: filter.PageSize,
            TotalItems: total,
            TotalPages: Paging.TotalPages(total, filter.PageSize)));
    }
}
