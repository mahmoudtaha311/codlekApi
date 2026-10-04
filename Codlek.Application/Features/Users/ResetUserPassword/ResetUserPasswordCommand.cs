using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Users;
using MediatR;

namespace Codlek.Application.Features.Users.ResetUserPassword;

public sealed record ResetUserPasswordCommand(Guid Id, string Password)
    : IRequest<Result<UserAccountResult>>;
