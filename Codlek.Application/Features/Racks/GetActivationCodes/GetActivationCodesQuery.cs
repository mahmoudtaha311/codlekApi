using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Racks;
using MediatR;

namespace Codlek.Application.Features.Racks.GetActivationCodes;

public sealed record GetActivationCodesQuery
    : IRequest<Result<IReadOnlyList<ActivationCodeRow>>>;
