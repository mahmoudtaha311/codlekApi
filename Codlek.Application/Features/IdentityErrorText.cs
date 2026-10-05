using Codlek.Core.Text;
using Microsoft.AspNetCore.Identity;

namespace Codlek.Application.Features;

/// <summary>
/// رسايل Identity بالعربي — <b>لكل مسار بيعيّن باسورد</b>.
///
/// <para>⚠️ رسايل Identity بالإنجليزي («Passwords must be at least
/// 8 characters»). والمستخدم هنا بيقرا عربي — فالرسالة الإنجليزية
/// بتبان كأنها عطل مش توجيه. وكانت مترجمة في تغيير الباسورد بس؛ إنشاء
/// حساب وإعادة التعيين من المدير كانوا بيطلّعوها بالإنجليزي.</para>
///
/// <para>⚠️ <b>والطول بييجي من القواعد نفسها</b>
/// (<see cref="PasswordOptions.RequiredLength"/>)، مش مكتوب هنا — عشان
/// لو القاعدة اتغيّرت، الرسالة تتغيّر معاها.</para>
/// </summary>
public static class IdentityErrorText
{
    public static string Of(IEnumerable<IdentityError> errors, PasswordOptions rules) =>
        string.Join(" ", errors.Select(e => Describe(e, rules)));

    public static string Describe(IdentityError error, PasswordOptions rules) => error.Code switch
    {
        "PasswordTooShort" =>
            $"كلمة المرور قصيرة — لازم {ArabicDigits.Of(rules.RequiredLength)} حروف على الأقل.",
        "PasswordRequiresDigit" => "كلمة المرور لازم يكون فيها رقم.",
        "PasswordRequiresLower" => "كلمة المرور لازم يكون فيها حرف صغير.",
        "PasswordRequiresUpper" => "كلمة المرور لازم يكون فيها حرف كبير.",
        "PasswordRequiresUniqueChars" => "كلمة المرور حروفها مكررة أوي.",
        "PasswordMismatch" => "كلمة المرور الحالية غلط.",

        // ⚠️ أي كود مش معروف بيرجع بنصه الإنجليزي بدل ما يتاكل.
        // رسالة وحشة أهون من رسالة فاضية.
        _ => error.Description,
    };
}
