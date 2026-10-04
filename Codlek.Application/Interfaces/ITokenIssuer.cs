using Codlek.Application.Contracts.Auth;

namespace Codlek.Application.Interfaces;

/// <summary>
/// بيوقّع التوكنات وبيفكّها — <b>وبس</b>.
///
/// <para>⚠️ <b>مابيلمسش القاعدة.</b> الجزء اللي بيسجّل ويلفّ
/// التوكنات في جدول <c>RefreshTokens</c> هو
/// <see cref="ILoginSessions"/>. والفصل ده عشان التوقيع والفك
/// يتجرّبوا من غير قاعدة بيانات.</para>
///
/// <para>🔴 <b>والدالتين دول وحدهم مش كفاية لتسجيل دخول.</b> لو كود
/// نده <see cref="Issue"/> على طول، التوكن بيتوقّع ومابيتسجّلش في
/// الجدول — وأول تجديد بيترفض بسبب «توكن مش معروف». فالمداخل كلها
/// بتستعمل <see cref="ILoginSessions"/>.</para>
/// </summary>
public interface ITokenIssuer
{
    TokenPair Issue(TokenSubject subject);

    /// <summary>
    /// بيفك توكن التجديد ويتأكد من توقيعه.
    ///
    /// <para>⚠️ بيرجّع <c>null</c> لو التوقيع غلط أو انتهى أو مش توكن
    /// تجديد أصلاً. <b>ومابيبصّش للقاعدة</b> — ده شغل
    /// <see cref="ILoginSessions"/>.</para>
    /// </summary>
    RefreshClaims? ReadRefresh(string refreshToken);
}
