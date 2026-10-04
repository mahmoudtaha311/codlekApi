using Codlek.Application.Abstractions;
using MediatR;

namespace Codlek.Application.Features.Brands.AddAlias;

public sealed record AddAliasCommand(Guid BrandId, string Value) : IRequest<Result>;
