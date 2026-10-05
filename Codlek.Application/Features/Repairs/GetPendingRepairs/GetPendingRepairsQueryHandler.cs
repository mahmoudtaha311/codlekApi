using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Repairs;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Repairs;
using Codlek.Core.Text;
using MediatR;

namespace Codlek.Application.Features.Repairs.GetPendingRepairs;

/// <summary>
/// بيبني طابور قرار المحاسب.
///
/// <para>🔴 <b>الصف ده فيه معلومتين المحاسب مايقدرش يقرّر من
/// غيرهم:</b> هل الماركة مش معروفة (فالقيد ماتطبّقش)، وهل الفني
/// المتسند برّه ماركات اللاب. هو الوحيد اللي بيقدر يعدّي القاعدة،
/// فلازم يشوفها <b>قبل</b> ما يوافق.</para>
/// </summary>
public sealed class GetPendingRepairsQueryHandler(
    IRepairRepository repairs,
    ICurrentUser me)
    : IRequestHandler<GetPendingRepairsQuery, Result<IReadOnlyList<PendingRepairRow>>>
{
    public async Task<Result<IReadOnlyList<PendingRepairRow>>> Handle(
        GetPendingRepairsQuery query, CancellationToken cancellationToken)
    {
        var items = await repairs.PendingAsync(me.TenantId, cancellationToken);

        if (items.Count == 0)
            return Result.Success<IReadOnlyList<PendingRepairRow>>([]);

        /*
          ⚠️ **قواعد الماركات بتتقرا مرة واحدة للطابور كله.**

          القديم كان بيقراها مرة كمان، فالعدد ثابت. لكن ماركات **كل
          فني** كانت بتتقرا جوّه اللفّة — استعلام لكل صف.

          🔴 وهنا بتتقرا مرة واحدة لكل فني **مميّز**: الطابور عادةً
          فيه نفس الفنيين مكرّرين، فده بيحوّل ١٠٠ استعلام لخمسة.
          والسلوك واحد بالحرف.
        */
        var rules = await repairs.BrandRulesAsync(me.TenantId, cancellationToken);

        var brandsByTechnician = new Dictionary<Guid, IReadOnlyCollection<Guid>>();

        foreach (var technicianId in items
                     .Where(w => w.AssignedTechnicianId is not null)
                     .Select(w => w.AssignedTechnicianId!.Value)
                     .Distinct())
        {
            brandsByTechnician[technicianId] =
                await repairs.TechnicianBrandsAsync(
                    me.TenantId, technicianId, cancellationToken);
        }

        var rows = new List<PendingRepairRow>(items.Count);

        foreach (var item in items)
        {
            string manufacturer = item.Device?.LastKnownManufacturer ?? "";

            var brand = BrandToken.Resolve(rules, manufacturer);

            /*
              🔴 **«الفني برّه ماركاته» بيتحسب هنا، مش في الواجهة.**

              الواجهة بتعرضه كتحذير جمب الصف. ولو حسبته لوحدها، القاعدة
              بتبقى مكتوبة في مكانين بلغتين — وأول تعديل في واحدة منهم
              بيخلّي المحاسب يوافق على إسناد غلط وهو فاكر إنه سليم.
            */
            bool outsideBrand = false;

            if (item.AssignedTechnicianId is { } technicianId
                && brandsByTechnician.TryGetValue(technicianId, out var allowed))
            {
                outsideBrand = !RepairBrandGate.CanAssign(allowed, brand);
            }

            string model = string.IsNullOrWhiteSpace(item.Device?.CommercialModelName)
                ? item.Device?.LastKnownModel ?? ""
                : item.Device!.CommercialModelName!;

            rows.Add(new PendingRepairRow(
                Id: item.Id,
                PublicCode: item.PublicCode,
                DeviceCode: item.Device?.PublicCode ?? "",
                DeviceName: $"{manufacturer} {model}".Trim(),
                Brand: brand.BrandId is null ? "" : brand.Name,

                // 🔴 الماركة المش معروفة بتتعلّم هنا — القيد بيعدّي
                // عليها، وصاحب الشغل لازم يشوفها عشان يضيفها للقايمة.
                BrandUnknown: brand.BrandId is null,

                OpenedByName: item.OpenedByName,
                OpenedAtUtc: item.OpenedAtUtc,
                FaultSummary: item.FaultSummary,
                AssignedTechnicianId: item.AssignedTechnicianId,
                TechnicianName: item.AssignedTechnician?.DisplayName ?? "",
                TechnicianOutsideBrand: outsideBrand,
                Parts: item.Parts.Select(p => p.Name).ToList(),
                StartedWithoutApproval: item.StartedWithoutApproval,
                DeviceId: item.DeviceId == Guid.Empty ? null : item.DeviceId));
        }

        return Result.Success<IReadOnlyList<PendingRepairRow>>(rows);
    }
}
