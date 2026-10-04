namespace Codlek.Core.Auth;

/// <summary>
/// قرار التجديد — <b>دالة نقية، مافيهاش قاعدة بيانات</b>.
///
/// <para>🔴 <b>ليه منفصلة عن الكود اللي بيقرا القاعدة.</b> القرار ده
/// فيه ٦ حالات رفض وترتيبهم مهم. لو كان متشتّت جوّه كود بيستعلم
/// ويحدّث، كان لازم قاعدة بيانات حقيقية عشان نجرّب كل حالة — وبالتالي
/// كانوا هيتجرّبوا واحد أو اتنين وبس.</para>
///
/// <para>كده الست حالات بتتجرّب في مللي ثانية، وكل واحدة فيها فحص
/// باسمها.</para>
/// </summary>
public static class RefreshRules
{
    /// <param name="tokenKnown">فيه صف للتوكن ده؟</param>
    /// <param name="revokedAtUtc">اتلغى إمتى — <c>null</c> يعني لسه شغّال.</param>
    /// <param name="wasRotated">اتبدّل بواحد جديد؟ (<c>ReplacedByTokenId</c> مليان)</param>
    /// <param name="expiresAtUtc">بينتهي إمتى.</param>
    /// <param name="accountActive">الحساب مفعّل؟</param>
    /// <param name="accountVersion">نسخة الاعتماد في القاعدة دلوقتي.</param>
    /// <param name="tokenVersion">النسخة اللي كانت وقت عمل التوكن.</param>
    /// <param name="nowUtc">الوقت.</param>
    public static RefreshOutcome Evaluate(
        bool tokenKnown,
        DateTime? revokedAtUtc,
        bool wasRotated,
        DateTime expiresAtUtc,
        bool accountActive,
        int accountVersion,
        int tokenVersion,
        DateTime nowUtc)
    {
        // ١ · مفيش صف خالص.
        if (!tokenKnown) return RefreshOutcome.UnknownToken;

        /*
          ٢ · 🔴 **السرقة الأول، قبل أي سبب تاني.**

          لأن توكن مسروق **بيبقى منتهي كمان** لو الحرامي استنى. فلو
          فحصنا الانتهاء الأول، حالة السرقة كانت هتتسجّل «انتهى» —
          وهو سبب عادي محدش بيبص عليه. وبالتالي السرقة بتعدّي في
          سكوت، وأسوأ: السلسلة مابتتقفلش فالحرامي يفضل داخل.
        */
        if (revokedAtUtc is not null)
            return wasRotated ? RefreshOutcome.Reused : RefreshOutcome.Revoked;

        // ٣ · عدّى عمره.
        if (expiresAtUtc <= nowUtc) return RefreshOutcome.Expired;

        /*
          ٤ · ⚠️ **الإيقاف قبل النسخة.**

          الاتنين بيرفضوا، بس السبب اللي بيتسجّل بيفرق: «الحساب موقوف»
          بيفهّم اللي بيبص على السجل إن ده إجراء إداري، و«النسخة
          اتغيّرت» بيفهّمه إن الباسورد اتغيّر. الإيقاف بيزوّد النسخة
          كمان، فلو العكس كان كل إيقاف بيتسجّل «الباسورد اتغيّر».
        */
        if (!accountActive) return RefreshOutcome.AccountDisabled;

        if (accountVersion != tokenVersion) return RefreshOutcome.CredentialsChanged;

        return RefreshOutcome.Allowed;
    }
}
