using System.Reflection;
using Codlek.Application.Behaviors;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Codlek.Application;

/// <summary>تسجيل طبقة التطبيق — الأوامر والاستعلامات والتحقق.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        /*
          🔴 **نداء واحد، مش اتنين.**

          `AddMediatR` بترمي لو اتنادت من غير تجميعة:
          «No assemblies found to scan». وده بيوقّف الإقلاع — جرّبناه
          فعلاً. فالتسجيل والسلوك لازم يبقوا في نفس النداء.

          ⚠️ **و`AddOpenBehavior` هي اللي بتشغّل المتحقّقات.**
          `AddValidatorsFromAssembly` بتسجّلهم وبس. واحد من غير التاني
          = متحقّقات موجودة ومحدش بينديها، وده اللي كان حاصل في
          المشروع المرجعي: ١٣ متحقّق ومفيش ولا بيانات بتتفحص.
        */
        services.AddMediatR(c =>
        {
            c.RegisterServicesFromAssembly(assembly);
            c.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly);

        /*
          ⚠️ **الانتقالات خدمة مسجّلة، مش ملف ثابت.**

          هي محتاجة مستودع وكاتب حركات ومسجّل — فهي بتتسجّل
          بـ`Scoped` زي أي حاجة بتلمس القاعدة.

          🔴 ولازم تفضل في طبقة التطبيق مش البنية التحتية: **مسار
          المزامنة من الراكة** هيناديها، ومن غيرها السبعة انتقالات
          هيتكرّروا هناك — وفني أوفلاين يقدر يقفل أمر مش من حقه.
        */
        services.AddScoped<Interfaces.IRepairTransitions, Features.Repairs.RepairTransitions>();

        /*
          🔴 **مترجم معرّف الجهاز للكانوني الحيّ.**

          مش مستودع — هو لفّة منطق فوق قرايتين. وبيتسجّل هنا لأن
          مسار الاستقبال ومسار الدفعات الاتنين بيستعملوه، ولازم
          يستعملوا **نفس** القاعدة: «موجود» معناها حيّ مش «ليه صف».

          ⚠️ ونسيان السطر ده بيوقّع الإقلاع كله — وده أحسن من
          إنه يشتغل بنص الترجمة.
        */
        services.AddScoped<Features.Rack.IngestReports.DeviceReferenceResolver>();
        services.AddScoped<Features.Rack.SyncDevices.DeviceSyncApplier>();

        return services;
    }
}
