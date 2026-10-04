using Codlek.Application.Abstractions;

namespace Codlek.Application.Account;

public static class AccountErrors
{
    /// <summary>
    /// 🔴 <b>التوكن سليم بس الصف مبقاش موجود — ودي <c>401</c> مش
    /// <c>404</c>.</b>
    ///
    /// <para>الحساب اتمسح أو اتنقل لشركة تانية بعد ما التوكن اتعمل.
    /// وده مش «مورد مش موجود» — دي <b>جلسة مابقتش صالحة</b>. والفرق
    /// بيبان في الواجهة: <c>401</c> بتودّي على شاشة الدخول،
    /// و<c>404</c> بتعرض «مش موجود» لواحد قاعد في النظام.</para>
    /// </summary>
    public static readonly Error SessionNoLongerValid =
        new("account.session_invalid", "الجلسة مابقتش صالحة — سجّل دخول تاني.", 401);

    public static readonly Error CurrentPasswordWrong =
        new("account.current_password_wrong", "كلمة المرور الحالية غلط.", 400);

    public static readonly Error ConfirmationMismatch =
        new("account.confirmation_mismatch", "التأكيد مش مطابق.", 400);

    public static Error NewPasswordRejected(string reason) =>
        new("account.new_password_rejected", reason, 400);
}
