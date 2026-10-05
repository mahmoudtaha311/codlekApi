namespace Codlek.Core.Auth;

/// <summary>
/// توكن الوصول لسه صالح لصاحبه؟ — <b>دالة نقية، مافيهاش قاعدة</b>.
///
/// <para>🔴 <b>ليه الفحص ده موجود أصلاً.</b> التوقيع بيقول إن التوكن
/// اتعمل هنا، مابيقولش إن صاحبه لسه مسموحله. من غير الفحص ده الحساب
/// الموقوف أو اللي باسورده اتغيّر بيفضل شغّال لحد ما التوكن يخلص —
/// ربع ساعة. والقديم كان بيطرده من أول طلب (<c>CookieSessionGuard</c>)،
/// والمالك عايز نفس الحاجة.</para>
/// </summary>
public static class AccessTokenRules
{
    /// <param name="accountExists">الحساب لسه موجود؟</param>
    /// <param name="accountActive">مفعّل؟</param>
    /// <param name="accountTenant">شركته في القاعدة دلوقتي.</param>
    /// <param name="accountVersion">نسخة الاعتماد في القاعدة دلوقتي.</param>
    /// <param name="tokenTenant">الشركة اللي في التوكن.</param>
    /// <param name="tokenVersion">النسخة اللي في التوكن (<c>ver</c>).</param>
    public static bool Allows(
        bool accountExists,
        bool accountActive,
        Guid accountTenant,
        int accountVersion,
        Guid tokenTenant,
        int tokenVersion) =>
        accountExists
        && accountActive

        // 🔴 النسخة لازم تساوي بالظبط — مش «أكبر من أو يساوي». الإيقاف
        // وإعادة التعيين وتغيير الباسورد كلهم بيزوّدوها، فأي فرق معناه
        // إن التوكن ده من قبل واحدة منهم.
        && accountVersion == tokenVersion

        // ⚠️ حساب اتنقل لشركة تانية = جلسة مابقتش صالحة، مش حساب تاني.
        // نفس اللي بيعمله تغيير الباسورد.
        && accountTenant == tokenTenant;
}
