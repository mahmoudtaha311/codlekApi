using System.Text.Json;
using System.Threading.RateLimiting;
using Codlek.Core.Racks;
using Microsoft.AspNetCore.RateLimiting;

namespace Codlek.Api.Racks;

/// <summary>إعدادات حدود الطلبات — من <c>RateLimits</c> في الإعدادات.</summary>
public sealed class RateLimitOptions
{
    public const string Section = "RateLimits";

    /// <summary>
    /// ⚠️ <b>القفل بيخلّي كل السياسات بلا حد</b> — بدل ما نشيل
    /// <c>RequireRateLimiting</c> من كل مسار ونرجّعه بعدين.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>تسجيل محطة — عشر محاولات في خمس دقايق.</summary>
    /// <summary>
    /// دخول اللوحة — لكل (IP + اسم المستخدم المطبَّع). <b>نفس اسم القديم
    /// ونفس أرقامه</b>، فإعداد <c>RateLimits:WebLogin</c> بيشتغل على الاتنين.
    /// </summary>
    public Window WebLogin { get; set; } = new() { Permit = 10, WindowSeconds = 300 };

    public Window RackRegister { get; set; } = new() { Permit = 10, WindowSeconds = 300 };

    /// <summary>دخول الفني — عشر محاولات في خمس دقايق.</summary>
    public Window TechnicianLogin { get; set; } = new() { Permit = 10, WindowSeconds = 300 };

    /// <summary>مسارات المزامنة — ٦٠٠ في الدقيقة.</summary>
    public Window RackApi { get; set; } = new() { Permit = 600, WindowSeconds = 60 };

    public sealed class Window
    {
        public int Permit { get; set; }

        public int WindowSeconds { get; set; }

        public TimeSpan Period => TimeSpan.FromSeconds(Math.Max(1, WindowSeconds));
    }
}

/// <summary>
/// حدود الطلبات على سطح الراكة.
///
/// <para>🔴 <b>والجسم بيتكتب بالإيد.</b> من غيره ASP.NET بيرجّع جسم
/// فاضي أو صفحة خطأ حسب الإعداد — والراكة بتقرا اللي يوصلها. وصفحة
/// HTML هنا بتتقري نجاح.</para>
///
/// <para>🔴 <b>والطابور صفر.</b> الطابور بيخلّي الطلب الزايد يستنى
/// بدل ما يترفض — وده أسوأ هنا: المحطة عندها مهلة، والطلب اللي
/// بيستنى بيوصل للمهلة ويتحسب <b>عطل شبكة</b> بدل «استنى وجرّب».
/// والرفض الفوري بـ<c>429</c> بيدّي للمحطة إشارة واضحة وبتأجّل
/// بنفسها.</para>
/// </summary>
public static class RackRateLimits
{
    public const string WebLogin = "web-login";
    public const string RackRegister = "rack-register";
    public const string TechnicianLogin = "technician-login";
    public const string RackApi = "rack-api";

    /// <summary>⚠️ نفس طول بادئة مفتاح المحطة — التقسيم بيها.</summary>
    private const int KeyPrefixLength = RackKey.PrefixLength;

