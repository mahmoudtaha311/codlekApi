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

namespace Codlek.Api;

/// <summary>تسجيل طبقة الواجهة — التحقق من التوكن وشكل الأخطاء.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApiServices(
        this IServiceCollection services, IConfiguration configuration)
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

        services.AddJwtAuthentication(configuration);
        services.AddValidationProblemShape();
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
                    OnTokenValidated = context =>
                    {
                        string? use = context.Principal?
                            .FindFirst(JwtTokenIssuer.TokenUseClaim)?.Value;

                        if (use != JwtTokenIssuer.AccessUse)
                            context.Fail("ده مش توكن وصول.");

                        return Task.CompletedTask;
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
