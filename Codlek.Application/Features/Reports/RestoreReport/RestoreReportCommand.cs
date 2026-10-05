using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Reports;
using MediatR;

namespace Codlek.Application.Features.Reports.RestoreReport;

public sealed record RestoreReportCommand(Guid Id, string? Reason)
    : IRequest<Result<ReportActionResponse>>;
