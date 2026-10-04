using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Brands;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Text;
using MediatR;

namespace Codlek.Application.Features.Brands.UpdateBrand;

public sealed class UpdateBrandCommandHandler(
    IBrandRepository brands,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    ICurrentUser me)
    : IRequestHandler<UpdateBrandCommand, Result<BrandRow>>
{
    public async Task<Result<BrandRow>> Handle(
        UpdateBrandCommand command, CancellationToken cancellationToken)
    {
        var brand = await brands.FindWithAliasesAsync(me.TenantId, command.Id, cancellationToken);

        if (brand is null) return Result.Failure<BrandRow>(BrandErrors.NotFound);

        // ⚠️ الاسم الفاضي معناه «ماتغيّرش» — نفس القديم.
        string name = (command.Name ?? "").Trim();

        if (name.Length > 0)
        {
            string key = BrandToken.Normalize(name);

            /*
              🔴 **التحقق من التكرار ده زيادة عن القديم — وهو إصلاح.**

              القديم كان بيكتب الاسم على طول من غير أي فحص. فماركتين
              بنفس الاسم المطبَّع ممكن يتعملوا بالتعديل: تعمل «HP»،
              وتعدّل «Dell» وتسمّيها «hp». وساعتها حل اللاب بيبقى
              معتمد على ترتيب الصفوف — ونفس اللاب يتحل لماركة مختلفة
              بعد أي تغيير في الترتيب.

              ⚠️ **والفرق ده آمن**: الطلب اللي كان بينجح غلط بقى
              بيترفض برسالة مفهومة. مفيش طلب سليم بيترفض.
            */
            if (await brands.NameTakenAsync(me.TenantId, key, brand.Id, cancellationToken))
                return Result.Failure<BrandRow>(BrandErrors.NameTaken(name));

            brand.Name = name;
            brand.NormalizedName = key;
        }

        if (command.SortOrder.HasValue) brand.SortOrder = command.SortOrder.Value;
        if (command.IsActive.HasValue) brand.IsActive = command.IsActive.Value;

        audit.Record(
            AuditActions.BrandUpdated, "LaptopBrand", brand.Id, brand.Name,
            $"اتعدّلت ماركة «{brand.Name}»");

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var aliases = await brands.AliasValuesAsync(brand.Id, cancellationToken);
        int technicians = await brands.CountTechniciansAsync(brand.Id, cancellationToken);

        return Result.Success(new BrandRow(
            brand.Id, brand.Name, brand.IsActive, brand.SortOrder, aliases, technicians));
    }
}