    public static IServiceCollection AddRackRateLimiting(
        this IServiceCollection services, IConfiguration configuration)
    {
        var options = new RateLimitOptions();
        configuration.GetSection(RateLimitOptions.Section).Bind(options);

        services.AddSingleton(options);

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            limiter.OnRejected = async (context, token) =>
            {
                var window = context.Lease.TryGetMetadata(
                    MetadataName.RetryAfter, out var retry)
                    ? retry
                    : TimeSpan.FromSeconds(60);

                context.HttpContext.Response.Headers.RetryAfter =
                    ((int)window.TotalSeconds).ToString();

                /*
                  🔴 **لازم يتحط بالإيد.**

                  من غيره ASP.NET بيرجّع جسم فاضي أو صفحة خطأ حسب
                  الإعداد — والمحطة بتقرا اللي يوصلها. وأي حاجة شكلها
                  HTML بتتقري نجاح وبتمسح الصف من الطابور.
                */
                context.HttpContext.Response.ContentType = "application/json; charset=utf-8";

                await context.HttpContext.Response.WriteAsync(
                    JsonSerializer.Serialize(new
                    {
                        error = "TooManyRequests",
                        message = "محاولات كتير في وقت قصير. استنى شوية وجرّب تاني.",
                        retryAfterSeconds = (int)window.TotalSeconds,
                    }),
                    token);
            };

            if (!options.Enabled)
            {
                foreach (string name in new[] { WebLogin, RackRegister, TechnicianLogin, RackApi })
                    limiter.AddPolicy(name, _ => RateLimitPartition.GetNoLimiter("disabled"));

                return;
            }

            /*
              ===== تسجيل محطة =====

              ⚠️ مفيش مفتاح ولا هوية هنا — الـIP هو كل اللي عندنا.
            */
            /*
              ===== دخول اللوحة =====

              🔴 **كان ناقص خالص من المشروع الجديد.** القديم عنده الحد ده
              على صفحة الدخول من أول يوم، والجديد كان بيقبل محاولات من غير
              عدد على حسابات المالك — يعني تخمين باسورد مفتوح على النت.
              اتلقط وقت تجهيز اللوحة على موقع لوحدها.

              ⚠️ والاسم مقروء من جسم JSON قبل الحد (`LoginNameCapture`)
              لأن دالة التقسيم متزامنة ومش بتقدر تقرا الجسم.
            */
            limiter.AddPolicy(WebLogin, http =>
                !HttpMethods.IsPost(http.Request.Method)
                    ? RateLimitPartition.GetNoLimiter("login:get")
                    : Fixed(options.WebLogin,
                            "login:" + Ip(http) + "|" + LoginNameCapture.From(http)));

            limiter.AddPolicy(RackRegister, http =>
                Fixed(options.RackRegister, "register:" + Ip(http)));

            /*
              ===== دخول الفني =====

              الطلب متحقق بمفتاح المحطة، فالتقسيم بالمحطة أدق من
              الـIP: ورشة كاملة ممكن تبقى ورا نفس الخروج.

              🔴 **والاسم مش جزء من مفتاح التقسيم هنا، عن قصد.**
              الاسم عايش في جسم JSON، وقراءته في دالة التقسيم بتستهلك
              مجرى الطلب قبل ما الربط يشوفه.

              ⚠️ والبُعد ده متغطّي في جدول محاولات الدخول: العدّ على
              (محطة + اسم مطبَّع)، وهو أقوى من عدّاد في الذاكرة —
              بيعيش بعد إعادة التشغيل وبيشتغل صح على أكتر من نسخة.
              فالحدّين بيكمّلوا بعض: ده بيقف قدام الفيضان من محطة
              واحدة، والتاني قدام تخمين باسورد فني بعينه.
            */
            limiter.AddPolicy(TechnicianLogin, http =>
                Fixed(options.TechnicianLogin, "tech:" + (ApiKeyPrefix(http) ?? Ip(http))));

            // ===== مسارات المزامنة =====
            limiter.AddPolicy(RackApi, http =>
                Fixed(options.RackApi, "rack:" + (ApiKeyPrefix(http) ?? Ip(http))));
        });

        return services;
    }

    private static RateLimitPartition<string> Fixed(
        RateLimitOptions.Window window, string key) =>
        RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = Math.Max(1, window.Permit),
            Window = window.Period,

            // 🔴 صفر — راجع التعليق على الكلاس.
            QueueLimit = 0,

            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true,
        });

    private static string Ip(HttpContext http) =>
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    /// <summary>
    /// ⚠️ <b>البادئة مش المفتاح كامل.</b> المفتاح الكامل كمفتاح
    /// تقسيم معناه إنه بيعيش في ذاكرة السيرفر — والبادئة كفاية
    /// للتقسيم.
    /// </summary>
    private static string? ApiKeyPrefix(HttpContext http)
    {
        string? key = http.Request.Headers[RackKey.Header].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(key)) return null;

        return key.Length <= KeyPrefixLength ? key : key[..KeyPrefixLength];
    }
}
