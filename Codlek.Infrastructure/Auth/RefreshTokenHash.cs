using System.Security.Cryptography;
using System.Text;

namespace Codlek.Infrastructure.Auth;

/// <summary>
/// بصمة توكن التجديد — <b>اللي بيتخزّن في الجدول</b>.
///
/// <para>🔴 <b>التوكن نفسه عمره ما بيتخزّن.</b> نفس قاعدة الباسوردات:
/// لو حد قرا الجدول — نسخة احتياطية، سجل، موظف — مايقدرش يستعمل اللي
/// فيه. ومالوش لازمة يتخزّن أصلاً: اللي محتاجينه إننا نتعرّف على توكن
/// جايّ، ودي البصمة بتعملها.</para>
///
/// <para>⚠️ <b>ومفيش ملح هنا، بخلاف الباسوردات.</b> التوكن أصلاً
/// ٣٢ بايت عشوائية موقّعة — مفيش قائمة كلمات شائعة حد يجرّبها عليه.
/// الملح بيحمي من الجداول الجاهزة، ودي مالهاش معنى مع قيمة عشوائية.
/// وSHA-256 بسيطة معناها إن البحث في الجدول فهرس واحد.</para>
/// </summary>
public static class RefreshTokenHash
{
    /// <summary>بصمة base64 — ٤٤ حرف ثابت.</summary>
    public static string Of(string token) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token ?? "")));
}
