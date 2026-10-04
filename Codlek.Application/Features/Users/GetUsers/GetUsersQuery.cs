using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Users;
using MediatR;

namespace Codlek.Application.Features.Users.GetUsers;

public sealed record GetUsersQuery : IRequest<Result<IReadOnlyList<UserAccountItem>>>;
