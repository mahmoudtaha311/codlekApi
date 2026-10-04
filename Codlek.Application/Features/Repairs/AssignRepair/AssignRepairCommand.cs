using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Repairs;
using MediatR;

namespace Codlek.Application.Features.Repairs.AssignRepair;

public sealed record AssignRepairCommand(Guid Id, Guid TechnicianId)
    : IRequest<Result<RepairActionResponse>>;
