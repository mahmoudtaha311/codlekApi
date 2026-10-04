using System.Net;
using Codlek.Core.Racks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Codlek.Tests;

/// <summary>
/// سطح الراكة <b>من خلال الأنبوب كله</b>.
///
/// <para>🔴 <b>والملف ده اتكتب بعد باج اتلقط بضرب حقيقي على
/// HTTP.</b> <c>RackKeyFilter</c> كان بيرجّع
/// <c>new UnauthorizedResult()</c>، وفحوص الوحدة كانت بتشوف النتيجة
/// دي <b>قبل</b> الأنبوب فعدّت كلها خضرا — بس
/// <c>[ApiController]</c> بيحوّل أي <c>IStatusCodeActionResult</c>
/// من غير جسم لـ<c>ProblemDetails</c>، فالراكة كانت بتستلم ١٦٥
/// بايت JSON فيها <c>type</c> و<c>title</c> و<c>traceId</c> على
/// <c>401</c>.</para>
///
/// <para>🔴 <b>وليه ده خطير:</b> الجسم على <c>401</c> جزء من عقد
/// مجمّد في القديم. وأهم من كده، أي رد على مسار راكة شكله «نجاح»
/// (<c>200</c>، أو HTML، أو تحويل بيوصل لصفحة) بيخلّي الراكة
/// <b>تمسح الصف من طابورها</b> — ضياع شغل فني في صمت.</para>
///
/// <para>⚠️ <b>والفحوص دي بتشغّل السيرفر فعلاً</b>، فهي أبطأ من
/// الباقي — بس هي الحتة الوحيدة اللي بتشوف اللي الراكة بتشوفه.</para>
/// </summary>
public class RackWireContractTests : IClassFixture<RackWireContractTests.Server>
{
    /// <summary>
    /// ⚠️ <b>السيرفر بينضرب على قاعدة التطوير</b> — القرايات دي
    /// كلها بترجع <c>401</c> قبل أي استعلام، فمفيش كتابة.
    /// </summary>
    public sealed class Server : WebApplicationFactory<Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            builder.ConfigureHostConfiguration(config =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    // ⚠️ قيم تطوير — التحقق من الإقلاع بيتفحص لوحده.
                    ["Server:PublicBaseUrl"] = "http://localhost:5097",
                }));

            return base.CreateHost(builder);
        }
    }

    private readonly HttpClient _client;

    public RackWireContractTests(Server server) =>
        _client = server.CreateClient(new WebApplicationFactoryClientOptions
        {
            /*
              🔴 **التحويل مقفول هنا عن قصد.**

              الراكة الحقيقية بتمشي ورا التحويل (`AllowAutoRedirect`
              افتراضية `true` في `HttpClient`)، وده بالظبط اللي
              بيخلّي تحويل لصفحة دخول يبان للراكة **نجاح**. هنا
              بنقفله عشان الفحص يشوف الرد الحقيقي بدل ما يتبعه.
            */
            AllowAutoRedirect = false,
        });

    /// <summary>كل مسار راكة بمفتاح — زي ما هو مكتوب في برنامج الراكة.</summary>
    public static TheoryData<string, string> KeyedRoutes => new()
    {
        { "POST", "/api/v2/technicians/login" },
        { "POST", "/api/v2/technicians/change-password" },
        { "GET", "/api/v2/sync/capabilities" },
        { "POST", "/api/v2/devices/lease" },
        { "GET", "/api/v2/technicians/repair" },
        { "GET", "/api/v2/containers" },
        { "GET", "/api/v2/repairs/assigned" },
        { "POST", "/api/sync/reports" },
    };

    // =================================================================
    //  جسم الـ401
    // =================================================================

    /// <summary>
    /// 🔴 <b>جسم الـ<c>401</c> فاضي بالظبط — صفر بايت.</b>
    /// </summary>
    [Theory]
    [MemberData(nameof(KeyedRoutes))]
    public async Task A_missing_key_gives_a_bare_401(string method, string path)
    {
        using var response = await Send(method, path, key: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal("", body);

        // ⚠️ ولا `Content-Type` — الراكة مابتحاولش تفكّ حاجة.
        Assert.Null(response.Content.Headers.ContentType);
    }

    /// <summary>
    /// ⚠️ <b>ونفس الرد بالظبط للمفتاح القصير والغلط.</b> اللي
    /// بيحاول مالوش يعرف إيه اللي ناقص.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX")]
    public async Task A_wrong_key_is_indistinguishable_from_a_missing_one(string key)
    {
        using var response = await Send("POST", "/api/v2/technicians/login", key);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("", await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// 🔴 <b>ومفيش <c>ProblemDetails</c> — ولا حتى حرف منها.</b>
    /// والفحص بيدوّر على الكلمات نفسها لأن ده اللي اتسرّب فعلاً.
    /// </summary>
    [Fact]
    public async Task The_401_body_has_no_problem_details_in_it()
    {
        using var response = await Send("POST", "/api/v2/technicians/login", key: null);

        string body = await response.Content.ReadAsStringAsync();

        foreach (string word in new[] { "type", "title", "status", "traceId", "rfc" })
            Assert.DoesNotContain(word, body, StringComparison.OrdinalIgnoreCase);
    }

    // =================================================================
    //  مفيش HTML ومفيش تحويل
    // =================================================================

    /// <summary>
    /// 🔴 <b>ولا مسار راكة واحد بيرجّع HTML ولا تحويل — وده أخطر
    /// فحص في الملف.</b>
    ///
    /// <para>الراكة بتمشي ورا التحويل، ولو التحويل وقع على صفحة
    /// HTML بـ<c>200</c>، <c>SyncOutcomeClassifier</c> بيعتبرها
    /// نجاح، و<c>ReadItemResult</c> مابيلاقيش <c>results</c> في
    /// الـHTML فبيرجّع «اترفع» — و<c>Outbox.Complete</c>
    /// <b>بيمسح الصف</b>. النتيجة: كل فحص وكل جهاز في طابور كل راكة
    /// منشورة بيتمسح، والفني شايف «اترفع».</para>
    ///
    /// <para>⚠️ وده أسوأ من <c>404</c>، لأن <c>404</c> على الأقل
    /// بيوقف الطابور بدل ما يفضّيه.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(KeyedRoutes))]
    public async Task No_rack_route_ever_answers_with_html_or_a_redirect(
        string method, string path)
    {
        using var response = await Send(method, path, key: null);

        Assert.False(
            response.StatusCode is >= HttpStatusCode.MultipleChoices
                                and < HttpStatusCode.BadRequest,
            $"{method} {path} رجّع تحويل ({(int)response.StatusCode}) — "
            + "والراكة بتمشي وراه.");

        string body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("<!DOCTYPE", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<html", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 🔴 <b>ومفيش <c>MapFallbackToFile</c> في المشروع ده — ولازم
    /// يفضل مفيش.</b>
    ///
    /// <para>في القديم، ترتيب تسجيل المسارات حمّال:
    /// <c>MapFallbackToFile</c> بعد نقط الراكة. لو اتقلب، طلب
    /// الراكة بياخد <c>200</c> + HTML. هنا مسار مش موجود خالص لازم
    /// يرجّع <c>404</c> — مش صفحة.</para>
    /// </summary>
    [Theory]
    [InlineData("/api/v2/does-not-exist")]
    [InlineData("/api/v2/sync/batch")]
    [InlineData("/app/devices")]
    [InlineData("/")]
    public async Task An_unknown_path_is_a_404_not_a_page(string path)
    {
        using var response = await Send("GET", path, key: null);

        string body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("<!DOCTYPE", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<html", body, StringComparison.OrdinalIgnoreCase);

        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound
                or HttpStatusCode.MethodNotAllowed
                or HttpStatusCode.Unauthorized,
            $"GET {path} رجّع {(int)response.StatusCode} — المتوقّع ٤٠٤.");
    }

    // =================================================================
    //  النقطة الوحيدة من غير مفتاح
    // =================================================================

    /// <summary>
    /// ⚠️ <b>والتسجيل بيرجّع <c>{code, message}</c> مش
    /// <c>ProblemDetails</c>.</b> الراكة بتقرا <c>code</c> من الجسم
    /// وبتعرض رسالة محلية بيه.
    /// </summary>
    [Fact]
    public async Task A_bad_pairing_code_answers_with_code_and_message()
    {
        using var response = await _client.PostAsync(
            "/api/v2/racks/register",
            JsonBody("{\"pairingCode\":\"ZZZZ-ZZZZ\"}"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();

        Assert.Contains("\"code\"", body, StringComparison.Ordinal);
        Assert.Contains("\"message\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"title\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("traceId", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// ⚠️ <b>والكود الناقص برضه <c>{code, message}</c></b> — مش
    /// <c>ValidationProblemDetails</c>.
    /// </summary>
    [Fact]
    public async Task A_missing_pairing_code_answers_the_same_shape()
    {
        using var response = await _client.PostAsync(
            "/api/v2/racks/register", JsonBody("{}"));

        string body = await response.Content.ReadAsStringAsync();

        Assert.Contains("\"code\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("errors", body, StringComparison.Ordinal);
    }

    // =================================================================
    //  نقطة الحياة
    // =================================================================

    [Fact]
    public async Task Health_is_open_and_stamps_utc()
    {
        using var response = await _client.GetAsync("/api/health");

        response.EnsureSuccessStatusCode();

        string body = await response.Content.ReadAsStringAsync();

        Assert.Contains("\"ok\":true", body, StringComparison.Ordinal);

        // 🔴 و`Z` في الآخر — المتصفح بيقرا اللي من غيرها توقيت محلي.
        Assert.Contains("Z\"", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// ⚠️ <b>ورابط الجهاز بيحوّل — وده المسار الوحيد اللي
    /// <u>المفروض</u> يحوّل.</b> بني آدم بيفتحه من موبايل، مش راكة.
    /// </summary>
    [Fact]
    public async Task The_device_short_link_redirects_for_humans()
    {
        using var response = await _client.GetAsync("/d/LP-00000123");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        Assert.Contains(
            "LP-00000123", response.Headers.Location!.ToString(), StringComparison.Ordinal);
    }

    // =================================================================
    //  العنوان
    // =================================================================

    /// <summary>
    /// 🔴 <b>والمسار جزء من العقد مش إعداد.</b>
    /// </summary>
    [Fact]
    public void The_sync_path_is_frozen()
    {
        Assert.Equal("/api/sync/reports", RackSyncAddress.SyncPath);

        Assert.Equal(
            "https://codlek.runasp.net/api/sync/reports",
            RackSyncAddress.SyncEndpoint("https://codlek.runasp.net"));
    }

    private Task<HttpResponseMessage> Send(string method, string path, string? key)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);

        if (key is not null) request.Headers.Add(RackKey.Header, key);

        if (method == "POST") request.Content = JsonBody("{}");

        return _client.SendAsync(request);
    }

    private static StringContent JsonBody(string json) =>
        new(json, System.Text.Encoding.UTF8, "application/json");
}
