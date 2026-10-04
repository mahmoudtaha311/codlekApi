using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Brands;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using MediatR;

namespace Codlek.Application.Features.Brands.GetBrands;

public sealed class GetBrandsQueryHandler(IBrandRepository brands, ICurrentUser me)
    : IRequestHandler<GetBrandsQuery, Result<IReadOnlyList<BrandRow>>>
{
    public async Task<Result<IReadOnlyList<BrandRow>>> Handle(
        GetBrandsQuery query, CancellationToken cancellationToken) =>
        Result.Success(await brands.ListAsync(me.TenantId, cancellationToken));
}
