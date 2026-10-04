using FluentValidation;

namespace Codlek.Application.Account.ChangePassword;

/// <summary>
/// ⚠️ <b>التحقق هنا على الوجود بس، مش على قوة الباسورد.</b>
///
/// <para>قواعد القوة (الطول، الأرقام، الحروف) مكانها
/// <c>IdentityOptions.Password</c> — مكان واحد بيتطبّق على <b>كل</b>
/// مسار بيعيّن باسورد: التغيير، وإعادة التعيين من المدير، وإنشاء
/// حساب جديد. ولو كانت مكتوبة هنا، أول مسار يتزاد بعدين بينساها.</para>
/// </summary>
public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(c => c.CurrentPassword)
            .NotEmpty().WithMessage("كلمة المرور الحالية مطلوبة.");

        RuleFor(c => c.NewPassword)
            .NotEmpty().WithMessage("كلمة المرور الجديدة مطلوبة.");
    }
}
