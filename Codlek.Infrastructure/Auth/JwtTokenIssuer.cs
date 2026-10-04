using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Codlek.Application.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Codlek.Infrastructure.Auth;

/// <summary>
/// بيعمل التوكنات — <b>وأسماء الادعاءات منقولة من النظام القديم بالحرف</b>.
///
/// <para>⚠️ <c>tenant</c> و<c>display</c> و<c>code</c> و<c>ver</c>
/// و<c>must</c> هي نفس أسماء ادعاءات الكوكي القديمة. السبب إن
/// الكنترولرز والسياسات هتقراها بنفس الأسماء، فالتحويل من الكوكي
/// للتوكن مابيلمسش أي كود بيقرا هوية المستخدم.</para>
/// </summary>
public sealed class JwtTokenIssuer : ITokenIssuer
{
    /// <summary>نسخة بيانات الاعتماد — ودي أساس الإلغاء كله.</summary>
    public const string VersionClaim = "ver";

    /// <summary>موجود بس لما يكون مطلوب فعلاً — وجوده هو الإشارة.</summary>
    public const string MustChangeClaim = "must";

    /// <summary>
    /// بيفرّق توكن التجديد عن توكن الوصول.
    ///
    /// <para>🔴 <b>من غيره، توكن الوصول ينفع يتقدّم كتوكن تجديد
    /// والعكس.</b> والنتيجة إن توكن وصول عمره ١٥ دقيقة يتحوّل لواحد
    /// جديد كل ربع ساعة للأبد — يعني الطرد مابيحصلش خالص.</para>
    /// </summary>
    public const string TokenUseClaim = "use";

    public const string RefreshUse = "refresh";
    public const string AccessUse = "access";

    private readonly JwtOptions _options;
    private readonly SigningCredentials _signing;

    public JwtTokenIssuer(IOptions<JwtOptions> options)
    {
        _options = options.Value;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        _signing = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    public TokenPair Issue(TokenSubject subject)
    {
        var now = DateTime.UtcNow;

        // ⚠️ نفس أسماء ادعاءات الكوكي القديمة بالحرف.
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subject.UserId.ToString()),
            new(ClaimTypes.NameIdentifier, subject.UserId.ToString()),
            new(ClaimTypes.Name, subject.Username),
            new(ClaimTypes.Role, subject.Role),
            new("tenant", subject.TenantId.ToString()),
            new("display", subject.DisplayName),
            new("code", subject.Code),
            new(VersionClaim, subject.CredentialVersion.ToString(CultureInfo.InvariantCulture)),
            new(TokenUseClaim, AccessUse)
        };

        if (subject.MustChangePassword)
            claims.Add(new Claim(MustChangeClaim, "1"));

        string access = Write(claims, now.AddMinutes(_options.AccessTokenMinutes), now);

        /*
          ⚠️ **توكن التجديد شايل أقل حاجة ممكنة:** مين، ونسخة بياناته.

          مش بيشيل الدور ولا الاسم ولا الشركة — لأن كل دي بتتقرا من
          القاعدة وقت التجديد وهي أحدث. توكن تجديد شايل الدور معناه
          إن ترقية أو تنزيل الصلاحية بتاخد أسبوع عشان تبان.
        */
        string refresh = Write(
            new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, subject.UserId.ToString()),
                new(VersionClaim, subject.CredentialVersion.ToString(CultureInfo.InvariantCulture)),
                new(TokenUseClaim, RefreshUse)
            },
            now.AddDays(_options.RefreshTokenDays),
            now);

        return new TokenPair(access, refresh, _options.AccessTokenMinutes * 60);
    }

    public RefreshClaims? ReadRefresh(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return null;

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _options.Issuer,
            ValidateAudience = true,
            ValidAudience = _options.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key)),
            ValidateLifetime = true,

            // ⚠️ صفر تسامح في الوقت. الافتراضي ٥ دقايق، وده بيمدّ عمر
            // كل توكن بخمس دقايق من غير ما حد يقصد.
            ClockSkew = TimeSpan.Zero
        };

        try
        {
            /*
              🔴 **`MapInboundClaims = false` — من غيرها الدالة دي
              بترجّع `null` على توكن سليم.**

              `JwtSecurityTokenHandler` افتراضياً بيترجم أسماء
              الادعاءات القياسية لأسماء XML قديمة: `sub` بتبقى
              `http://schemas.xmlsoap.org/.../nameidentifier`. فالبحث
              عن `sub` بيرجع فاضي، والتجديد بيترفض وكل مستخدم بيتطرد
              كل ١٥ دقيقة — من غير أي خطأ ظاهر.

              ⚠️ وبتتحط على النسخة دي مش على الإعداد العام: الإعداد
              العام حالة مشتركة بتتغيّر من تحت أي كود تاني.
            */
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };

            var principal = handler.ValidateToken(refreshToken, parameters, out _);

            // 🔴 **لازم يكون توكن تجديد فعلاً.** من غير الفحص ده، توكن
            // الوصول ينفع يتقدّم هنا ويطلّع واحد جديد — فالطرد عمره
            // ما بيحصل.
            if (principal.FindFirst(TokenUseClaim)?.Value != RefreshUse) return null;

            if (!Guid.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,
                               out Guid id))
                return null;

            if (!int.TryParse(principal.FindFirst(VersionClaim)?.Value,
                              NumberStyles.Integer, CultureInfo.InvariantCulture, out int version))
                return null;

            return new RefreshClaims(id, version);
        }
        catch
        {
            // ⚠️ توقيع غلط أو منتهي أو مش توكن أصلاً — كلهم «لأ».
            // التفرقة بينهم بتقول للي بيجرّب إيه اللي قرّب يظبط.
            return null;
        }
    }

    private string Write(List<Claim> claims, DateTime expires, DateTime now) =>
        new JwtSecurityTokenHandler().WriteToken(
            new JwtSecurityToken(
                issuer: _options.Issuer,
                audience: _options.Audience,
                claims: claims,
                notBefore: now,
                expires: expires,
                signingCredentials: _signing));
}
