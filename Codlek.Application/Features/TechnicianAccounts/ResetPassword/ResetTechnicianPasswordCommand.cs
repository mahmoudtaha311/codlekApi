using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Technicians;
using MediatR;

namespace Codlek.Application.Features.TechnicianAccounts.ResetPassword;

public sealed record ResetTechnicianPasswordCommand(Guid Id, string? Password)
    : IRequest<Result<TechnicianSecretResponse>>;
