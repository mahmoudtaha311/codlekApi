using Codlek.Core.Auth;

namespace Codlek.Tests;

/// <summary>
/// قرار التجديد — <b>الست حالات رفض، كل واحدة بفحصها</b>.
///
/// <para>⚠️ ومافيش قاعدة بيانات هنا. ده اللي خلّى الست حالات كلها
/// متجرّبة بدل واحدة أو اتنين.</para>
/// </summary>
public class RefreshRuleTests
{
    private static readonly DateTime Now = new(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>حالة سليمة بالكامل — والفحوص بتغيّر حاجة واحدة فيها.</summary>
    private static RefreshOutcome Evaluate(
        bool tokenKnown = true,
        DateTime? revokedAtUtc = null,
        bool wasRotated = false,
        DateTime? expiresAtUtc = null,
        bool accountActive = true,
        int accountVersion = 3,
        int tokenVersion = 3) =>
        RefreshRules.Evaluate(
            tokenKnown, revokedAtUtc, wasRotated,
            expiresAtUtc ?? Now.AddDays(5),
            accountActive, accountVersion, tokenVersion, Now);

    [Fact]
    public void A_healthy_token_is_allowed() =>
        Assert.Equal(RefreshOutcome.Allowed, Evaluate());

    [Fact]
    public void A_token_with_no_row_is_unknown() =>
        Assert.Equal(RefreshOutcome.UnknownToken, Evaluate(tokenKnown: false));

    /// <summary>
    /// 🔴 توكن اتبدّل واتقدّم تاني = سرقة.
    ///
    /// <para>والفرق عن «اتلغى» إن <c>ReplacedByTokenId</c> مليان: يعني
    /// فيه نسخة جديدة شغّالة في مكان تاني.</para>
    /// </summary>
    [Fact]
    public void A_rotated_token_offered_again_is_theft() =>
        Assert.Equal(RefreshOutcome.Reused,
            Evaluate(revokedAtUtc: Now.AddHours(-1), wasRotated: true));

    /// <summary>
    /// ⚠️ وتوكن اتلغى بالنيّة (خروج) <b>مش</b> سرقة.
    ///
    /// <para>لو الاتنين اتعاملوا بنفس الشكل، كل خروج عادي كان هيسجّل
    /// «اشتباه سرقة» — وبعد أسبوع محدش بيبص على السجل ده خلاص.</para>
    /// </summary>
    [Fact]
    public void A_token_revoked_on_purpose_is_not_theft() =>
        Assert.Equal(RefreshOutcome.Revoked,
            Evaluate(revokedAtUtc: Now.AddHours(-1), wasRotated: false));

    [Fact]
    public void An_expired_token_is_rejected() =>
        Assert.Equal(RefreshOutcome.Expired, Evaluate(expiresAtUtc: Now.AddSeconds(-1)));

    /// <summary>⚠️ وبالثانية بالظبط = منتهي. مفيش تسامح.</summary>
    [Fact]
    public void A_token_expiring_exactly_now_is_expired() =>
        Assert.Equal(RefreshOutcome.Expired, Evaluate(expiresAtUtc: Now));

    [Fact]
    public void A_suspended_account_cannot_refresh() =>
        Assert.Equal(RefreshOutcome.AccountDisabled, Evaluate(accountActive: false));

    /// <summary>🔴 ده هو الطرد: الباسورد اتغيّر فالنسخة زادت.</summary>
    [Fact]
    public void A_stale_credential_version_is_rejected() =>
        Assert.Equal(RefreshOutcome.CredentialsChanged,
            Evaluate(accountVersion: 4, tokenVersion: 3));

    // =================================================================
    //  الترتيب — وده اللي بيضيع لو حد رتّب الفحوص تاني
    // =================================================================

    /// <summary>
    /// 🔴 <b>توكن مسروق <i>ومنتهي</i> لازم يتسجّل «سرقة» مش «انتهى».</b>
    ///
    /// <para>لأن الحرامي لو استنى، التوكن بيبقى الاتنين. فلو الانتهاء
    /// اتفحص الأول، السرقة بتتسجّل بسبب عادي محدش بيبص عليه —
    /// <b>والسلسلة مابتتقفلش، فالحرامي يفضل داخل</b>.</para>
    /// </summary>
    [Fact]
    public void A_stolen_token_that_also_expired_still_reports_theft() =>
        Assert.Equal(RefreshOutcome.Reused,
            Evaluate(revokedAtUtc: Now.AddDays(-9), wasRotated: true,
                     expiresAtUtc: Now.AddDays(-2)));

    /// <summary>
    /// ⚠️ والصف الناقص بيسبق كل حاجة — عشان باقي الفحوص بتبص على
    /// حاجات في الصف، ولو مفيش صف كانت هتبص على قيم افتراضية.
    /// </summary>
    [Fact]
    public void An_unknown_token_is_reported_before_anything_else() =>
        Assert.Equal(RefreshOutcome.UnknownToken,
            Evaluate(tokenKnown: false, accountActive: false,
                     expiresAtUtc: Now.AddDays(-2), accountVersion: 99));

    /// <summary>
    /// ⚠️ والإيقاف بيسبق النسخة، لأن الإيقاف بيزوّد النسخة كمان —
    /// فلو العكس كان كل إيقاف بيتسجّل «الباسورد اتغيّر».
    /// </summary>
    [Fact]
    public void A_suspension_is_reported_before_the_version_bump_it_caused() =>
        Assert.Equal(RefreshOutcome.AccountDisabled,
            Evaluate(accountActive: false, accountVersion: 4, tokenVersion: 3));
}
