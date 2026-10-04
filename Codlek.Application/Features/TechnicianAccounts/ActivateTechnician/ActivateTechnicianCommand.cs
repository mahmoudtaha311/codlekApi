using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Technicians;
using MediatR;

namespace Codlek.Application.Features.TechnicianAccounts.ActivateTechnician;

public sealed record ActivateTechnicianCommand(Guid Id)
    : IRequest<Result<TechnicianActionResponse>>;
