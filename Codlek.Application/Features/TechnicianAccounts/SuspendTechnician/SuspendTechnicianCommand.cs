using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Technicians;
using MediatR;

namespace Codlek.Application.Features.TechnicianAccounts.SuspendTechnician;

public sealed record SuspendTechnicianCommand(Guid Id, string? Reason)
    : IRequest<Result<TechnicianActionResponse>>;
