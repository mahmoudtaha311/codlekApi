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
/// <para>🔴 <b>والشرطة <u>جزء من السر</u> — مش تزيين ومابتتشالش.</b>
/// البصمة في القاعدة بتتعمل على النص <b>بشرطته</b>
/// (<c>PasswordHasher.Create(code)</c>)، والتسجيل بيقارن النص زي ما
/// اتكتب — التنضيف الوحيد <c>Trim</c> و<c>ToUpperInvariant</c>،
/// ومفيش ولا <c>Replace("-")</c> في المشروع القديم كله.</para>
///
/// <para>🔴 <b>وده تصحيح لتعليق كان غلط هنا.</b> التعليق القديم كان
/// بيقول إن التسجيل بيشيل الشرطة قبل المقارنة. لو حد نفّذ الكلام ده،
/// <b>كل</b> تسجيل في الميدان بياخد <c>404</c> (تمنية حروف عمرها ما
/// هتطابق بصمة تسعة)، <b>وكل محاولة بتحرق واحدة من خمس</b> من كل كود
/// مستني في نفس البادئة — في كل الشركات. خمس فنيين بيحاولوا بيقفلوا
/// البادئة كلها.</para>
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
