using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Codlek.Infrastructure;

/// <summary>تسجيل طبقة البنية التحتية — القاعدة والمستودعات والتحقق.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        // ⚠️ هيتملا في المرحلة ٢: DbContext على **نفس** قاعدة الإنتاج،
        // والمستودعات، و UnitOfWork.
        return services;
    }
}
