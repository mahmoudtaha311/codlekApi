using Codlek.Application.Abstractions;
using Codlek.Core.Auth;

namespace Codlek.Application.Features.Users;

public static class UserErrors
{
    /// <summary>
    /// 🔴 <b><c>404</c> مقصودة بدل <c>403</c>.</b>
    ///
    /// <para>«مش موجود» مابتأكّدش إن الحساب ده موجود عند حد تاني. و
    /// <c>403</c> كانت هتخلّي النقطة أداة عدّ لحسابات الشركات
    /// التانية.</para>
    /// </summary>
    public static readonly Error NotFound =
        new("user.not_found", "الحساب ده مش موجود", 404);

    public static readonly Error UnknownRole =
        new("user.unknown_role", "الصلاحية دي مش معروفة", 400);

    public static readonly Error UsernameTooShort =
        new("user.username_too_short",
            $"اسم المستخدم لازم يكون {UserManagementRules.MinUsernameLength} حروف على الأقل", 400);

    public static readonly Error UsernameTooLong =
        new("user.username_too_long",
            $"اسم المستخدم أطول من {UserManagementRules.MaxUsernameLength} حرف", 400);

    public static readonly Error DisplayNameTooShort =
        new("user.display_name_too_short", "اكتب الاسم بالكامل", 400);

    public static readonly Error DisplayNameTooLong =
        new("user.display_name_too_long",
            $"الاسم أطول من {UserManagementRules.MaxDisplayNameLength} حرف", 400);

    /// <summary>
    /// ⚠️ <b>والرسالة مابتقولش الاسم متاخد في أنهي شركة.</b>
    ///
    /// <para>لو قالت، النقطة دي بتبقى أداة عدّ لحسابات الشركات
    /// التانية: أي مدير يجرّب أسماء ويعرف مين عند مين.</para>
    /// </summary>
    public static readonly Error UsernameTaken =
        new("user.username_taken", "اسم المستخدم ده مستخدم قبل كده", 400);

    public static readonly Error SuspendReasonRequired =
        new("user.suspend_reason_required",
            "لازم تكتب سبب الإيقاف — المستخدم هيشوفه لما يحاول يدخل", 400);

    public static readonly Error SuspendReasonTooLong =
        new("user.suspend_reason_too_long",
            $"السبب أطول من {UserManagementRules.MaxSuspendReasonLength} حرف — اختصره", 400);

    public static readonly Error CannotCreateThatRole =
        new("user.cannot_create_role",
            "مدير المخزن بيضيف فنيين بس — الحسابات الأعلى من صلاحية المدير العام", 403);

    /// <summary>⚠️ الرسالة بتتبنى من <c>UserManagementRules</c>.</summary>
    public static Error Forbidden(string reason) =>
        new("user.forbidden", reason, 403);

    public static Error PasswordRejected(string reason) =>
        new("user.password_rejected", reason, 400);

    /// <summary>
    /// 🔴 مقدرش يولّد كود حساب غير مكرر.
    ///
    /// <para>⚠️ ده مش عطل عشوائي: ٤٠ محاولة فاشلة معناها الشركة
    /// استهلكت مدى الأكواد تقريباً. ولو سكتنا، كود مكرر بيخلط شغل
    /// ناس ببعضه.</para>
    /// </summary>
    public static readonly Error CodeExhausted =
        new("user.code_exhausted", "مقدرتش أولّد كود حساب — كلّم الدعم", 500);
}
