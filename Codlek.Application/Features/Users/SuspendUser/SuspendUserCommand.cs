using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Users;
using MediatR;

namespace Codlek.Application.Features.Users.SuspendUser;

public sealed record SuspendUserCommand(Guid Id, string Reason)
    : IRequest<Result<UserAccountResult>>;
