using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Codlek.Application.Contracts.Account;
using Codlek.Core.Auth;
using Codlek.Core.Enums;
using Codlek.Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Codlek.Tests;

/// <summary>
/// 🔴 <b>الطرد من أول طلب — زي القديم.</b>
///
/// <para>التوكن عمره ١٥ دقيقة، وكان بيتفحص على الحساب وقت التجديد بس.
/// يعني المالك يوقف حساب لابتوبه اتسرق — وشاشة الدخول ترفضه فعلاً،
/// <b>فالمالك يشوف إن الإيقاف اشتغل</b> — والتاب المفتوح يفضل يصدّر
/// المخزن ربع ساعة. القديم (<c>CookieSessionGuard</c>) كان بيطرده من
/// أول طلب.</para>
///
/// <para>⚠️ كل فحص هنا بيستعمل <b>نفس</b> الـ<c>HttpClient</c> بنفس
/// التوكن قبل وبعد — والطلب الأول بيسخّن الكاش، فالـ<c>401</c> بعده
/// بيثبت إن المسح فوري مش إن الكاش لسه فاضي.</para>
/// </summary>
[Collection(DashboardServer.Collection)]
public class ImmediateLogoutTests(DashboardServer server)
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private const string Probe = "/api/v1/users";

    private async Task<HttpClient> OwnerAsync() =>
        server.ClientFor(await server.AddUserAsync(UserRole.Owner));

    private static async Task AssertStatus(HttpClient client, HttpStatusCode expected, string path = Probe)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task Suspending_kills_the_open_token_on_the_next_request()
    {
        var victim = await server.AddUserAsync(UserRole.Manager);
        using var tab = server.ClientFor(victim);

        // ⚠️ إثبات إن التوكن كان شغّال — وتسخين الكاش.
        await AssertStatus(tab, HttpStatusCode.OK);

        using var owner = await OwnerAsync();
        using (var suspend = await owner.PostAsJsonAsync(
                   $"/api/v1/users/{victim.Id}/suspend", new { reason = "لابتوبه اتسرق" }))
            Assert.Equal(HttpStatusCode.OK, suspend.StatusCode);

        await AssertStatus(tab, HttpStatusCode.Unauthorized);

        // ⚠️ و«أنا مين» كمان — اللوحة بتسألها وهي بتقرر تروح على الدخول.
        await AssertStatus(tab, HttpStatusCode.Unauthorized, "/api/v1/auth/me");
    }

    [Fact]
    public async Task A_password_reset_by_an_admin_kills_the_old_token()
    {
        var victim = await server.AddUserAsync(UserRole.Manager);
        using var tab = server.ClientFor(victim);

        await AssertStatus(tab, HttpStatusCode.OK);

        using var owner = await OwnerAsync();
        using (var reset = await owner.PostAsJsonAsync(
                   $"/api/v1/users/{victim.Id}/password", new { password = "Kicked-Out-123" }))
            Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        await AssertStatus(tab, HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// 🔴 <b>تغيير باسوردي بيطفّي التوكن القديم — والجديد اللي راجع في
    /// الرد بيشتغل من أول طلب.</b> لو المسح مش فوري أو النسخة مش متطابقة،
    /// المستخدم بينجح في التغيير وبعدين يلاقي نفسه مطرود.
    /// </summary>
    [Fact]
    public async Task Changing_my_password_kills_the_old_token_but_not_the_new_one()
    {
        var me = await server.AddUserAsync(UserRole.Manager);
        using var oldTab = server.ClientFor(me);

        await AssertStatus(oldTab, HttpStatusCode.OK);

        using var change = await oldTab.PostAsJsonAsync("/api/v1/account/change-password",
            new ChangePasswordRequest(DashboardServer.Password, "Brand-New-456", "Brand-New-456"));
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);

        var fresh = await change.Content.ReadFromJsonAsync<PasswordChangedResponse>(Web);
        Assert.False(string.IsNullOrWhiteSpace(fresh!.AccessToken));

        await AssertStatus(oldTab, HttpStatusCode.Unauthorized);

        using var newTab = server.ClientWith(fresh.AccessToken);
        await AssertStatus(newTab, HttpStatusCode.OK);
    }

    /// <summary>
    /// ⚠️ <b>الشاهد المقابل.</b> حارس بيرفض كل حاجة بيعدّي على الفحوص
    /// اللي فوق وهو كاسر اللوحة كلها — والحلقة بتكشف حارس بيقبل أول
    /// طلب وبس (أو كاش بيبوّظ التاني).
    /// </summary>
    [Fact]
    public async Task An_untouched_account_keeps_working()
    {
        var bystander = await server.AddUserAsync(UserRole.Manager);
        var other = await server.AddUserAsync(UserRole.Manager);

        using var tab = server.ClientFor(bystander);
        using var owner = await OwnerAsync();

        using (var suspend = await owner.PostAsJsonAsync(
                   $"/api/v1/users/{other.Id}/suspend", new { reason = "حساب تاني خالص" }))
            Assert.Equal(HttpStatusCode.OK, suspend.StatusCode);

        for (int i = 0; i < 5; i++)
            await AssertStatus(tab, HttpStatusCode.OK);
    }

    /// <summary>🔴 والتفعيل مابيرجّعش التوكن القديم للحياة — النسخة اتزوّدت وقت الإيقاف.</summary>
    [Fact]
    public async Task Reactivating_does_not_bring_the_old_token_back()
    {
        var victim = await server.AddUserAsync(UserRole.Manager);
        using var tab = server.ClientFor(victim);
        using var owner = await OwnerAsync();

        await AssertStatus(tab, HttpStatusCode.OK);

        using (var s = await owner.PostAsJsonAsync(
                   $"/api/v1/users/{victim.Id}/suspend", new { reason = "إيقاف مؤقت" }))
            Assert.Equal(HttpStatusCode.OK, s.StatusCode);

        using (var a = await owner.PostAsync($"/api/v1/users/{victim.Id}/activate", null))
            Assert.Equal(HttpStatusCode.OK, a.StatusCode);

        await AssertStatus(tab, HttpStatusCode.Unauthorized);

        // ⚠️ بس دخول جديد بعد التفعيل شغّال من أول طلب — يعني التفعيل
        // مسح حالة «موقوف» من الكاش، ماستناش عمره.
        await using var db = server.CreateDb();
        var row = await db.Users.AsNoTracking().SingleAsync(u => u.Id == victim.Id);

        using var again = server.ClientFor(row);
        await AssertStatus(again, HttpStatusCode.OK);
    }

    /// <summary>
    /// 🔴 <b>الكاش موجود فعلاً، والمسح هو اللي بيخلّي الطرد فوري.</b>
    ///
    /// <para>التغيير هنا بيتكتب في القاعدة <b>من برّه الـAPI</b> (زي نسخة
    /// سيرفر تانية) — فالطلب اللي بعده لسه بيعدّي من الكاش. وبعد
    /// <c>Forget</c> بيترفض على طول.</para>
    /// </summary>
    [Fact]
    public async Task The_cache_holds_until_forgotten_then_the_kick_is_immediate()
    {
        var victim = await server.AddUserAsync(UserRole.Manager);
        using var tab = server.ClientFor(victim);

        await AssertStatus(tab, HttpStatusCode.OK);

        await using (var db = server.CreateDb())
        {
            await db.Users.Where(u => u.Id == victim.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false));
        }

        // ⚠️ جوّه نافذة الكاش — لو الفحص ده وقع، يا إما مفيش كاش (كل
        // طلب بيستعلم) يا إما النافذة أقصر من الوقت اللي الفحص أخده.
        await AssertStatus(tab, HttpStatusCode.OK);

        server.Services.GetRequiredService<AccountStanding>().Forget(victim.Id);

        await AssertStatus(tab, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_deleted_account_is_kicked()
    {
        var victim = await server.AddUserAsync(UserRole.Manager);
        using var tab = server.ClientFor(victim);

        await AssertStatus(tab, HttpStatusCode.OK);

        await using (var db = server.CreateDb())
        {
            await db.RefreshTokens.Where(t => t.UserId == victim.Id).ExecuteDeleteAsync();
            await db.Users.Where(u => u.Id == victim.Id).ExecuteDeleteAsync();
        }

        server.Services.GetRequiredService<AccountStanding>().Forget(victim.Id);

        await AssertStatus(tab, HttpStatusCode.Unauthorized);
    }

    /// <summary>⚠️ الـ<c>401</c> هو نفس رد التوكن المنتهي — من غير جسم، واللوحة بتروح على الدخول.</summary>
    [Fact]
    public async Task The_kick_looks_like_an_expired_token()
    {
        var victim = await server.AddUserAsync(UserRole.Manager);
        using var tab = server.ClientFor(victim);

        await using (var db = server.CreateDb())
        {
            await db.Users.Where(u => u.Id == victim.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.CredentialVersion, 2));
        }

        server.Services.GetRequiredService<AccountStanding>().Forget(victim.Id);

        using var response = await tab.GetAsync(Probe);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(response.Headers.WwwAuthenticate, h => h.Scheme == "Bearer");
        Assert.Equal("", await response.Content.ReadAsStringAsync());
    }
}

/// <summary>القاعدة النقية — كل سبب رفض لوحده.</summary>
public class AccessTokenRuleTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    [Fact]
    public void An_active_account_on_the_same_version_is_allowed() =>
        Assert.True(AccessTokenRules.Allows(true, true, Tenant, 3, Tenant, 3));

    [Fact]
    public void A_missing_account_is_refused() =>
        Assert.False(AccessTokenRules.Allows(false, true, Tenant, 3, Tenant, 3));

    [Fact]
    public void A_suspended_account_is_refused() =>
        Assert.False(AccessTokenRules.Allows(true, false, Tenant, 3, Tenant, 3));

    [Theory]
    [InlineData(4, 3)]
    [InlineData(3, 4)]
    public void Any_version_difference_is_refused(int account, int token) =>
        Assert.False(AccessTokenRules.Allows(true, true, Tenant, account, Tenant, token));

    [Fact]
    public void Another_tenant_is_refused() =>
        Assert.False(AccessTokenRules.Allows(true, true, Tenant, 3, Guid.NewGuid(), 3));
}
