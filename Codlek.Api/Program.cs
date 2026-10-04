using Codlek.Api;
using Codlek.Application;
using Codlek.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

/*
  🔴 **الملف ده يفضل قصير — ده كل الفكرة.**

  في المشروع القديم `Program.cs` وصل ١٬٢٤٨ سطر: تسجيل خدمات + سياسات
  + وسطاء + شغل بدء التشغيل + ١٣ نقطة نهاية مكتوبة جوّاه. وهو أكتر ملف
  اتغيّر في المشروع كله (٣٢ مرة) — يعني كل حاجة بتعدّي من هنا، وأي حد
  جديد بيفتحه ويسيبه.

  كل طبقة بتسجّل نفسها في `DependencyInjection.cs` بتاعها، والملف ده
  بينده عليهم وبس.
*/

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApiServices(builder.Configuration);

var app = builder.Build();

app.UseApiPipeline();
app.Run();

/*
  ⚠️ **موجود عشان فحوص الـAPI تقدر تقوّم السيرفر**
  (`WebApplicationFactory<Program>`).

  ⚠️ **ومن غير `namespace` عن قصد.** في المشروع القديم `Program`
  و`Policies` و`SessionUser` في الـglobal namespace، و٨٧ ملف فحص
  بيلاقوهم كده. أي `namespace` هنا بيكسرهم كلهم.
*/
public partial class Program { }
