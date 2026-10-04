using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Repairs;
using MediatR;

namespace Codlek.Application.Features.Repairs.CancelRepair;

public sealed record CancelRepairCommand(Guid Id, string? Reason)
    : IRequest<Result<RepairActionResponse>>;
