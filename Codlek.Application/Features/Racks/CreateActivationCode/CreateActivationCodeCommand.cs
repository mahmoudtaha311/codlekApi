using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Racks;
using MediatR;

namespace Codlek.Application.Features.Racks.CreateActivationCode;

public sealed record CreateActivationCodeCommand(string? Name, string? Location)
    : IRequest<Result<ActivationCodeIssued>>;
