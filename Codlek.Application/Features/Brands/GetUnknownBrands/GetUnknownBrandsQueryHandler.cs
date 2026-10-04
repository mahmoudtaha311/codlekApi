using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Brands;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Text;
using MediatR;

namespace Codlek.Application.Features.Brands.GetUnknownBrands;

public sealed class GetUnknownBrandsQueryHandler(IBrandRepository brands, ICurrentUser me)
    : IRequestHandler<GetUnknownBrandsQuery, Result<IReadOnlyList<UnknownBrandRow>>>
{
    public async Task<Result<IReadOnlyList<UnknownBrandRow>>> Handle(
        GetUnknownBrandsQuery query, CancellationToken cancellationToken)
    {
        var rules = await brands.RulesAsync(me.TenantId, cancellationToken);
        var raw = await brands.RawManufacturersAsync(me.TenantId, cancellationToken);

        /*
          ⚠️ **الحلّ بيحصل في الذاكرة عن قصد.**

          `BrandToken.Resolve` دالة نقية فيها قواعد مطابقة (تطبيع +
          أسماء بديلة + احتواء) — وماينفعش تترجم لـSQL. والعدد صغير:
          أسماء المصنّعين المميّزة عشرات، مش ملايين.

          🔴 **والأهم إنها نفس الدالة اللي الراكة بتشغّلها.** لو
          كتبناها SQL هنا، كان بقى عندنا تنفيذين لنفس القاعدة —
          وأول تعديل في واحد منهم بيخلّي الطرفين يحلّوا نفس اللاب
          لماركتين مختلفتين.
        */
        var unknown = raw
            .Where(x => BrandToken.Resolve(rules, x.Name).BrandId is null)
            .OrderByDescending(x => x.Count)
            .Select(x => new UnknownBrandRow(x.Name, x.Count))
            .ToList();

        return Result.Success<IReadOnlyList<UnknownBrandRow>>(unknown);
    }
}
