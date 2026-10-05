using FluentValidation;

namespace Codlek.Application.Features.Maintenance.ClearOemCodeNames;

public sealed class ClearOemCodeNamesCommandValidator : AbstractValidator<ClearOemCodeNamesCommand>
{
    public ClearOemCodeNamesCommandValidator()
    {
        RuleFor(c => c.TenantId).NotEmpty().WithMessage("الشركة مطلوبة.");

        RuleFor(c => c.BatchSize)
            .InclusiveBetween(1, MaintenanceLimits.MaxBatchSize)
            .WithMessage($"حجم الدفعة لازم يبقى من 1 لـ{MaintenanceLimits.MaxBatchSize}.");
    }
}
