using FluentValidation;

namespace Codlek.Application.Features.Maintenance.ResolveOrphanReports;

public sealed class ResolveOrphanReportsCommandValidator
    : AbstractValidator<ResolveOrphanReportsCommand>
{
    public ResolveOrphanReportsCommandValidator()
    {
        RuleFor(c => c.TenantId).NotEmpty().WithMessage("الشركة مطلوبة.");

        // ⚠️ صفر = دفعة فاضية من أول لفة واللفة بتخلص من غير ما تعمل
        //    حاجة، ورقم كبير بيرجّع مشكلة الذاكرة اللي الدفعات معمولة عشانها.
        RuleFor(c => c.BatchSize)
            .InclusiveBetween(1, MaintenanceLimits.MaxBatchSize)
            .WithMessage($"حجم الدفعة لازم يبقى من 1 لـ{MaintenanceLimits.MaxBatchSize}.");
    }
}
