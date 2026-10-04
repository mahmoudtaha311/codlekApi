namespace Codlek.Api;

/// <summary>
/// تسجيل طبقة الواجهة، وترتيب خط المعالجة.
///
/// <para>⚠️ الترتيب جوّه <see cref="UseApiPipeline"/> <b>حمّال</b> —
/// مش تنظيم. شوف التعليق جوّاه.</para>
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApiServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers();
        services.AddHttpContextAccessor();

        // ⚠️ Scalar بدل Swagger — زي FixFlow، وللتطوير بس.
        services.AddOpenApi();

        return services;
    }

    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
            app.MapOpenApi();

        /*
          🔴 **ترتيب التسجيل حمّال — ودي غلطة كلّفت في القديم.**

          أي مسار احتياطي للواجهة (`MapFallbackToFile`) لازم يتسجّل
          **بعد** كل نقط الراكة. لو سبقها، طلب الراكة بياخد
          `200 + HTML` بدل رده — والراكة بتقرا الـ٢٠٠ على إنه نجاح
          و**بتمسح الصف من طابورها**. ضياع شغل صامت في الورشة، من غير
          أي رسالة خطأ.

          ⚠️ لسه مفيش مسار احتياطي هنا. السطر ده مكتوب دلوقتي عشان
          لما يتزاد، يتزاد في مكانه الصح.
        */
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();

        return app;
    }
}
