using Codlek.Application.Abstractions;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Core.Text;
using MediatR;

namespace Codlek.Application.Features.Brands.AddAlias;

public sealed class AddAliasCommandHandler(
    IBrandRepository brands,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    ICurrentUser me)
    : IRequestHandler<AddAliasCommand, Result>
{
    public async Task<Result> Handle(
        AddAliasCommand command, CancellationToken cancellationToken)
    {
        var brand = await brands.FindAsync(me.TenantId, command.BrandId, cancellationToken);

        if (brand is null) return Result.Failure(BrandErrors.NotFound);

        string raw = command.Value.Trim();

        if (raw.Length == 0) return Result.Failure(BrandErrors.AliasRequired);

        string key = BrandToken.Normalize(raw);

        /*
          🔴 **التفرّد على مستوى الشركة كلها، مش جوّه الماركة.**

          لو ماركتين ادّعوا «HP»، حل اللاب بيبقى معتمد على ترتيب
          الصفوف — ونفس اللاب يتحل لماركة مختلفة بعد ما حد يغيّر
          الترتيب.

          ⚠️ والفهرس في القاعدة بيمنعها برضه؛ الفحص ده عشان اللي
          بيكتب يفهم السبب بدل ما ياخد خطأ قاعدة بيانات.
        */
        var owner = await brands.FindAliasOwnerAsync(me.TenantId, key, cancellationToken);

        if (owner is { } taken)
            return Result.Failure(BrandErrors.AliasTakenBy(raw, taken.BrandName));

        // ⚠️ والاسم اللي هو نفسه اسم ماركة تانية بيترفض كمان — غير
        // كده الاسم بيبقى ليه معنيين.
        var sameName = await brands.FindByNormalizedNameAsync(
            me.TenantId, key, brand.Id, cancellationToken);

        if (sameName is not null)
            return Result.Failure(BrandErrors.AliasIsAnotherBrandName(raw, sameName.Name));

        brands.AddAlias(new LaptopBrandAlias
        {
            TenantId = me.TenantId,
            BrandId = brand.Id,
            RawValue = raw,
            NormalizedValue = key,
        });

        audit.Record(
            AuditActions.BrandAliasAdded, "LaptopBrand", brand.Id, brand.Name,
            $"اتضاف اسم «{raw}» لماركة «{brand.Name}»");

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
