using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Codlek.Tests;

/// <summary>
/// حدّ دخول اللوحة — <b>منقول من القديم: لكل (IP + اسم)</b>.
///
/// <para>🔴 <b>كان ناقص خالص من المشروع الجديد</b> — تخمين باسورد المالك
/// كان مفتوح من غير عدد. والفحص اللي في القديم كان على صفحة Razor،
/// فلما فحوص القديم اتشغّلت على الجديد اتصنّف «صفحة مش موجودة»
/// والفجوة استخبّت وراه.</para>
///
/// <para>⚠️ <b>ومن غير ما يكتب في القاعدة:</b> أسماء مش موجودة، والدخول
/// الفاشل مابيسجّلش حاجة.</para>
/// </summary>
public class WebLoginThrottleTests(RackWireContractTests.Server server)
    : IClassFixture<RackWireContractTests.Server>
{
    private static Task<HttpResponseMessage> TryAsync(HttpClient client, string username) =>
        client.PostAsJsonAsync("/api/v1/auth/login", new { username, password = "WrongPass1" });

    [Fact]
    public async Task The_eleventh_try_on_one_name_waits_and_says_why()
    {
        using var client = server.CreateClient();
        string name = "throttle." + Guid.NewGuid().ToString("N")[..8];

        for (int i = 0; i < 10; i++)
        {
            using var attempt = await TryAsync(client, name);
            Assert.Equal(HttpStatusCode.Unauthorized, attempt.StatusCode);
        }

        using var blocked = await TryAsync(client, name);

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.NotNull(blocked.Headers.RetryAfter);

        var body = JsonDocument.Parse(await blocked.Content.ReadAsStringAsync()).RootElement;
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("message").GetString()));

        // ⚠️ نفس الاسم بحروف كبيرة ومسافات = نفس الحساب = نفس الحد.
        using var disguised = await TryAsync(client, "  " + name.ToUpperInvariant() + " ");
        Assert.Equal(HttpStatusCode.TooManyRequests, disguised.StatusCode);

        // ⚠️ وحساب تاني مابيتقفلش بسبب غلط غيره.
        using var other = await TryAsync(client, name + "x");
        Assert.Equal(HttpStatusCode.Unauthorized, other.StatusCode);
    }

    /// <summary>
    /// 🔴 <b>قراية الاسم قبل الحد مابتاكلش الجسم.</b> لو المجرى ماترجعش
    /// لأوله، الدخول نفسه كان هيلاقي جسم فاضي ويرفض كل محاولة بـ٤٠٠ —
    /// حتى الصح.
    /// </summary>
    [Fact]
    public async Task Reading_the_name_leaves_the_body_for_the_login()
    {
        using var client = server.CreateClient();

        using var response = await TryAsync(client, "nobody." + Guid.NewGuid().ToString("N")[..8]);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
