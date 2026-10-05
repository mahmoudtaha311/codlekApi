using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Reports;
using MediatR;

namespace Codlek.Application.Features.Reports.DeleteReport;

public sealed record DeleteReportCommand(Guid Id, string? Reason)
    : IRequest<Result<ReportActionResponse>>;
