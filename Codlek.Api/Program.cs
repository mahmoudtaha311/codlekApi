using Codlek.Api.Dashboard;
using Codlek.Api.Middlewares;
using Codlek.Api.Racks;
using Codlek.Api;
using Codlek.Application;
using Codlek.Application.Contracts.Wire;
using Codlek.Infrastructure.Auth;
using Codlek.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

/*
  🔴 **كل تاريخ بيخرج على السلك UTC صريح بحرف `Z` — ودي كانت ناقصة.**

  من غير ده، EF بيرجّع `Kind = Unspecified`، والـJSON بيتكتب من غير
  منطقة (`2026-10-04T07:12:02.9533225`)، والمتصفح بيقراه **توقيت
  محلي** — فتحويل «اعرض بتوقيت القاهرة» في الواجهة بيبقى بلا أثر
  والمدير بيشوف وقت غلط بساعتين أو تلاتة.

  🔴 **و`AddJsonOptions` مش `ConfigureHttpJsonOptions`.**

  القديم بيستعمل التانية — وهي بتنفع مع **المسارات البسيطة** بس.
  والمشروع ده كله كنترولرز، فنفس السطر هناك مالوش أي أثر هنا.
  واللي بان من ضرب HTTP حقيقي: كل تاريخ في كل نقطة كان بيخرج من
  غير `Z`.

  ⚠️ **والتخزين مابيتغيّرش** — كله بيفضل UTC في القاعدة. اللي
  بيتصلّح هنا هو إن السلك يقول الحقيقة عن اللي بيبعته.
*/
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
        o.JsonSerializerOptions.Converters.Add(new NullableUtcDateTimeConverter());
    });
builder.Services.AddOpenApi();

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(
    builder.Configuration, builder.Environment.IsDevelopment());
builder.Services.AddApiServices(
    builder.Configuration, builder.Environment.IsDevelopment());

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
/*
  🔴 **`UseRateLimiter` قبل التحقق.**

  الحد على تسجيل المحطة ودخول الفني مفروض **قبل** أي شغل: الطلب
  المخنوق مالوش يوصل لقراية قاعدة ولا لتحقق تشفيري. والترتيب ده
  هو اللي بيخلّي الحد حاجز حقيقي مش عدّاد بعد الواقعة.
*/
app.UseDashboardAccess();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// 🔴 باسورد مؤقت = مفيش وصول غير لتغييره — شوف ForcedPasswordChangeGate.
app.UseForcedPasswordChange();

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

/*
  🔴 **الكلاس الفاضي ده هو اللي بيخلّي فحوص العقد تشغّل
  السيرفر فعلاً.**

  `WebApplicationFactory<Program>` محتاج النوع يبقى مرئي، والمشروع
  بمستوى أعلى (top-level statements) بيولّد `Program` داخلي.

  ⚠️ **ومن غير الفحوص دي، جسم الـ`401` على مسارات الراكة
  مابيتقاسش من خلال الأنبوب** — وده بالظبط اللي سرّب `ProblemDetails`
  على رد المفروض **فاضي**: فحوص الوحدة كانت بتشوف
  `UnauthorizedResult` وتعدّي خضرا، و`[ApiController]` بيحوّلها بعد
  كده لجسم JSON.
*/
public partial class Program;
