using Codlek.Api.Middlewares;
using Codlek.Api;
using Codlek.Application;
using Codlek.Infrastructure.Auth;
using Codlek.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(
    builder.Configuration, builder.Environment.IsDevelopment());
builder.Services.AddApiServices(builder.Configuration);

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

/*
  🔴 **`UseAuthentication` قبل `UseAuthorization` — والاتنين لازمين.**

  المشروع المرجعي فيه `UseAuthorization` لوحدها، ومفيش
  `UseAuthentication`. والنتيجة إن إعدادات التوكن كلها متظبّطة
  و**مابتتطبّقش**: كل طلب بيوصل مجهول الهوية، فـ`[Authorize]` بترفض كل
  حاجة و`User` بيبقى فاضي.

  ⚠️ والترتيب نفسه مهم: `UseAuthorization` بتسأل «مين ده؟»، و
  `UseAuthentication` هي اللي بتجاوب. لو اتقلبوا، بتسأل قبل ما حد
  يجاوب — ونفس النتيجة بالظبط.
*/
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

/*
  🔴 **زرع الأدوار بعد البناء وقبل التشغيل.**

  لو اتعمل جوّه طلب، أول مستخدم بيدخل بياخد تأخير، وطلبين في نفس
  اللحظة بيحاولوا يزرعوا نفس الدور. وهنا بيحصل مرة واحدة على خط
  واحد.

  ⚠️ وفشله بيمنع الإقلاع عن قصد — راجع `IdentitySeeder`.
*/
using (var scope = app.Services.CreateScope())
{
    int added = await scope.ServiceProvider
        .GetRequiredService<IdentitySeeder>()
        .SeedRolesAsync();

    if (added > 0)
        app.Logger.LogInformation("اتزرع {Count} دور جديد.", added);
}

app.Run();
