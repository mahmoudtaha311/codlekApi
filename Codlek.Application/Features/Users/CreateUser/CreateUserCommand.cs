using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Users;
using MediatR;

namespace Codlek.Application.Features.Users.CreateUser;

public sealed record CreateUserCommand(
    string Username,
    string DisplayName,
    string Password,
    int Role) : IRequest<Result<UserAccountResult>>;
