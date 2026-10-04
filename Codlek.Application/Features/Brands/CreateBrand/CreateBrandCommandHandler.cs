using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Brands;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Core.Text;
using MediatR;

namespace Codlek.Application.Features.Brands.CreateBrand;

public sealed class CreateBrandCommandHandler(
    IBrandRepository brands,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    ICurrentUser me)
    : IRequestHandler<CreateBrandCommand, Result<BrandRow>>
{
    public async Task<Result<BrandRow>> Handle(
        CreateBrandCommand command, CancellationToken cancellationToken)
    {
        string name = command.Name.Trim();

        if (name.Length == 0) return Result.Failure<BrandRow>(BrandErrors.NameRequired);

        string key = BrandToken.Normalize(name);

        if (await brands.NameTakenAsync(me.TenantId, key, null, cancellationToken))
            return Result.Failure<BrandRow>(BrandErrors.NameTaken(name));

        var brand = new LaptopBrand
        {
            TenantId = me.TenantId,
            Name = name,
            NormalizedName = key,
            SortOrder = command.SortOrder,
            IsActive = true,
        };

        brands.Add(brand);

        audit.Record(
            AuditActions.BrandCreated, "LaptopBrand", brand.Id, brand.Name,
            $"اتعملت ماركة «{brand.Name}»");

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new BrandRow(
            brand.Id, brand.Name, brand.IsActive, brand.SortOrder, [], 0));
    }
}
