extern alias newapi;
using Microsoft.AspNetCore.TestHost;
// PARITY COPY of CodlekWeb.Tests/RateLimitTests.cs - one change only: ThrottledServer boots the new API on the parity database.
// Every assertion is the legacy one, byte for byte.
using System.Net;
using System.Net.Http.Json;
using CodlekWeb.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CodlekWeb.Tests;

/// <summary>
/// سيرفر بحدود معدّل صغيرة — على <b>نفس</b> قاعدة الاختبار.
///
/// <para><b>ليه سيرفر تاني.</b> السيرفر المشترك بيقفل الحدود عشان
/// مئات الاختبارات في عملية واحدة ما تتخطّاهاش. لكن الحدود نفسها
/// لازم تتقاس على التطبيق الحقيقي، مش على وحدة منفصلة — اللي بيهم
/// إن السياسة <b>متركّبة على المسار الصح</b>، وده حاجة مالهاش معنى
/// غير من فوق HTTP.</para>
///
/// <para>⚠️ القاعدة مش بتتعمل هنا. الاختبارات دي في نفس المجموعة
/// بتاعة <see cref="TestServer"/>، والمجموعة بتجهّز القاعدة قبل أي
/// اختبار فيها.</para>
/// </summary>
public sealed class ThrottledServer : WebApplicationFactory<newapi::Program>
{
    /// <summary>الحد الصغير اللي الاختبارات بتعدّي عليه.</summary>
    public const int Permit = 3;

    // PARITY: limits are read while services register, so they go in as
    // environment variables for the duration of the host build.
    protected override Microsoft.Extensions.Hosting.IHost CreateHost(
        Microsoft.Extensions.Hosting.IHostBuilder builder)
    {
        var variables = new Dictionary<string, string>
        {
            ["Server__PublicBaseUrl"] = TestServer.PublicBaseUrl,
            ["RateLimits__Enabled"] = "true",
        };

        foreach (string policy in new[] { "WebLogin", "RackRegister", "TechnicianLogin", "RackApi" })
        {
            variables[$"RateLimits__{policy}__Permit"] = Permit.ToString();
            variables[$"RateLimits__{policy}__WindowSeconds"] = "300";
        }

        return TestServer.WithEnvironment(variables, () => base.CreateHost(builder));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:SqlServer", TestServer.ConnectionString);
        builder.UseSetting(PublicBaseUrl.Key, TestServer.PublicBaseUrl);
        builder.UseEnvironment("Development");

        builder.UseSetting("RateLimits:Enabled", "true");

        // PARITY: the new API reads its connection string at registration
        // time (user secrets win over UseSetting), so the database is
        // swapped in the container instead.
        builder.ConfigureTestServices(TestServer.UseParityDatabase);

        // نافذة طويلة عن قصد: الاختبار بيقيس «الرابع اترفض»، مش
        // بيستنى النافذة تعدّي. نافذة قصيرة كانت هتخلّي النتيجة
        // تعتمد على سرعة الجهاز.
        foreach (string policy in new[]
                 { "WebLogin", "RackRegister", "TechnicianLogin", "RackApi" })
        {
            builder.UseSetting($"RateLimits:{policy}:Permit", Permit.ToString());
            builder.UseSetting($"RateLimits:{policy}:WindowSeconds", "300");
        }
    }
}

/// <summary>
/// حدود المعدّل على المسارات الحسّاسة.
///
/// <para><b>الخطر.</b> التحقق من مفتاح المحطة ومن كود الاقتران بيعدّي
/// على PBKDF2 بمية ألف دورة. من غير حد، مهاجم من غير أي مفتاح بيقدر
/// يخلّي السيرفر يحرق وقته في الحساب لحد ما يقع — والخدمة على
/// استضافة صغيرة بتخدم كل المحطات.</para>
///
/// <para><b>والخطر التاني، جنب الأول:</b> حد شديد على مسارات
/// المزامنة بيكسر رفع الطابور. عشان كده فيه اختبار هنا بيثبت إن
/// المزامنة العادية بتعدّي، واختبار بيثبت إن الرد المرفوض عمره ما
/// يتقرا «اترفع».</para>
/// </summary>
[Collection(TestServerCollection.Name)]
public class RateLimitTests : IClassFixture<ThrottledServer>
{
    private readonly TestServer _server;
    private readonly ThrottledServer _throttled;

