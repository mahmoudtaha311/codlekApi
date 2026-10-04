using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Brands;
using MediatR;

namespace Codlek.Application.Features.Brands.GetBrands;

public sealed record GetBrandsQuery : IRequest<Result<IReadOnlyList<BrandRow>>>;
