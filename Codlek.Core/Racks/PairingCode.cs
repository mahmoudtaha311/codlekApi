using System.Security.Cryptography;

namespace Codlek.Core.Racks;

/// <summary>
/// كود تفعيل محطة فحص — <b>بيتقرا من الشاشة ويتكتب بالإيد</b>.
///
/// <para>🔴 <b>الأبجدية ناقصة حروف عن قصد.</b> الفني بيقرا الكود من
/// شاشة المدير ويكتبه على الراكة، فـ<c>0</c> و<c>O</c> بيبقوا نفس
/// الشكل، وكذلك <c>1</c> و<c>I</c> و<c>L</c>. الكود اللي بيتلخبط
/// بيتحسب محاولة غلط، وخمس محاولات بتقفل الكود — يعني المدير بيضطر
/// يعمل كود جديد لأن حرف اتلخبط.</para>
///
/// <para>⚠️ <b>والشرطة في النص مش تزيين.</b> تمنية حروف ورا بعضها
/// بتتكتب غلط أكتر من أربعة وأربعة — والتسجيل بيشيل الشرطة قبل
/// المقارنة، فالفني اللي كتبها واللي ماكتبهاش الاتنين بيدخلوا.</para>
///
/// <para>⚠️ ومكانه <c>Core</c> عشان طبقة التطبيق تستعمله من غير ما
/// تشاور على البنية التحتية — زي <c>AccountCode</c> بالظبط.</para>
/// </summary>
public static class PairingCode
{
    /// <summary>
    /// 🔴 من غير <c>0</c> و<c>O</c>، ومن غير <c>1</c> و<c>I</c>
    /// و<c>L</c>.
    /// </summary>
    public const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

    /// <summary>طول البادئة المتخزّنة — الكود نفسه عمره ما بيتخزّن.</summary>
    public const int PrefixLength = 4;

    /// <summary>
    /// عمر الكود.
    ///
    /// <para>⚠️ <b>قصير عن قصد.</b> الكود ده بيتحوّل لمفتاح محطة
    /// دايم، فاللي بيفضل صالح أسبوع بيبقى مفتاح احتياطي ساكت لأي حد
    /// شافه على الشاشة.</para>
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    /// <summary>
    /// كود جديد — <c>4F7K-92QX</c> شكلاً.
    ///
    /// <para>⚠️ <b>و<c>RandomNumberGenerator</c> مش <c>Random</c>.</b>
    /// <c>Random</c> بيتزرع بالوقت، فمديرين عملوا كود في نفس اللحظة
    /// كانوا هياخدوا نفس الكود — وده كود بيفتح محطة.</para>
    /// </summary>
    public static string New()
    {
        var chars = new char[8];

        for (int i = 0; i < chars.Length; i++)
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];

        return new string(chars, 0, 4) + "-" + new string(chars, 4, 4);
    }

    /// <summary>
    /// بادئة الكود — <b>اللي بتتخزّن وبتتعرض</b>.
    ///
    /// <para>⚠️ أربع حروف بتضيّق البحث وقت التسجيل وبتخلّي المدير
    /// يعرف أنهي صف هو أنهي كود، <b>من غير</b> ما تكشف الكود.</para>
    /// </summary>
    public static string Prefix(string code) =>
        (code ?? "").Length >= PrefixLength ? code![..PrefixLength] : (code ?? "");
}
