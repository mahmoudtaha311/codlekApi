using FluentValidation;

namespace Codlek.Application.Features.Auth.Refresh;

public sealed class RefreshCommandValidator : AbstractValidator<RefreshCommand>
{
    public RefreshCommandValidator() =>
        RuleFor(c => c.RefreshToken)
            .NotEmpty().WithMessage("توكن التجديد مطلوب.");
}
