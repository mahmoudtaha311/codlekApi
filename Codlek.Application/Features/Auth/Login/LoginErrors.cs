using Codlek.Application.Abstractions;

namespace Codlek.Application.Features.Auth.Login;

/// <summary>أسباب فشل الدخول.</summary>
public static class LoginErrors
{
    /*
      🔴 **رسالة واحدة لـ«الاسم مش موجود» و«الباسورد غلط».**

      لأن التفرقة بينهم بتقول للي بيجرّب إن الاسم ده موجود — فيبقى
      عايز يخمّن الباسورد بس. ودي أول خطوة في أي محاولة دخول.

      ⚠️ والسبب الحقيقي بيروح للسجل. اللي بيدير النظام بيعرف، واللي
      بيجرّب لأ.
    */
    public static readonly Error InvalidCredentials =
        new("auth.invalid_credentials", "اسم المستخدم أو كلمة المرور غلط.", 401);

    /// <summary>
    /// الحساب موقوف — <b>ودي رسالة مختلفة عن قصد</b>.
    ///
    /// <para>⚠️ الفرق إن ده حساب صاحبه كتب الباسورد <b>صح</b>. لو
    /// قلنا له «الباسورد غلط» هيفضل يجرّب ويقفل حسابه أكتر. والسبب
    /// الحقيقي هنا مش بيدّي اللي بيجرّب حاجة — هو عدّى التحقق أصلاً.</para>
    /// </summary>
    public static readonly Error AccountSuspended =
        new("auth.account_suspended", "الحساب موقوف. كلّم الإدارة.", 403);
}
