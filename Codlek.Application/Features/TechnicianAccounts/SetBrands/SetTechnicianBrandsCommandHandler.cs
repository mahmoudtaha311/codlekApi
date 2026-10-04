using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Technicians;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using MediatR;

namespace Codlek.Application.Features.TechnicianAccounts.SetBrands;

public sealed class SetTechnicianBrandsCommandHandler(
    ITechnicianAccountRepository accounts,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    ICurrentUser me)
    : IRequestHandler<SetTechnicianBrandsCommand, Result<TechnicianBrandsResponse>>
{
    public async Task<Result<TechnicianBrandsResponse>> Handle(
        SetTechnicianBrandsCommand command, CancellationToken cancellationToken)
    {
        var technician = await accounts.FindAsync(me.TenantId, command.Id, cancellationToken);

        if (technician is null)
            return Result.Failure<TechnicianBrandsResponse>(
                TechnicianAccountErrors.NotFound);

        var wanted = (command.BrandIds ?? []).Distinct().ToList();

        /*
          ⚠️ **الماركة المش موجودة بترفض الطلب كله.**

          ربط فني بماركة مش في القايمة بيخلّي القيد يتصرّف بشكل مش
          متوقّع: الماركة مش في قواعد التوحيد، فأي لاب عمره ما
          يطابقها — والفني بيبان مقيّد وهو عملياً ممنوع من كل حاجة.
        */
        if (wanted.Count > 0)
        {
            int known = await accounts.CountKnownBrandsAsync(
                me.TenantId, wanted, cancellationToken);

            if (known != wanted.Count)
                return Result.Failure<TechnicianBrandsResponse>(
                    TechnicianAccountErrors.UnknownBrand);
        }

        var current = await accounts.BrandLinksForAsync(
            me.TenantId, command.Id, cancellationToken);

        var currentIds = current.Select(x => x.BrandId).ToHashSet();

        /*
          ⚠️ **المقارنة بالمجموعة — الترتيب مالوش معنى.**

          المدير بيدوس حفظ من غير ما يعدّل، والرد لازم يقوله إنه
          مااتغيّرش حاجة بدل ما يفتكر إنه عمل حاجة. ومن غير الفحص ده،
          كل دوسة بتمسح الصفوف وتكتبها تاني وتسيب سطر سجل وتحدّث
          «وقت تغيير الصلاحية» — واللي بيراجع بيشوف تغييرات
          مااتعملتش.
        */
        bool changed = !currentIds.SetEquals(wanted);

        if (changed)
        {
            // ⚠️ استبدال كامل: امسح كل الربط وأعد كتابته.
            accounts.RemoveBrandLinks(current);

            foreach (var brandId in wanted)
            {
                accounts.AddBrandLink(new TechnicianBrand
                {
                    TenantId = me.TenantId,
                    TechnicianId = command.Id,
                    BrandId = brandId,
                });
            }

            // 🔴 والقيد ده صلاحية — فوقت تغييرها بيتحدّث، عشان الشغل
            // الأوفلاين يتحاكم بقواعد وقته.
            technician.CapabilityChangedAtUtc = DateTime.UtcNow;
            technician.UpdatedAtUtc = DateTime.UtcNow;

            var names = await accounts.BrandNamesAsync(
                me.TenantId, wanted, cancellationToken);

            audit.Record(
                AuditActions.TechnicianBrandsChanged, "Technician",
                technician.Id, technician.Code,

                // ⚠️ والسجل بيقول الأسماء مش المعرّفات — اللي بيراجع
                // مش حافظ الـGUIDs.
                names.Count == 0
                    ? $"ماركات «{technician.DisplayName}»: مفيش قيد — كل الماركات"
                    : $"ماركات «{technician.DisplayName}»: {string.Join("، ", names)}");

            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(new TechnicianBrandsResponse(
            wanted, changed ? "اتحفظت الماركات" : "مفيش تغيير"));
    }
}
