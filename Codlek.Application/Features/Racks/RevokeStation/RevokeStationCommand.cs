using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Racks;
using MediatR;

namespace Codlek.Application.Features.Racks.RevokeStation;

public sealed record RevokeStationCommand(Guid Id, string? Reason)
    : IRequest<Result<RackActionResponse>>;
