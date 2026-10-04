using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Brands;
using MediatR;

namespace Codlek.Application.Features.Brands.CreateBrand;

public sealed record CreateBrandCommand(string Name, int SortOrder)
    : IRequest<Result<BrandRow>>;
