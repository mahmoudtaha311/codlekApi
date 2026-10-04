using Codlek.Application.Interfaces.Repositories;
using Codlek.Application.Interfaces;
using Codlek.Core.Entities.Auth;
using Codlek.Infrastructure.Auth;
using Codlek.Infrastructure.Data;
using Codlek.Infrastructure.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Codlek.Infrastructure;

/// <summary>تسجيل طبقة البنية التحتية — القاعدة والمستودعات والتحقق.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isDevelopment = false)
    {
        services.AddDbContext<AppDbContext>(o =>
            o.UseSqlServer(
                SqlServerConnection.Resolve(configuration, isDevelopment),
                sql =>
                {
                    /*
                      ⚠️ **القاعدة المُدارة بتنام.**

                      أول استعلام بعد ما تنام بيرجع خطأ عابر. من غير الإعادة
                      دي، المدير بيشوف صفحة خطأ بدل ما يستنى ثانية. نفس الإعداد
                      اللي في المشروع القديم بالحرف.

                      🔴 وده بيمنع <c>BeginTransaction</c> اليدوي: أي معاملة
                      صريحة لازم تعدّي على <c>CreateExecutionStrategy</c>، وإلا
                      EF بترمي وقت التشغيل مش وقت البناء.
                    */
                    sql.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorNumbersToAdd: null);

                    sql.CommandTimeout(60);
                }));

        services.AddIdentityServices();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()

            // 🔴 **التحقق وقت الإقلاع مش وقت أول دخول.**
            //
            // الافتراضي إن الإعدادات بتتقرا أول مرة حد يطلبها. يعني
            // مفتاح JWT ناقص = السيرفر بيقلع عادي، وأول واحد يحاول
            // يدخل بياخد 500. والنشر بيبان ناجح.
            .ValidateOnStart();

        services.AddScoped<ITokenIssuer, JwtTokenIssuer>();
        services.AddScoped<ILoginSessions, LoginSessions>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAuditTrail, AuditTrail>();

        /*
          ⚠️ **مستودع لكل قطاع، مش مستودع عام.**

          `IGenericRepository<T>` بـ`GetAll`/`Find` بيرجّع الترشيح
          بالشركة لكل مكان نداء — وأول واحد ينساه يفتح بيانات
          شركة تانية. المستودعات هنا دوالها بتاخد `tenantId` إجباري.
        */
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IBrandRepository, BrandRepository>();
        services.AddScoped<IUserAccountRepository, UserAccountRepository>();
        services.AddScoped<IContainerRepository, ContainerRepository>();
        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<IRepairRepository, RepairRepository>();
        services.AddScoped<IHardwareRepository, HardwareRepository>();
        services.AddScoped<IHandoverRepository, HandoverRepository>();
        services.AddScoped<IAnalyticsRepository, AnalyticsRepository>();
        services.AddScoped<IAuditRepository, AuditRepository>();
        services.AddScoped<IDeviceReference, DeviceReference>();
        services.AddScoped<ITenantCounters, TenantCounters>();
        services.AddScoped<IDeviceWorkflowRecorder, DeviceWorkflowRecorder>();

        return services;
    }

    /// <summary>
    /// Identity — المستخدمين والأدوار.
    ///
    /// <para>🔴 <b><c>AddIdentityCore</c> مش <c>AddIdentity</c>.</b>
    /// <c>AddIdentity</c> بتجيب معاها تحقق بالكوكي وبتحوّل الطلب
    /// المرفوض على صفحة دخول. وده API بيرجّع توكن: اللي بيستعمله
    /// الراكة والداش بورد محتاجين <c>401</c> صريح، مش <c>302</c>
    /// على صفحة HTML. الراكة بتقرا الـ<c>302</c> ومعاها HTML، وبتفهمه
    /// نجاح — <b>وبتمسح الصف من طابورها</b>. يعني شغل بيضيع في سكوت.</para>
    /// </summary>
    private static IServiceCollection AddIdentityServices(this IServiceCollection services)
    {
        services
            .AddIdentityCore<ApplicationUser>(o =>
            {
                /*
                  🔴 **٨ بدل ٤ — وده فرق مقصود عن المشروع القديم.**

                  القديم فيه `MinPasswordLength = 4`. وأربع حروف
                  بتتخمّن بالقوة في ثواني.

                  ⚠️ **ومحدش بيتقفل برّه بسبب ده:** مفيش فحص طول
                  وقت **الدخول** — راجع `LoginCommandValidator`. فاللي
                  باسورده تلات حروف بيدخل عادي، بس أول ما يغيّره
                  لازم يطوّله.

                  ⚠️ **وفي فرق سلوك وقت التحويل:** لو الاتنين شغّالين
                  مع بعض، باسورد ٥ حروف بيتقبل من اللوحة القديمة
                  وبيترفض من الجديدة. وده مقبول لأن الجديد مابيخدمش
                  حد لحد التحويل، والتحويل بينقل الداش بورد مرة واحدة.
                */
                o.Password.RequiredLength = 8;

                /*
                  ⚠️ **مفيش شرط رموز أو حروف كبيرة عن قصد.**

                  اللي بيستعملوا النطام فنيين ومديرين في ورشة، بيكتبو
                  الباسورد على كيبورد عربي. شرط `P@ssw0rd!` بينتهي
                  بورقة ملزوقة على الشاشة — وده أسوأ من باسورد أطول
                  وأبسط.
                */
                o.Password.RequireNonAlphanumeric = false;
                o.Password.RequireUppercase = false;
                o.Password.RequireDigit = false;

                // ⚠️ الاسم بيتطبّع بقاعدة Identity، والقديم بيتطبّع
                // بـ`LoginName.Normalize`. القاعدتين مش واحدة — فنقل
                // الحسابات وقت التحويل لازم يحسب الاسم المطبَّع من
                // جديد، ماينقلهوش من العمود القديم.
                o.User.RequireUniqueEmail = false;
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<AppDbContext>();

        /*
          🔴 **المطبّع ده بيوحّد مفتاح الدخول بين الموقع والراكة.**

          Identity الافتراضية بتعمل `ToUpperInvariant`، والمشروع
          (والبرنامج المكتبي) بيستعملوا `LoginName.Normalize` — حروف
          صغيرة ومقصوصة على ٦٠.

          ⚠️ ولو اتسابوا مختلفين، اسم يدخل على الراكة ومايدخلش
          على الموقع.
        */
        services.Replace(ServiceDescriptor.Scoped<ILookupNormalizer, LoginNameNormalizer>());

        /*
          🔴 **السطر ده هو اللي بيخلّي الباسوردات الموجودة تشتغل.**

          `AddIdentityCore` بتسجّل `PasswordHasher<ApplicationUser>`
          الافتراضي، واللي مابيعرفش الشكل القديم (بصمة وملح في عمودين).
          `Replace` بتشيله وتحط اللي بيفهم الاتنين.

          ⚠️ ولازم يبقى **بعد** `AddIdentityCore`: `Replace` بتدوّر على
          تسجيل موجود، فلو اتنادت قبلها مش هتلاقي حاجة تشيلها — وتفضل
          البصمة الافتراضية هي الشغّالة من غير أي خطأ ظاهر.
        */
        services.Replace(ServiceDescriptor.Scoped<
            IPasswordHasher<ApplicationUser>, LegacyPasswordHasher>());

        services.AddScoped<IdentitySeeder>();

        return services;
    }
}
