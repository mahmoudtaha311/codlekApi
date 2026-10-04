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
    /// <para>🔴 <b>الرقم ده هو زمن الطرد، مش رقم عشوائي.</b> النظام
    /// القديم كان بيقرا <c>CredentialVersion</c> من القاعدة مع
    /// <b>كل طلب</b>، فإيقاف حساب أو تغيير باسورد كان بيقفل أجهزته
    /// التانية في <b>نفس اللحظة</b>.</para>
    ///
    /// <para>التوكن مابيعملش كده — هو ورقة موقّعة والسيرفر مابيراجعش
    /// القاعدة عشان يقراها. فالطرد بياخد لحد ما التوكن ينتهي والعميل
    /// يطلب واحد جديد، ووقتها بس السيرفر بيبص على القاعدة.</para>
    ///
    /// <para>⚠️ يعني: <b>كل ما الرقم ده يكبر، الطرد يبطأ.</b> ١٥
    /// دقيقة قرار متاخد بعلم — مش سهو.</para>
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
