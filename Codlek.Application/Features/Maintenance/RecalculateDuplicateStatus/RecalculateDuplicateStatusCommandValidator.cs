using FluentValidation;

namespace Codlek.Application.Features.Maintenance.RecalculateDuplicateStatus;

public sealed class RecalculateDuplicateStatusCommandValidator
    : AbstractValidator<RecalculateDuplicateStatusCommand>
{
    public RecalculateDuplicateStatusCommandValidator()
    {
        RuleFor(c => c.TenantId).NotEmpty().WithMessage("الشركة مطلوبة.");

        RuleFor(c => c.BatchSize)
            .InclusiveBetween(1, MaintenanceLimits.MaxBatchSize)
            .WithMessage($"حجم الدفعة لازم يبقى من 1 لـ{MaintenanceLimits.MaxBatchSize}.");
    }
}
