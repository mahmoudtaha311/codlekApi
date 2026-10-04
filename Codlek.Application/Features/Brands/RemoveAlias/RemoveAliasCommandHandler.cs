using Codlek.Application.Abstractions;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Text;
using MediatR;

namespace Codlek.Application.Features.Brands.RemoveAlias;

public sealed class RemoveAliasCommandHandler(
    IBrandRepository brands,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    ICurrentUser me)
    : IRequestHandler<RemoveAliasCommand, Result>
{
    public async Task<Result> Handle(
        RemoveAliasCommand command, CancellationToken cancellationToken)
    {
        // ⚠️ البحث بالشكل المطبَّع: المسار جايّ من الواجهة بالاسم
        // الخام، واللي متخزّن هو الاتنين.
        string key = BrandToken.Normalize(command.Value);

        var alias = await brands.FindAliasAsync(
            me.TenantId, command.BrandId, key, cancellationToken);

        if (alias is null) return Result.Failure(BrandErrors.AliasNotFound);

        brands.RemoveAlias(alias);

        /*
          ⚠️ **الاسم الخام في السجل، مش المطبَّع.**

          اللي بيقرا السجل عايز يشوف اللي اتشال زي ما كان مكتوب —
          «Hewlett-Packard» مش «hewlettpackard».
        */
        audit.Record(
            AuditActions.BrandAliasRemoved, "LaptopBrand",
            command.BrandId, alias.RawValue,
            $"اتشال اسم «{alias.RawValue}»");

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
