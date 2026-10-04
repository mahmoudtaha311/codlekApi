using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Brands;
using MediatR;

namespace Codlek.Application.Features.Brands.UpdateBrand;

public sealed record UpdateBrandCommand(Guid Id, string? Name, int? SortOrder, bool? IsActive)
    : IRequest<Result<BrandRow>>;
