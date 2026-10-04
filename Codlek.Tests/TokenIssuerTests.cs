using Codlek.Application.Interfaces;
using Codlek.Infrastructure.Auth;
using Microsoft.Extensions.Options;

namespace Codlek.Tests;

/// <summary>
/// التوكنات — <b>والإلغاء هو كل الموضوع</b>.
///
/// <para>🔴 النظام القديم كان بيقرا <c>CredentialVersion</c> من
/// القاعدة مع <b>كل طلب</b>، فإيقاف حساب أو تغيير باسورد كان بيقفل
/// أجهزته التانية في نفس اللحظة. التوكن مابيعملش كده — فالفحوص هنا
/// بتثبّت إن اللي حلّ محلّه شغّال فعلاً.</para>
/// </summary>
public class TokenIssuerTests
{
    private static JwtTokenIssuer Issuer(int accessMinutes = 15, int refreshDays = 7) =>
        new(Options.Create(new JwtOptions
        {
            // ⚠️ مفتاح للفحص بس — الحقيقي بييجي من البيئة.
            Key = "فحص-فحص-فحص-فحص-فحص-فحص-فحص-فحص-1234567890",
            Issuer = "codlek-test",
            Audience = "codlek-test",
            AccessTokenMinutes = accessMinutes,
            RefreshTokenDays = refreshDays
        }));

