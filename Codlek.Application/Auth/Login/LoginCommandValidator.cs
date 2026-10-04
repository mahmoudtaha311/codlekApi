using FluentValidation;

namespace Codlek.Application.Auth.Login;

/// <summary>
/// ⚠️ <b>التحقق هنا على الشكل بس، مش على الصلاحية.</b>
///
/// <para>مفيش طول أدنى للباسورد هنا عن قصد: الباسوردات القديمة
/// الموجودة في القاعدة دلوقتي ممكن تكون أقصر من القاعدة الجديدة.
/// فلو حطّينا الشرط هنا، <b>مستخدم بباسورد قديم قصير مايعرفش يدخل
/// يغيّره</b> — يعني الشرط نفسه بيمنع الوصول للشاشة اللي بتحقّقه.</para>
///
/// <para>والطول الأدنى مكانه وقت <b>تعيين</b> الباسورد
/// (<c>IdentityOptions.Password</c>)، مش وقت الدخول بيه.</para>
/// </summary>
public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(c => c.Username)
            .NotEmpty().WithMessage("اسم المستخدم مطلوب.")
            .MaximumLength(60).WithMessage("اسم المستخدم أطول من اللازم.");

        RuleFor(c => c.Password)
            .NotEmpty().WithMessage("كلمة المرور مطلوبة.");
    }
}
