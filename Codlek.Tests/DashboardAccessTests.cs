using System.Net;
using System.Security.Claims;
using Codlek.Api.Controllers;
using Codlek.Api.Dashboard;
using Codlek.Api.Middlewares;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;

namespace Codlek.Tests;

/// <summary>
/// اللوحة على موقع لوحدها — <b>عنوانها، ومين يكلّم السيرفر</b>.
/// </summary>
public class DashboardAccessTests
{
    private static IConfiguration Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(v => v.Key, v => (string?)v.Value))
            .Build();

    // =================================================================
    //  عنوان اللوحة لرابط الـQR
    // =================================================================

    /// <summary>⚠️ من غير إعداد = نفس الموقع، زي القديم بالظبط.</summary>
    [Fact]
    public void Without_a_setting_the_dashboard_is_on_the_same_site()
    {
        Assert.Equal("/app", DashboardAccess.ResolveAddress(Config()));
    }

    [Fact]
    public void A_configured_address_loses_its_trailing_slash()
    {
        Assert.Equal(
            "https://dash.example.net/app",
            DashboardAccess.ResolveAddress(Config((DashboardAccess.AddressKey, " https://dash.example.net/app/ "))));
    }

    /// <summary>
    /// 🔴 <b>عنوان بايظ بيوقف الإقلاع</b> — بدل ما كل QR يفتح صفحة
    /// مكسورة ومحدش ياخد باله غير لما فني يمسح ليبل.
    /// </summary>
    [Theory]
    [InlineData("dash.example.net/app")]
    [InlineData("ftp://dash.example.net")]
    [InlineData("https://dash.example.net/app?x=1")]
    public void A_malformed_address_stops_the_start(string value)
    {
        Assert.Throws<InvalidOperationException>(() =>
            DashboardAccess.ResolveAddress(Config((DashboardAccess.AddressKey, value))));
    }

    [Fact]
    public void The_scan_link_lands_on_the_configured_dashboard()
    {
        var controller = new HealthController(Config(), new DashboardAddress("https://dash.example.net/app"));

        var result = Assert.IsType<RedirectResult>(controller.Scan("LP-0000 0001&x"));

        Assert.Equal("https://dash.example.net/app/devices?scan=LP-0000%200001%26x", result.Url);
    }

    [Fact]
    public void The_scan_link_stays_on_this_site_by_default()
    {
        var controller = new HealthController(Config(), new DashboardAddress(DashboardAccess.SameSiteBase));

        var result = Assert.IsType<RedirectResult>(controller.Scan("LP-00000001"));

        Assert.Equal("/app/devices?scan=LP-00000001", result.Url);
    }

    // =================================================================
    //  مين يكلّم السيرفر من المتصفح
    // =================================================================

    [Fact]
    public void Origins_are_trimmed_and_deduplicated()
    {
        var origins = DashboardAccess.ResolveOrigins(Config(
            ($"{DashboardAccess.OriginsKey}:0", "https://dash.example.net/"),
            ($"{DashboardAccess.OriginsKey}:1", "https://DASH.example.net"),
            ($"{DashboardAccess.OriginsKey}:2", "  ")));

        Assert.Equal(["https://dash.example.net"], origins);
    }

    /// <summary>
    /// ⚠️ <b>مسار جوّه الأصل غلط إعداد</b> — المتصفح بيبعت الأصل من غير
    /// مسار، فالمقارنة كانت هتفشل دايماً من غير سبب ظاهر.
    /// </summary>
    [Fact]
    public void An_origin_with_a_path_stops_the_start()
    {
        Assert.Throws<InvalidOperationException>(() => DashboardAccess.ResolveOrigins(
            Config(($"{DashboardAccess.OriginsKey}:0", "https://dash.example.net/app"))));
    }

    private static async Task<WebApplication> ServerAsync(params (string Key, string Value)[] settings)
    {
        var builder = WebApplication.CreateBuilder();

        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(
            settings.ToDictionary(s => s.Key, s => (string?)s.Value));

        builder.Services.AddDashboardAccess(builder.Configuration);

        var app = builder.Build();

        app.UseDashboardAccess();

        app.MapGet("/api/v1/ping", (HttpContext context) =>
        {
            context.Response.Headers.RetryAfter = "5";
            context.Response.Headers.ContentDisposition = "attachment; filename=x.xlsx";
            return "pong";
        });

        await app.StartAsync();
        return app;
    }

    private const string Dash = "https://dash.example.net";

    /// <summary>
    /// 🔴 <b>طلب الاستئذان من موقع اللوحة بيعدّي — ومعاه ترويسة التوكن.</b>
    /// من غير <c>Authorization</c> في المسموح، المتصفح بيقفل كل طلب
    /// داخل بتوكن قبل ما يتبعت.
    /// </summary>
    [Fact]
    public async Task The_dashboard_site_may_call_with_a_token()
    {
        await using var app = await ServerAsync(($"{DashboardAccess.OriginsKey}:0", Dash));
        var client = app.GetTestClient();

        var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/v1/ping");
        preflight.Headers.Add("Origin", Dash);
        preflight.Headers.Add("Access-Control-Request-Method", "GET");
        preflight.Headers.Add("Access-Control-Request-Headers", "authorization");

        using var answer = await client.SendAsync(preflight);

        Assert.Equal(Dash, Assert.Single(answer.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Contains("authorization",
            string.Join(",", answer.Headers.GetValues("Access-Control-Allow-Headers")),
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// ⚠️ <b>والترويستين اللي اللوحة بتقراهم مكشوفين</b> — «استنى» واسم
    /// ملف التصدير.
    /// </summary>
    [Fact]
    public async Task The_dashboard_can_read_retry_after_and_the_file_name()
    {
        await using var app = await ServerAsync(($"{DashboardAccess.OriginsKey}:0", Dash));
        var client = app.GetTestClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/ping");
        request.Headers.Add("Origin", Dash);

        using var response = await client.SendAsync(request);

        string exposed = string.Join(",", response.Headers.GetValues("Access-Control-Expose-Headers"));

        Assert.Contains("Retry-After", exposed, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Content-Disposition", exposed, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Another_site_gets_no_permission()
    {
        await using var app = await ServerAsync(($"{DashboardAccess.OriginsKey}:0", Dash));
        var client = app.GetTestClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/ping");
        request.Headers.Add("Origin", "https://evil.example.net");

        using var response = await client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    /// <summary>⚠️ ومن غير إعداد خالص مفيش أي موقع تاني — زي النهارده.</summary>
    [Fact]
    public async Task Without_a_setting_no_site_is_allowed()
    {
        await using var app = await ServerAsync();
        var client = app.GetTestClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/ping");
        request.Headers.Add("Origin", Dash);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }
}

/// <summary>
/// باسورد مؤقت = <b>مفيش وصول غير لتغييره</b> — القاعدة نفسها.
/// </summary>
public class ForcedPasswordChangeGateTests
{
    private static HttpContext Context(string path, bool authenticated = true, bool mustChange = true)
    {
        var claims = new List<Claim> { new(ClaimTypes.Name, "manager.a") };

        if (mustChange) claims.Add(new Claim("must", "1"));

        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(authenticated
                ? new ClaimsIdentity(claims, "Bearer")
                : new ClaimsIdentity()),
        };

        context.Request.Path = path;
        return context;
    }

    [Theory]
    [InlineData("/api/v1/devices")]
    [InlineData("/api/v1/users")]
    [InlineData("/api/v1/account/profile")]
    [InlineData("/api/v1/account/change-password/x")]
    public void Everything_else_is_closed(string path)
    {
        Assert.True(ForcedPasswordChangeGate.Blocks(Context(path)));
    }

    /// <summary>
    /// ⚠️ <b>الأبواب المفتوحة: «أنا مين»، والحساب، وتغيير الباسورد،
    /// والخروج والتجديد، والحياة</b> — وبأي حالة حروف أو شرطة في الآخر.
    /// </summary>
    [Theory]
    [InlineData("/api/v1/auth/me")]
    [InlineData("/API/V1/Auth/Me")]
    [InlineData("/api/v1/account")]
    [InlineData("/api/v1/account/")]
    [InlineData("/api/v1/account/change-password")]
    [InlineData("/api/v1/auth/logout")]
    [InlineData("/api/v1/auth/refresh")]
    [InlineData("/api/health")]
    public void Only_the_way_out_stays_open(string path)
    {
        Assert.False(ForcedPasswordChangeGate.Blocks(Context(path)));
    }

    [Fact]
    public void A_normal_account_is_never_held()
    {
        Assert.False(ForcedPasswordChangeGate.Blocks(Context("/api/v1/devices", mustChange: false)));
    }

    /// <summary>⚠️ والمجهول مش شغلة الحاجز ده — التحقق نفسه بيرفضه.</summary>
    [Fact]
    public void An_anonymous_request_is_not_this_gates_business()
    {
        Assert.False(ForcedPasswordChangeGate.Blocks(Context("/api/v1/devices", authenticated: false)));
    }
}
