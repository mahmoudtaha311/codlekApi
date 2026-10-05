namespace Codlek.Api.Dashboard;

/// <summary>
/// اللوحة على موقع لوحدها — <b>عنوانها، ومين مسموح له يكلّم السيرفر</b>.
///
/// <para>🔴 <b>الاتنين فاضيين افتراضياً = اللوحة على نفس الموقع</b>، زي
/// القديم بالظبط: رابط الـQR بيحوّل على <c>/app</c>، والمتصفح مابيسمحش
/// لأي موقع تاني يكلّم السيرفر. الإعداد بيتحط بس لما اللوحة تتفصل.</para>
/// </summary>
public static class DashboardAccess
{
    public const string AddressKey = "Server:DashboardBaseUrl";

    public const string OriginsKey = "Cors:AllowedOrigins";

    /// <summary>عنوان اللوحة لو على نفس الموقع.</summary>
    public const string SameSiteBase = "/app";

    public const string PolicyName = "dashboard";

    /// <summary>
    /// أصل اللوحة اللي رابط الـQR بيحوّل عليه — <b>من غير شرطة في
    /// الآخر</b>.
    ///
    /// <para>🔴 <b>وبيتحقق عند الإقلاع.</b> رابط غلط هنا معناه إن كل QR
    /// ملزوق على لاب بيفتح صفحة مكسورة — والغلطة دي مابتبانش غير لما فني
    /// يمسح ليبل.</para>
    /// </summary>
    public static string ResolveAddress(IConfiguration configuration)
    {
        string configured = (configuration[AddressKey] ?? "").Trim();

        if (configured.Length == 0) return SameSiteBase;

        if (!Uri.TryCreate(configured, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || uri.Query.Length > 0 || uri.Fragment.Length > 0)
        {
            throw new InvalidOperationException(
                $"{AddressKey} لازم يبقى عنوان كامل http أو https من غير استعلام — " +
                $"مثلاً https://dashboard.example.net/app. اللي اتحط: «{configured}».");
        }

        return configured.TrimEnd('/');
    }

    /// <summary>
    /// المواقع المسموح لها تكلّم السيرفر من المتصفح — <b>أصول بس</b>.
    ///
    /// <para>⚠️ <b>والأصل بيتقارن بالحرف.</b> <c>https://x.net/</c>
    /// بالشرطة مابيطابقش أبداً، فالشرطة بتتشال هنا. ومسار جوّه الأصل
    /// (<c>https://x.net/app</c>) غلط في الإعداد — بيوقف الإقلاع بدل ما
    /// يبان كأن السيرفر رافض اللوحة من غير سبب.</para>
    /// </summary>
    public static string[] ResolveOrigins(IConfiguration configuration)
    {
        var raw = configuration.GetSection(OriginsKey).Get<string[]>() ?? [];

        var origins = new List<string>();

        foreach (string entry in raw)
        {
            string value = (entry ?? "").Trim().TrimEnd('/');

            if (value.Length == 0) continue;

            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                || uri.AbsolutePath != "/" || uri.Query.Length > 0)
            {
                throw new InvalidOperationException(
                    $"{OriginsKey}: «{entry}» مش أصل صالح — المتوقّع حاجة زي " +
                    "https://dashboard.example.net من غير مسار.");
            }

            origins.Add(value);
        }

        return [.. origins.Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    /// السياسة — <b>بالتوكن مش بالكوكي</b>، فمفيش <c>AllowCredentials</c>.
    ///
    /// <para>⚠️ <b>والترويستين دول لازم يتكشفوا صراحةً:</b> اللوحة بتقرا
    /// <c>Retry-After</c> عشان تقول «استنى» واسم الملف من
    /// <c>Content-Disposition</c> في التصدير — والمتصفح بيخبّيهم عن موقع
    /// تاني إلا لو السيرفر قال.</para>
    /// </summary>
    public static IServiceCollection AddDashboardAccess(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(new DashboardAddress(ResolveAddress(configuration)));

        string[] origins = ResolveOrigins(configuration);

        services.AddSingleton(new DashboardOrigins(origins));

        if (origins.Length > 0)
        {
            services.AddCors(cors => cors.AddPolicy(PolicyName, policy => policy
                .WithOrigins(origins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .WithExposedHeaders("Retry-After", "Content-Disposition")));
        }

        return services;
    }

    /// <summary>
    /// ⚠️ <b>قبل التحقق وقبل حدود الطلبات</b> — طلب الاستئذان
    /// (<c>OPTIONS</c>) مالوش توكن، ولو وصل للتحقق بيترفض والمتصفح
    /// بيقفل الطلب الحقيقي.
    /// </summary>
    public static IApplicationBuilder UseDashboardAccess(this IApplicationBuilder app)
    {
        var origins = app.ApplicationServices.GetRequiredService<DashboardOrigins>();

        return origins.Values.Length > 0 ? app.UseCors(PolicyName) : app;
    }
}

/// <summary>أصل اللوحة لرابط الـQR — <c>/app</c> أو عنوان كامل.</summary>
public sealed record DashboardAddress(string Base);

/// <summary>المواقع المسموح لها من المتصفح — فاضي = نفس الموقع بس.</summary>
public sealed record DashboardOrigins(string[] Values);
