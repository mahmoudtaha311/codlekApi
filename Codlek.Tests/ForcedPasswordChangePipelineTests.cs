using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Codlek.Application.Contracts.Auth;
using Codlek.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Codlek.Tests;

/// <summary>
/// حاجز الباسورد المؤقت <b>من خلال الأنبوب كله</b> — بتوكن حقيقي من
/// نفس المُصدِر.
///
/// <para>⚠️ <b>ومن غير ما يلمس القاعدة:</b> الحاجز بيرد قبل أي معالج،
/// و«أنا مين» بتتقرا من التوكن، وتغيير الباسورد بجسم ناقص بيترفض من
/// التحقق قبل أي استعلام.</para>
/// </summary>
public class ForcedPasswordChangePipelineTests(RackWireContractTests.Server server)
    : IClassFixture<RackWireContractTests.Server>
{
    private HttpClient Client(bool mustChange)
    {
        using var scope = server.Services.CreateScope();

        var pair = scope.ServiceProvider.GetRequiredService<ITokenIssuer>().Issue(new TokenSubject(
            UserId: Guid.NewGuid(),
            TenantId: Guid.NewGuid(),
            Username: "manager.a",
            DisplayName: "مدير",
            Code: "100002",
            Role: "Manager",
            CredentialVersion: 1,
            MustChangePassword: mustChange));

        var client = server.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pair.AccessToken);
        return client;
    }

    /// <summary>
    /// 🔴 <b>حساب على باسورد مؤقت مايقدرش يقرا الأجهزة</b> — ٤٠٣ برسالة
    /// وكود اللوحة تتعرّف بيه.
    /// </summary>
    [Fact]
    public async Task A_temporary_password_cannot_read_the_devices()
    {
        using var client = Client(mustChange: true);
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
        using var client = Client(mustChange: true);
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
        using var client = Client(mustChange: true);
        using var response = await client.PostAsJsonAsync("/api/v1/account/change-password", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_normal_account_is_not_held()
    {
        using var client = Client(mustChange: false);
        using var response = await client.GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var me = await response.Content.ReadFromJsonAsync<MeResponse>(
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.False(me!.MustChangePassword);
    }
}
