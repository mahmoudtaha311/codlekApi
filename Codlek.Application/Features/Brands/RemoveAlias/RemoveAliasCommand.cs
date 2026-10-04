using Codlek.Application.Abstractions;
using MediatR;

namespace Codlek.Application.Features.Brands.RemoveAlias;

public sealed record RemoveAliasCommand(Guid BrandId, string Value) : IRequest<Result>;
