using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Repairs;
using MediatR;

namespace Codlek.Application.Features.Repairs.OpenRepair;

public sealed record OpenRepairCommand(
    Guid DeviceId,
    Guid? SourceReportId,
    Guid? AssignTechnicianId,
    string? FaultSummary,
    int? RequiredSpecialty) : IRequest<Result<OpenedRepairResponse>>;
