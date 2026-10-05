using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Reports;
using MediatR;

namespace Codlek.Application.Features.Reports.GetReportEdits;

/// <summary>تعديلات الفحص بعد التسليم — المديرين بس.</summary>
public sealed record GetReportEditsQuery(Guid Id)
    : IRequest<Result<IReadOnlyList<ReportEditItem>>>;
