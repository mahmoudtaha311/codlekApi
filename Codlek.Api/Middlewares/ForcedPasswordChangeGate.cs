using Codlek.Infrastructure.Auth;

namespace Codlek.Api.Middlewares;

/// <summary>
/// باسورد مؤقت = <b>مفيش وصول لحاجة غير تغييره</b> — منقول من القديم.
///
/// <para>🔴 <b>والسبب مش شكليات.</b> الحساب الجديد بيتعمل بباسورد كتبه
/// المدير بإيده وبعته على واتساب، فهو — وأي حد شاف الرسالة — عارفه.
/// من غير الحاجز ده الحساب بيفضل شغّال بالباسورد ده شهور، وكل سطر في
/// سجل التدقيق مكتوب باسم صاحبه بيبقى قابل للإنكار. وده نفس اللي
/// بيخلّي <c>admin</c>/<c>admin</c> سايبة على النت كارثة.</para>
///
/// <para>🔴 <b>والمشروع الجديد كان بيحط العلامة ومابيطبّقهاش.</b> إنشاء
/// الحساب وإعادة تعيين الباسورد كانوا بيعلّموا <c>MustChangePassword</c>،
/// والتوكن بيشيلها — بس مفيش حاجة كانت بتمنع. اتلقط وقت تجهيز اللوحة
/// على الجديد: القديم عنده الحاجز ده في <c>Program.cs</c> من بدري.</para>
///
/// <para>⚠️ <b>فرق واحد مقصود عن القديم:</b> «أنا مين» مسموحة. القديم
/// كان بيرفضها واللوحة بتحوّل على صفحة Razor؛ هنا مفيش صفحات، واللوحة
/// محتاجة تعرف إن المستخدم لازم يغيّر عشان تفتحله شاشة التغيير.</para>
/// </summary>
public static class ForcedPasswordChangeGate
{
    /// <summary>
    /// اللي مسموح بيه — <b>مسارات كاملة، مش بداياتها</b>. بادئة
    /// <c>/api/v1/account</c> كانت هتعدّي تعديل الملف الشخصي كمان.
    /// </summary>
    public static readonly IReadOnlySet<string> Allowed =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "/api/health",
            "/api/v1/auth/me",
            "/api/v1/auth/login",
            "/api/v1/auth/refresh",
            "/api/v1/auth/logout",
            "/api/v1/account",
            "/api/v1/account/change-password",
        };

    public const string Code = "PasswordChangeRequired";

    public const string Message = "لازم تغيّر كلمة المرور المؤقتة الأول.";

    public static bool Blocks(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true) return false;

        if (!context.User.HasClaim(JwtTokenIssuer.MustChangeClaim, "1")) return false;

        string path = (context.Request.Path.Value ?? "").TrimEnd('/');

        return !Allowed.Contains(path);
    }

    /// <summary>
    /// ⚠️ <b>بعد التحقق</b> — الحاجز بيقرا الادعاء من التوكن، فلازم
    /// يكون اتقرا. ومن غير استعلام: التوكن بيتعاد إصداره ساعة التغيير
    /// (<c>ChangePasswordCommandHandler</c>)، فالعلامة بتروح في نفس اللحظة.
    /// </summary>
    public static IApplicationBuilder UseForcedPasswordChange(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (Blocks(context))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;

                await context.Response.WriteAsJsonAsync(new { code = Code, message = Message });
                return;
            }

            await next();
        });
}