    public RateLimitTests(TestServer server, ThrottledServer throttled)
    {
        _server = server;
        _throttled = throttled;
    }

    // =================================================================
    //  ١ — تخمين الدخول بيتوقف
    // =================================================================

    /// <summary>
    /// باسوردات غلط ورا بعض على نفس الاسم = ٤٢٩ بعد الحد.
    ///
    /// <para>وقبل الحد لازم تبقى ٢٠٠ (الصفحة بترجع برسالة غلط) —
    /// وإلا الاختبار ممكن يعدّي على سيرفر بيرفض كل حاجة.</para>
    /// </summary>
    [Fact]
    public async Task Repeated_bad_passwords_are_throttled()
    {
        await Fixture.EnsureAsync(_server);

        using var client = _throttled.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var seen = new List<HttpStatusCode>();

        for (int attempt = 0; attempt < ThrottledServer.Permit + 2; attempt++)
        {
            string token = await TestServer.AntiforgeryTokenAsync(client, "/Login");

            using var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["username"] = Fixture.OwnerA,
                ["password"] = "definitely-wrong"
            });

            using var response = await client.PostAsync("/Login", form);
            seen.Add(response.StatusCode);
        }

        Assert.Contains(HttpStatusCode.OK, seen);
        Assert.Contains(HttpStatusCode.TooManyRequests, seen);

        // والرفض بييجي بعد المسموح، مش من أول محاولة
        Assert.Equal(HttpStatusCode.OK, seen[0]);
    }

    // =================================================================
    //  ٢ — تسجيل المحطة: المنجم الغالي
    // =================================================================

    /// <summary>
    /// أكواد اقتران غلط ورا بعض بتترفض قبل ما PBKDF2 يشتغل.
    ///
    /// <para>الكود الغلط عادةً بيرجّع ٤٠٠/٤٠٤ — واللي بيهمنا إن بعد
    /// الحد بيرجّع ٤٢٩، يعني الطلب وقف قبل الحساب الغالي.</para>
    /// </summary>
    [Fact]
    public async Task Registration_guessing_is_throttled()
    {
        await Fixture.EnsureAsync(_server);

        using var client = _throttled.CreateClient();

        var seen = new List<HttpStatusCode>();

        for (int attempt = 0; attempt < ThrottledServer.Permit + 2; attempt++)
        {
            using var response = await client.PostAsJsonAsync("/api/v2/racks/register", new
            {
                pairingCode = $"WRONG{attempt:D3}",
                rackName = "محاولة تخمين",
                installationId = Guid.NewGuid().ToString("N"),
                machineIdentifier = Guid.NewGuid().ToString("N"),
                appVersion = RackFixture.SupportedClientVersion
            });

            seen.Add(response.StatusCode);
        }

        Assert.Contains(HttpStatusCode.TooManyRequests, seen);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, seen[0]);
    }

    // =================================================================
    //  ٣ — الرد المرفوض شكله صح
    // =================================================================

    /// <summary>
    /// ٤٢٩ بيرجع JSON ومعاه <c>Retry-After</c> — عمره ما HTML.
    ///
    /// <para>🔴 <b>ده مش تجميل.</b> المحطة بتقرا جسم الرد. صفحة HTML
    /// كانت ممكن تتقرا غلط، والأهم إن الحالة لازم تفضل ٤٢٩ عشان
    /// <c>SyncOutcomeClassifier</c> يحوّلها <c>RetryScheduled</c>
    /// ويحتفظ بالشغل في الطابور.</para>
    /// </summary>
    [Fact]
    public async Task The_rejection_is_json_with_retry_after()
    {
        await Fixture.EnsureAsync(_server);

        using var client = _throttled.CreateClient();

        HttpResponseMessage? rejected = null;

        for (int attempt = 0; attempt < ThrottledServer.Permit + 3; attempt++)
        {
            var response = await client.PostAsJsonAsync("/api/v2/racks/register", new
            {
                pairingCode = $"NOPE{attempt:D3}",
                rackName = "شكل الرد",
                installationId = Guid.NewGuid().ToString("N"),
                machineIdentifier = Guid.NewGuid().ToString("N"),
                appVersion = RackFixture.SupportedClientVersion
            });

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                rejected = response;
                break;
            }

            response.Dispose();
        }

        Assert.NotNull(rejected);

        using (rejected)
        {
            Assert.Equal("application/json", rejected.Content.Headers.ContentType?.MediaType);
            Assert.True(rejected.Headers.Contains("Retry-After"));

            string body = await rejected.Content.ReadAsStringAsync();

            Assert.DoesNotContain("<html", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("<!DOCTYPE", body, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("TooManyRequests", body);
        }
    }

    // ⚠️ «المحطة بتصنّف ٤٢٩ كإعادة محاولة» متقاس في أداة التحقق
    // (tools/SpicsHarness) مش هنا: التصنيف عايش في مشروع الراكة
    // (net9.0-windows) واختبارات الموقع (net9.0) مش بتقدر ترجعه.

    // =================================================================
    //  ٤ — المزامنة العادية مابتتخنقش
    // =================================================================

    /// <summary>
    /// محطة سليمة بترفع دفعاتها ورا بعض من غير ما تتخنق.
    ///
    /// <para>⚠️ الحد هنا مضبوط على ٣ عن قصد — أقسى بكتير من الإنتاج
    /// (٦٠٠/دقيقة). ومع ذلك الرفع لازم يعدّي، لأن التقسيم بيحصل
    /// <b>بمفتاح المحطة</b>: يعني محطة بتاخد نصيبها لوحدها ومابتتأثرش
    /// بغيرها. لو التقسيم كان بالـ IP بس، ورشة فيها محطتين ورا نفس
    /// الخروج كانت هتتخنق.</para>
    /// </summary>
    [Fact]
    public async Task Healthy_rack_sync_is_not_throttled_out()
    {
        var rack = await RackFixture.EnsureAsync(_server, "RACK-LIMIT-OK");

        using var client = RackFixture.Client(_throttled, rack.ApiKey);

        for (int i = 0; i < ThrottledServer.Permit; i++)
        {
            var batch = RackFixture.Batch(
                Guid.NewGuid(), rack.RackCode,
                RackFixture.DeviceItem(Guid.NewGuid(), $"LP-8200{i:D4}"));

            using var response = await RackFixture.PostBatchAsync(client, batch);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    /// <summary>
    /// ومحطتين مختلفتين مابيتزاحموش على نفس النصيب.
    ///
    /// <para>الثانية بتبدأ من أول نصيبها هي، حتى والأولى خلّصت
    /// نصيبها بالكامل.</para>
    /// </summary>
    [Fact]
    public async Task Two_racks_get_separate_budgets()
    {
        var first = await RackFixture.EnsureAsync(_server, "RACK-LIMIT-A");
        var second = await RackFixture.EnsureAsync(_server, "RACK-LIMIT-B");

        using var clientA = RackFixture.Client(_throttled, first.ApiKey);
        using var clientB = RackFixture.Client(_throttled, second.ApiKey);

        // نستهلك نصيب الأولى بالكامل وزيادة
        for (int i = 0; i < ThrottledServer.Permit + 2; i++)
        {
            using var _ = await RackFixture.PostBatchAsync(clientA, RackFixture.Batch(
                Guid.NewGuid(), first.RackCode,
                RackFixture.DeviceItem(Guid.NewGuid(), $"LP-8300{i:D4}")));
        }

        // والتانية لسه بتعدّي
        using var response = await RackFixture.PostBatchAsync(clientB, RackFixture.Batch(
            Guid.NewGuid(), second.RackCode,
            RackFixture.DeviceItem(Guid.NewGuid(), "LP-8390001")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
