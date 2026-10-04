using Microsoft.Extensions.DependencyInjection;

namespace Codlek.Application;

/// <summary>
/// تسجيل طبقة التطبيق.
///
/// <para>⚠️ <b>ملف واحد لكل طبقة، والطبقة بتسجّل نفسها.</b>
/// <c>Program.cs</c> بينده على تلات دوال وبس — ده اللي بيمنعه يرجع
/// ١٬٢٤٨ سطر زي القديم.</para>
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // ⚠️ هيتملا في المرحلة ٣ (MediatR + Mapster + ValidationBehavior).
        // سايبينه فاضي عن قصد بدل ما نسجّل حاجات لسه مش موجودة.
        return services;
    }
}
