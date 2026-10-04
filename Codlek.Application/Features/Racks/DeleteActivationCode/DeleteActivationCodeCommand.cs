using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Racks;
using MediatR;

namespace Codlek.Application.Features.Racks.DeleteActivationCode;

public sealed record DeleteActivationCodeCommand(Guid Id)
    : IRequest<Result<RackActionResponse>>;
