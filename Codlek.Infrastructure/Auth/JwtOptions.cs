using System.ComponentModel.DataAnnotations;

namespace Codlek.Infrastructure.Auth;

/// <summary>
/// إعدادات التوكن.
///
/// <para>🔴 <b>المفتاح بييجي من البيئة، عمره ما يتكتب في
/// <c>appsettings.json</c>.</b> في FixFlow المفتاح مكتوب في الملف
/// ومرفوع مع الكود — وده بالظبط اللي اتفقنا مناخدهوش. اللي يقرا
/// المفتاح يقدر يعمل توكن لأي حساب.</para>
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>⚠️ من <c>Jwt__Key</c> في البيئة. ٣٢ حرف على الأقل.</summary>
    [Required, MinLength(32)]
    public string Key { get; set; } = "";

    [Required]
    public string Issuer { get; set; } = "codlek";

    [Required]
    public string Audience { get; set; } = "codlek";

    /// <summary>
    /// عمر توكن الوصول — <b>بالدقايق</b>.
    ///
    /// <para>🔴 <b>الرقم ده مابقاش زمن الطرد.</b> زي القديم، كل طلب لوحة
    /// بيتفحص على صف الحساب (<c>AccountStanding</c>): الإيقاف أو تغيير
    /// الباسورد أو إعادة تعيينه بيطرد من أول طلب — فوراً على السيرفر اللي
    /// اتعمل عليه، ولحد ثواني (عمر الكاش) على أي نسخة تانية.</para>
    ///
    /// <para>⚠️ اللي الرقم ده لسه بيتحكّم فيه: كل قد إيه الادعاءات اللي
    /// مش بتتفحص كل طلب (الدور، الاسم، علامة الباسورد المؤقت) بتتقرا من
    /// القاعدة تاني — وده بيحصل وقت التجديد. فكبره معناه إن ترقية أو
    /// تنزيل صلاحية بياخد وقت أطول يبان.</para>
    /// </summary>
    [Range(1, 240)]
    public int AccessTokenMinutes { get; set; } = 15;

    /// <summary>
    /// عمر توكن التجديد — <b>بالأيام</b>.
    ///
    /// <para>ده اللي بيخلّي المستخدم مايكتبش الباسورد كل ربع ساعة.
    /// وكل تجديد بيعدّي على القاعدة، فهو نقطة الطرد الحقيقية.</para>
    /// </summary>
    [Range(1, 90)]
    public int RefreshTokenDays { get; set; } = 7;
}