    private static TokenSubject Someone(int version = 3, bool mustChange = false) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "ahmed", "أحمد", "100001",
            "Manager", version, mustChange);

    // =================================================================
    //  الإصدار
    // =================================================================

    [Fact]
    public void A_login_gives_two_different_tokens()
    {
        var pair = Issuer().Issue(Someone());

        Assert.False(string.IsNullOrWhiteSpace(pair.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(pair.RefreshToken));
        Assert.NotEqual(pair.AccessToken, pair.RefreshToken);
    }

    /// <summary>⚠️ العميل محتاج يعرف يجدّد قبل الانتهاء.</summary>
    [Fact]
    public void The_client_is_told_how_long_the_access_token_lives()
    {
        Assert.Equal(15 * 60, Issuer(accessMinutes: 15).Issue(Someone()).ExpiresInSeconds);
        Assert.Equal(45 * 60, Issuer(accessMinutes: 45).Issue(Someone()).ExpiresInSeconds);
    }

    // =================================================================
    //  🔴 الفصل بين النوعين — وده أهم فحص في الملف
    // =================================================================

    /// <summary>
    /// 🔴 <b>توكن الوصول ماينفعش يتقدّم كتوكن تجديد.</b>
    ///
    /// <para>من غير الفصل ده، توكن وصول عمره ١٥ دقيقة بيتحوّل لواحد
    /// جديد كل ربع ساعة <b>للأبد</b> — يعني الطرد عمره ما بيحصل،
    /// وكل الكلام عن «الإيقاف بياخد ١٥ دقيقة» بيبقى مش صحيح.</para>
    /// </summary>
    [Fact]
    public void An_access_token_is_refused_as_a_refresh_token()
    {
        var pair = Issuer().Issue(Someone());

        Assert.Null(Issuer().ReadRefresh(pair.AccessToken));
    }

    [Fact]
    public void A_refresh_token_reads_back_who_and_which_version()
    {
        var subject = Someone(version: 7);
        var pair = Issuer().Issue(subject);

        var claims = Issuer().ReadRefresh(pair.RefreshToken);

        Assert.NotNull(claims);
        Assert.Equal(subject.UserId, claims!.UserId);
        Assert.Equal(7, claims.CredentialVersion);
    }

    /// <summary>
    /// ⚠️ وتوكن التجديد <b>مش</b> شايل الدور.
    ///
    /// <para>لو كان شايله، ترقية أو تنزيل صلاحية كانت هتاخد أسبوع
    /// عشان تبان. الدور بيتقرا من القاعدة وقت التجديد.</para>
    /// </summary>
    [Fact]
    public void A_refresh_token_carries_nothing_but_identity_and_version()
    {
        var pair = Issuer().Issue(Someone());

        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler()
            .ReadJwtToken(pair.RefreshToken);

        var names = token.Claims.Select(c => c.Type).ToList();

        Assert.DoesNotContain(names, n => n.Contains("role", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("tenant", names);
        Assert.DoesNotContain("display", names);
        Assert.DoesNotContain("code", names);
    }

    // =================================================================
    //  الرفض
    // =================================================================

    [Fact]
    public void A_token_signed_with_another_key_is_refused()
    {
        var pair = Issuer().Issue(Someone());

        var other = new JwtTokenIssuer(Options.Create(new JwtOptions
        {
            Key = "مفتاح-تاني-خالص-مفتاح-تاني-خالص-0987654321",
            Issuer = "codlek-test",
            Audience = "codlek-test"
        }));

        Assert.Null(other.ReadRefresh(pair.RefreshToken));
    }

    /// <summary>
    /// 🔴 <b>المنتهي بيترفض — ومن غير تسامح في الوقت.</b>
    ///
    /// <para>الافتراضي في المكتبة <b>٥ دقايق تسامح</b>، يعني توكن
    /// منتهي من أربع دقايق بيعدّي. وده بيمدّ عمر كل توكن خمس دقايق
    /// من غير ما حد يقصد — ومعاه زمن الطرد.</para>
    ///
    /// <para>⚠️ <b>والفحص ده اتكتب غلط أول مرة:</b> كان بيقيس عمر
    /// التوكن المكتوب بدل ما يقيس التسامح وقت القراية، فالمسخ
    /// (رجّع التسامح للافتراضي) عدّى منه. دلوقتي بيعمل توكن
    /// <b>منتهي فعلاً</b> ويتأكد إنه بيترفض.</para>
    /// </summary>
    [Fact]
    public void An_expired_refresh_token_is_refused_with_no_grace()
    {
        var issuer = Issuer();

        // ⚠️ توكن انتهى من دقيقتين — جوّه نافذة التسامح الافتراضية
        // (٥ دقايق) وبرّه الصفر اللي إحنا ظابطينه.
        string expired = ExpiredRefreshToken(DateTime.UtcNow.AddMinutes(-2));

        Assert.Null(issuer.ReadRefresh(expired));
    }

    /// <summary>⚠️ والشاهد الموجب: توكن لسه صالح بيعدّي.</summary>
    [Fact]
    public void A_live_refresh_token_is_accepted()
    {
        var issuer = Issuer();
        var pair = issuer.Issue(Someone());

        Assert.NotNull(issuer.ReadRefresh(pair.RefreshToken));
    }

    /// <summary>
    /// بيعمل توكن تجديد بتاريخ انتهاء في الماضي — بنفس المفتاح
    /// والمُصدِر عشان الرفض يبقى بسبب الوقت وبس.
    /// </summary>
    private static string ExpiredRefreshToken(DateTime expiredAt)
    {
        var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes(
                "فحص-فحص-فحص-فحص-فحص-فحص-فحص-فحص-1234567890"));

        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: "codlek-test",
            audience: "codlek-test",
            claims: new[]
            {
                new System.Security.Claims.Claim(
                    System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub,
                    Guid.NewGuid().ToString()),
                new System.Security.Claims.Claim(JwtTokenIssuer.VersionClaim, "1"),
                new System.Security.Claims.Claim(
                    JwtTokenIssuer.TokenUseClaim, JwtTokenIssuer.RefreshUse)
            },
            notBefore: expiredAt.AddMinutes(-10),
            expires: expiredAt,
            signingCredentials: new Microsoft.IdentityModel.Tokens.SigningCredentials(
                key, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256));

        return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("مش-توكن-خالص")]
    [InlineData("a.b.c")]
    public void Rubbish_is_refused_without_throwing(string rubbish) =>
        Assert.Null(Issuer().ReadRefresh(rubbish));

    /// <summary>
    /// 🔴 <b>توكن من مُصدِر تاني بيترفض — حتى لو موقّع بنفس المفتاح.</b>
    ///
    /// <para>الفحص ده اتكتب لأن مسخ عاش: شلت <c>ValidateIssuer</c>
    /// و<c>ValidateAudience</c> والفحوص كلها فضلت خضرا. يعني مكانش
    /// فيه حاجة بتحرسهم.</para>
    ///
    /// <para>⚠️ <b>وده مش نظري.</b> لو المفتاح اتشارك مع نظام تاني
    /// (بيئة تجربة، خدمة تانية بنفس السر)، توكن منه بيعدّي هنا ويدخّل
    /// حد مالوش علاقة. التحقق من المُصدِر هو اللي بيمنع ده.</para>
    /// </summary>
    [Fact]
    public void A_token_from_another_issuer_is_refused_even_with_the_same_key()
    {
        const string sameKey = "فحص-فحص-فحص-فحص-فحص-فحص-فحص-فحص-1234567890";

        var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes(sameKey));

        var foreign = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: "نظام-تاني",
            audience: "codlek-test",
            claims: new[]
            {
                new System.Security.Claims.Claim(
                    System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub,
                    Guid.NewGuid().ToString()),
                new System.Security.Claims.Claim(JwtTokenIssuer.VersionClaim, "1"),
                new System.Security.Claims.Claim(
                    JwtTokenIssuer.TokenUseClaim, JwtTokenIssuer.RefreshUse)
            },
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddDays(1),
            signingCredentials: new Microsoft.IdentityModel.Tokens.SigningCredentials(
                key, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256));

        string token = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler()
            .WriteToken(foreign);

        Assert.Null(Issuer().ReadRefresh(token));
    }

    /// <summary>⚠️ وجمهور تاني برضه — نفس السبب.</summary>
    [Fact]
    public void A_token_for_another_audience_is_refused()
    {
        const string sameKey = "فحص-فحص-فحص-فحص-فحص-فحص-فحص-فحص-1234567890";

        var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes(sameKey));

        var foreign = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: "codlek-test",
            audience: "تطبيق-تاني",
            claims: new[]
            {
                new System.Security.Claims.Claim(
                    System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub,
                    Guid.NewGuid().ToString()),
                new System.Security.Claims.Claim(JwtTokenIssuer.VersionClaim, "1"),
                new System.Security.Claims.Claim(
                    JwtTokenIssuer.TokenUseClaim, JwtTokenIssuer.RefreshUse)
            },
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddDays(1),
            signingCredentials: new Microsoft.IdentityModel.Tokens.SigningCredentials(
                key, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256));

        string token = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler()
            .WriteToken(foreign);

        Assert.Null(Issuer().ReadRefresh(token));
    }

    // =================================================================
    //  الادعاءات — أسماؤها عقد مع باقي النظام
    // =================================================================

    /// <summary>
    /// ⚠️ <b>نفس أسماء ادعاءات الكوكي القديمة بالحرف.</b>
    ///
    /// <para>السياسات والكنترولرز بتقرا الهوية بالأسماء دي. لو
    /// اتغيّرت، التحويل من الكوكي للتوكن بيكسر كل حاجة بتقرا
    /// مستخدم — من غير خطأ بناء.</para>
    /// </summary>
    [Fact]
    public void The_access_token_carries_the_same_claim_names_as_the_old_cookie()
    {
        var subject = Someone(version: 4, mustChange: true);
        var pair = Issuer().Issue(subject);

        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler()
            .ReadJwtToken(pair.AccessToken);

        string? Claim(string name) => token.Claims.FirstOrDefault(c => c.Type == name)?.Value;

        Assert.Equal(subject.TenantId.ToString(), Claim("tenant"));
        Assert.Equal(subject.DisplayName, Claim("display"));
        Assert.Equal(subject.Code, Claim("code"));
        Assert.Equal("4", Claim(JwtTokenIssuer.VersionClaim));
        Assert.Equal("1", Claim(JwtTokenIssuer.MustChangeClaim));
    }

    /// <summary>
    /// ⚠️ و«لازم يغيّر الباسورد» <b>وجودها</b> هو الإشارة.
    ///
    /// <para>نفس قاعدة النظام القديم: مابتتحطّش بقيمة "0"، فمفيش
    /// احتمال إن حد يقراها غلط.</para>
    /// </summary>
    [Fact]
    public void The_must_change_claim_is_absent_when_it_does_not_apply()
    {
        var pair = Issuer().Issue(Someone(mustChange: false));

        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler()
            .ReadJwtToken(pair.AccessToken);

        Assert.DoesNotContain(token.Claims, c => c.Type == JwtTokenIssuer.MustChangeClaim);
    }
}
