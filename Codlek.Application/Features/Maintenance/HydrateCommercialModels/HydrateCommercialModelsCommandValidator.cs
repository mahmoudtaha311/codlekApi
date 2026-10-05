using FluentValidation;

namespace Codlek.Application.Features.Maintenance.HydrateCommercialModels;

public sealed class HydrateCommercialModelsCommandValidator
    : AbstractValidator<HydrateCommercialModelsCommand>
{
    public HydrateCommercialModelsCommandValidator()
    {
        RuleFor(c => c.TenantId).NotEmpty().WithMessage("الشركة مطلوبة.");

        RuleFor(c => c.BatchSize)
            .InclusiveBetween(1, MaintenanceLimits.MaxBatchSize)
            .WithMessage($"حجم الدفعة لازم يبقى من 1 لـ{MaintenanceLimits.MaxBatchSize}.");
    }
}
