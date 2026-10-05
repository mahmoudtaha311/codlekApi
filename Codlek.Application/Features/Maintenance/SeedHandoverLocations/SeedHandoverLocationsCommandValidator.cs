using FluentValidation;

namespace Codlek.Application.Features.Maintenance.SeedHandoverLocations;

public sealed class SeedHandoverLocationsCommandValidator
    : AbstractValidator<SeedHandoverLocationsCommand>
{
    public SeedHandoverLocationsCommandValidator()
    {
        // ⚠️ شركة فاضية = صفوف مواقع مالهاش شركة — والمفتاح الأجنبي
        //    بيرفضها بخطأ قاعدة بدل رسالة.
        RuleFor(c => c.TenantId).NotEmpty().WithMessage("الشركة مطلوبة.");
    }
}
