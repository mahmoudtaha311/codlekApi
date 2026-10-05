using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Codlek.Application.Contracts.Auth;
using Codlek.Core.Enums;

namespace Codlek.Tests;

/// <summary>
/// حاجز الباسورد المؤقت <b>من خلال الأنبوب كله</b> — بتوكن حقيقي من
/// نفس المُصدِر.
///
/// <para>⚠️ <b>والحساب حقيقي في قاعدة فحص.</b> كانت الفحوص دي بتعمل
/// توكن لمعرّف عشوائي ومن غير قاعدة — وده بقى بيترفض <c>401</c> من
/// أول طلب، لأن كل توكن بيتفحص على صف صاحبه (شوف
/// <c>ImmediateLogoutTests</c>). الحاجز نفسه لسه بيرد قبل أي معالج.</para>
/// </summary>
[Collection(DashboardServer.Collection)]
public class ForcedPasswordChangePipelineTests(DashboardServer server)
{
    private async Task<HttpClient> ClientAsync(bool mustChange) =>
        server.ClientFor(await server.AddUserAsync(UserRole.Manager, mustChange));

    /// <summary>
    /// 🔴 <b>حساب على باسورد مؤقت مايقدرش يقرا الأجهزة</b> — ٤٠٣ برسالة
    /// وكود اللوحة تتعرّف بيه.
    /// </summary>
    [Fact]
    public async Task A_temporary_password_cannot_read_the_devices()
    {
        using var client = await ClientAsync(mustChange: true);
        using var response = await client.GetAsync("/api/v1/devices");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal("PasswordChangeRequired", body.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("message").GetString()));
    }

    /// <summary>⚠️ بس «أنا مين» بترد — اللوحة محتاجة تعرف إنه لازم يغيّر.</summary>
    [Fact]
    public async Task A_temporary_password_still_learns_it_must_change()
    {
        using var client = await ClientAsync(mustChange: true);
        using var response = await client.GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var me = await response.Content.ReadFromJsonAsync<MeResponse>(
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.True(me!.MustChangePassword);
    }

    /// <summary>
    /// ⚠️ <b>وباب التغيير مفتوح</b> — الجسم الناقص بيترفض من التحقق نفسه
    /// (٤٠٠) مش من الحاجز (٤٠٣).
    /// </summary>
    [Fact]
    public async Task The_change_password_door_stays_open()
    {
        using var client = await ClientAsync(mustChange: true);
        using var response = await client.PostAsJsonAsync("/api/v1/account/change-password", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_normal_account_is_not_held()
    {
        using var client = await ClientAsync(mustChange: false);
        using var response = await client.GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var me = await response.Content.ReadFromJsonAsync<MeResponse>(
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.False(me!.MustChangePassword);
    }
}
