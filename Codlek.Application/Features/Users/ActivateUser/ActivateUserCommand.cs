using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Users;
using MediatR;

namespace Codlek.Application.Features.Users.ActivateUser;

public sealed record ActivateUserCommand(Guid Id) : IRequest<Result<UserAccountResult>>;
