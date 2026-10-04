namespace Codlek.Application.Contracts.Auth;

/// <summary>
/// رد الدخول والتجديد.
///
/// <para>⚠️ <c>MustChangePassword</c> في الرد <b>وفي التوكن</b>. اللي
/// في التوكن هو اللي السيرفر بيتصرّف بيه، واللي هنا عشان الواجهة
/// تعرض الشاشة الصح من غير ما تفك التوكن.</para>
/// </summary>
public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresInSeconds,
    Guid UserId,
    string DisplayName,
    string Role,
    bool MustChangePassword);
