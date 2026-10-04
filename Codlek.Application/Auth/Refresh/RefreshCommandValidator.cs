using FluentValidation;

namespace Codlek.Application.Auth.Refresh;

public sealed class RefreshCommandValidator : AbstractValidator<RefreshCommand>
{
    public RefreshCommandValidator() =>
        RuleFor(c => c.RefreshToken)
            .NotEmpty().WithMessage("توكن التجديد مطلوب.");
}
