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

        return services;
    }
}
