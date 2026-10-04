using FluentValidation;

namespace Codlek.Application.Account.UpdateProfile;

public sealed class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator()
    {
        // ⚠️ الرسايل دي نفس رسايل المشروع القديم بالحرف — الواجهة
        // بتعرضها زي ما هي.
        RuleFor(c => c.DisplayName)
            .Must(n => !string.IsNullOrWhiteSpace(n))
            .WithMessage("الاسم ماينفعش يبقى فاضي.")
            .MaximumLength(120)
            .WithMessage("الاسم طويل أوي — ١٢٠ حرف بحد أقصى.");
    }
}
