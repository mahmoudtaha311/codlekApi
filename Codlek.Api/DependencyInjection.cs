using Codlek.Api.Dashboard;
using System.Text;
using Codlek.Api.Authorization;
using Codlek.Api.Extensions;
using Codlek.Api.Services;
using Codlek.Application.Abstractions;
using Codlek.Application.Interfaces;
using Codlek.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

using Codlek.Api.Racks;

namespace Codlek.Api;

/// <summary>تسجيل طبقة الواجهة — التحقق من التوكن وشكل الأخطاء.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApiServices(
        this IServiceCollection services, IConfiguration configuration,
        bool isDevelopment)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

        /*
          🔴 **حارس مفتاح المحطة — فلتر مش مخطّط تحقّق.**

          سطح الراكة مالوش هوية مستخدم: هو مفتاح جهاز. ولو اتعمل
          كمخطّط، `ICurrentUser` كانت بترمي أول ما تتنده على طلب
          راكة لأن مفيش مطالبة شركة.
        */
        services.AddScoped<Racks.RackKeyFilter>();

        // 🔴 حدود الطلبات على سطح الراكة — ومعاها جسم ٤٢٩ مكتوب
        //    بالإيد، لأن الراكة بتقرا اللي يوصلها.
        services.AddRackRateLimiting(configuration);

        // اللوحة على موقع لوحدها: عنوانها لرابط الـQR، ومين يكلّم السيرفر.
        services.AddDashboardAccess(configuration);

        /*
          🔴 **إعدادات السيرفر اللي الراكة بتقراها.**

          رابط المزامنة بيتبعت للراكة وقت التسجيل وبتفضل عليه
          شهور — فبناؤه من ترويسة `Host` معناه إن اللي بيسجّل
          بيحدّد فين الراكة هترفع شغلها.
        */
        services.Configure<Racks.RackServerOptions>(
            configuration.GetSection(Racks.RackServerOptions.Section));

        /*
          🔴 **والعنوان بيتحقق <u>عند الإقلاع</u> مش عند أول
          تسجيل.**

          عنوان فاضي معناه `syncUrl` فاضي على كل راكة بتتسجّل،
          والراكة بتخزّنه وبتفضل تحاول ترفع عليه — ومحدّش بياخد باله
          غير بعد ما شغل أسبوع يبقى واقف في الطابور. الوقوف هنا
          بيتصلّح في دقيقة؛ العنوان الفاضي بيتصلّح بإعادة تسجيل كل
          المحطات.
        */
        services.AddSingleton(
            Racks.CloudAddresses.Resolve(configuration, isDevelopment));

        services.AddJwtAuthentication(configuration);
        services.AddValidationProblemShape();

        /*
          🔴 **صيانة الإقلاع — في الخلفية بعد ما السيرفر يبتدي يرد.**

          القديم كان بيربط الفحوص اليتيمة ويعيد حساب «مشكوك إنه مكرر»
          ويملأ الاسم التجاري مع كل تشغيل. من غيرها الحاجات دي بتفضل
          على حالها للأبد. التفاصيل في `StartupMaintenanceService`.
        */
        services.Configure<Startup.StartupMaintenanceOptions>(
            configuration.GetSection(Startup.StartupMaintenanceOptions.Section));
        services.AddHostedService<Startup.StartupMaintenanceService>();

        return services;
    }

    /// <summary>
    /// التحقق من توكن الوصول.
    ///
    /// <para>🔴 <b>الإعدادات دي لازم تطابق <c>JwtTokenIssuer</c>
    /// بالحرف.</b> أي فرق — مُصدِر، جمهور، تسامح وقت — معناه إن توكن
    /// السيرفر بيعمله السيرفر نفسه مش بيقبله. والرسالة اللي بتطلع
    /// <c>401</c> من غير سبب.</para>
    /// </summary>
    private static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException(
                "قسم Jwt ناقص من الإعدادات — السيرفر مايقدرش يتحقق من أي توكن.");

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = options.Issuer,
                    ValidateAudience = true,
                    ValidAudience = options.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey =
                        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key)),
                    ValidateLifetime = true,

                    // ⚠️ صفر تسامح. الافتراضي ٥ دقايق، وده بيمدّ عمر
                    // كل توكن ٥ دقايق من غير ما حد يقصد — يعني الطرد
                    // بياخد ٢٠ دقيقة بدل ١٥.
                    ClockSkew = TimeSpan.Zero,
                };

                /*
                  🔴 **`MapInboundClaims = false` — نفس اللي في الإصدار.**

                  من غيرها، `sub` بتتحوّل لاسم XML قديم. فأي كود بيقرا
                  `sub` بيلاقي فاضي — ومفيش خطأ، بس الهوية بتضيع.
                */
                o.MapInboundClaims = false;

                /*
                  ⚠️ **توكن التجديد ممنوع يُقبل كتوكن وصول.**

                  الاتنين موقّعين بنفس المفتاح وبنفس المُصدِر، فـ
                  `AddJwtBearer` بتقبل الاتنين. ولو توكن التجديد نفع
                  للوصول، يبقى معاه وصول أسبوع كامل مالوش طرد —
                  والـ١٥ دقيقة اللي النظام كله مبني عليها بتبقى بلا
                  معنى.
                */
                o.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        string? use = context.Principal?
                            .FindFirst(JwtTokenIssuer.TokenUseClaim)?.Value;

                        if (use != JwtTokenIssuer.AccessUse)
                        {
                            context.Fail("ده مش توكن وصول.");
                            return;
                        }

                        /*
                          🔴 **الحساب لسه مسموحله؟ — في كل طلب، زي القديم.**

                          التوقيع بيثبت إن التوكن بتاعنا، مش إن صاحبه لسه
                          شغّال. من غير السطور دي، المالك يوقف حساب لابتوبه
                          اتسرق والتاب المفتوح يفضل يصدّر المخزن ربع ساعة.
                          القديم كان بيطرده من أول طلب (`CookieSessionGuard`).

                          ⚠️ **هنا ومش في وسيط بعد التحقق:** الوسيط ممكن
                          يتحط في الترتيب الغلط أو يتشال. هنا التوكن نفسه
                          بيبقى مرفوض — فكل `[Authorize]` بترد `401` بنفس
                          شكل التوكن المنتهي، واللوحة بتعرف تتعامل معاه.

                          ⚠️ **ومسارات الراكة مابتعدّيش هنا:** هي بمفتاح
                          في ترويسة لوحدها مش `Bearer`، فمفيش توكن يتقرا.
                        */
                        var principal = context.Principal!;

                        bool readable =
                            Guid.TryParse(principal.FindFirst(
                                System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value,
                                out Guid userId)
                            & Guid.TryParse(principal.FindFirst("tenant")?.Value, out Guid tenant)
                            & int.TryParse(principal.FindFirst(JwtTokenIssuer.VersionClaim)?.Value,
                                System.Globalization.NumberStyles.Integer,
                                System.Globalization.CultureInfo.InvariantCulture,
                                out int version);

                        if (!readable)
                        {
                            context.Fail("التوكن ناقص بيانات الحساب.");
                            return;
                        }

                        var services = context.HttpContext.RequestServices;

                        bool allowed = await services
                            .GetRequiredService<AccountStanding>()
                            .AllowsAsync(
                                services.GetRequiredService<Codlek.Infrastructure.Data.AppDbContext>(),
                                userId, tenant, version, context.HttpContext.RequestAborted);

                        if (!allowed)
                            context.Fail("الحساب اتوقف أو بياناته اتغيّرت.");
                    },
                };
            });

        services.AddAuthorization(o => o.AddCodlekPolicies());
        return services;
    }

    /// <summary>
    /// أخطاء التحقق التلقائي بتطلع بنفس شكل أخطائنا.
    ///
    /// <para>⚠️ من غير ده، فيه شكلين للخطأ: واحد من
    /// <c>ValidationBehavior</c> وواحد من ASP.NET لما الـJSON نفسه
    /// مايتقراش. والواجهة بتتعامل مع واحد وبتنسى التاني.</para>
    /// </summary>
    private static IServiceCollection AddValidationProblemShape(
        this IServiceCollection services) =>
        services.Configure<ApiBehaviorOptions>(o =>
            o.InvalidModelStateResponseFactory = context =>
            {
                var errors = context.ModelState
                    .Where(e => e.Value?.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

                return Result.Failure(new ValidationError(errors)).ToProblem();
            });
}
