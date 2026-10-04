using Codlek.Core.Entities.Auth;
using Codlek.Core.Enums;
using Codlek.Infrastructure;
using Codlek.Infrastructure.Auth;
using Codlek.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Codlek.Tests;

/// <summary>
/// الأدوار والتوصيل — <b>الحاجات اللي بتقع في سكوت</b>.
///
/// <para>⚠️ الفحوص دي مش بتفحص منطق، هي بتفحص <b>إن اللي كتبناه
/// بيتستعمل فعلاً</b>. اللي بيحصل غير كده: الكود صح، ومسجَّل غلط،
/// والنظام بيشتغل بالسلوك الافتراضي من غير أي رسالة.</para>
/// </summary>
public class IdentityWiringTests
{
    // =================================================================
    //  الأدوار
    // =================================================================

    /// <summary>
    /// 🔴 كل دور في <see cref="UserRole"/> له صف هيتزرع.
    ///
    /// <para>الفحص ده بيقع لوحده لو حد زوّد دور في الـenum ونسي
    /// الزرع — وده بالظبط اللي بيخلّي حساب يدخل وبعدين مايعرفش يعمل
    /// حاجة.</para>
    /// </summary>
    [Fact]
    public void Every_role_in_the_enum_gets_seeded()
    {
        var seeded = RoleSeed.All().Select(r => r.Role).ToHashSet();

        Assert.Equal(Enum.GetValues<UserRole>().ToHashSet(), seeded);
    }

    /// <summary>
    /// 🔴 اسم الدور = اسم الـenum بالحرف.
    ///
    /// <para>لأن الصلاحيات بتتكتب كده:
    /// <c>[Authorize(Roles = "Manager")]</c>. أي حرف مختلف معناه إن
    /// الشرط مابيطابقش — والنتيجة <c>403</c> لمستخدم من حقه يدخل.</para>
    /// </summary>
    [Fact]
    public void The_role_name_matches_the_enum_name_exactly()
    {
        foreach (var (role, name, _) in RoleSeed.All())
            Assert.Equal(role.ToString(), name);
    }

    /// <summary>
    /// 🔴 <b>الأسماء العربية دي بتتعرض للمستخدم — فهي عقد.</b>
    ///
    /// <para>وكانت مكتوبة في مكانين واختلفوا فعلاً: <c>WebUser.RoleText</c>
    /// كان بيقول «مدير عام» و«مدير المخزن»، وزرع الأدوار كان بيقول
    /// «مالك» و«مدير». يعني نفس الشخص باسم دور مختلف على حسب الشاشة.</para>
    ///
    /// <para>⚠️ والنصوص هنا مكتوبة بالإيد عن قصد — ده الغرض من الفحص:
    /// لو حد غيّر <see cref="UserRoleText"/>، الفحص يقع ويقول إن اللي
    /// بيتعرض في الشاشة اتغيّر.</para>
    /// </summary>
    [Theory]
    [InlineData(UserRole.Owner, "مدير عام")]
    [InlineData(UserRole.Manager, "مدير المخزن")]
    [InlineData(UserRole.FloorManager, "مدير الدور")]
    [InlineData(UserRole.Accountant, "محاسب")]
    [InlineData(UserRole.Technician, "فني")]
    public void The_arabic_role_names_are_frozen(UserRole role, string expected) =>
        Assert.Equal(expected, UserRoleText.Arabic(role));

    /// <summary>
    /// ⚠️ وزرع الأدوار بياخد من نفس المكان — <b>مش نسخة</b>.
    ///
    /// <para>الفحص ده هو اللي بيمنع النسختين يرجعوا يفترقوا.</para>
    /// </summary>
    [Fact]
    public void Role_seeding_uses_the_same_arabic_names()
    {
        foreach (var (role, _, arabicName) in RoleSeed.All())
            Assert.Equal(UserRoleText.Arabic(role), arabicName);
    }

    /// <summary>⚠️ ومفيش دور اسمه العربي = اسمه الإنجليزي (يعني ناسي).</summary>
    [Fact]
    public void Every_role_has_a_real_arabic_name()
    {
        foreach (var (role, name, arabicName) in RoleSeed.All())
            Assert.NotEqual(name, arabicName);
    }

    // =================================================================
    //  التوصيل
    // =================================================================

    private static IServiceCollection BuildServices()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // ⚠️ نص اتصال وهمي. `UseSqlServer` مابتتصلش وقت
                // التسجيل، فالفحص مايحتاجش SQL Server شغّال.
                //
                // ⚠️ والاسم جاي من الثابت مش مكتوب — عشان لو اتغيّر،
                // الفحص يتحرك معاه بدل ما يقع لسبب مالوش علاقة.
                [$"ConnectionStrings:{SqlServerConnection.Name}"] = "Server=.;Database=none;",
            })
            .Build();

        return new ServiceCollection()
            .AddLogging()
            .AddInfrastructureServices(configuration);
    }

    private static ServiceProvider BuildProvider() => BuildServices().BuildServiceProvider();

    /// <summary>
    /// 🔴 <b>الفحص اللي يمنع تسجيل الباسوردات كلها تضيع.</b>
    ///
    /// <para><c>LegacyPasswordHasher</c> ينفع يبقى مكتوب صح ومجرَّب
    /// ومش مسجَّل — وساعتها Identity بتستعمل البصمة الافتراضية،
    /// وكل مستخدم قديم بياخد «الباسورد غلط» وهو صح. مفيش خطأ،
    /// مفيش سجل: بس الناس مش بتدخل.</para>
    ///
    /// <para>⚠️ وده كمان بيقع لو حد حرّك <c>Replace</c> قبل
    /// <c>AddIdentityCore</c> — لأنها ساعتها مالاقتش حاجة تشيلها.</para>
    /// </summary>
    [Fact]
    public void The_registered_password_hasher_understands_old_passwords()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<ApplicationUser>>();

        Assert.IsType<LegacyPasswordHasher>(hasher);
    }

    /// <summary>
    /// ⚠️ ومش بس النوع صح — هو فعلاً بيقرا بصمة قديمة من خلال
    /// الحاوية. (الفحص اللي فوق بيثبت التسجيل، وده بيثبت السلوك.)
    /// </summary>
    [Fact]
    public void The_registered_hasher_actually_verifies_a_legacy_hash()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<ApplicationUser>>();
        var (hash, salt) = PasswordHasher.Create("MyS3cret!");
        var user = new ApplicationUser { LegacySalt = salt };

        Assert.Equal(
            PasswordVerificationResult.SuccessRehashNeeded,
            hasher.VerifyHashedPassword(user, hash, "MyS3cret!"));
    }

    /// <summary>
    /// 🔴 <c>AddIdentityCore</c> مش <c>AddIdentity</c> — يعني مفيش
    /// تحقق بالكوكي اتسجّل.
    ///
    /// <para>لأن <c>AddIdentity</c> بتحوّل الطلب المرفوض على صفحة
    /// دخول HTML. والراكة بتقرا الرد ده نجاح <b>وبتمسح الصف من
    /// طابورها</b> — شغل بيضيع في سكوت.</para>
    /// </summary>
    [Fact]
    public void No_cookie_authentication_scheme_is_registered()
    {
        /*
          ⚠️ بنفحص قائمة التسجيلات نفسها، مش بنحاول نحلّ النوع.

          لأن تحويل الطلب على صفحة دخول بيحصل من `AddIdentity` وهي
          بتسجّل حاجات في `Microsoft.AspNetCore.Authentication`. والمشروع
          ده مابيشاورش على الحزمة دي خالص — فلو الاسم ده ظهر في
          القائمة، يبقى حد غيّر `AddIdentityCore` لـ`AddIdentity`.
        */
        var authRegistrations = BuildServices()
            .Where(d => d.ServiceType.FullName?
                .StartsWith("Microsoft.AspNetCore.Authentication") == true)
            .Select(d => d.ServiceType.FullName)
            .ToList();

        Assert.Empty(authRegistrations);
    }

    /// <summary>⚠️ وزارع الأدوار نفسه بيتحلّ من الحاوية.</summary>
    [Fact]
    public void The_role_seeder_resolves()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IdentitySeeder>());
    }

    // =================================================================
    //  نص الاتصال — نقطة التحويل
    // =================================================================

    /// <summary>
    /// 🔴 <b>الاسم لازم يبقى <c>SqlServer</c> بالحرف.</b>
    ///
    /// <para>الاستضافة فيها <c>ConnectionStrings__SqlServer</c> متحطوط في
    /// <c>web.config</c> من أيام المشروع القديم. فلو الجديد دوّر على
    /// <c>DefaultConnection</c> (الاسم الافتراضي في كل القوالب)،
    /// <b>مش هيلاقي حاجة</b>.</para>
    ///
    /// <para>⚠️ والفشل مش بيبان: في التطوير بيرجع للقاعدة
    /// المحلية في سكوت. يعني <b>السيرفر المنشور يقوم على
    /// قاعدة فاضية ويقول إنه شغّال</b>، والشغل بيتكتب في
    /// مكان غلط.</para>
    /// </summary>
    [Fact]
    public void The_connection_string_name_matches_the_old_project()
    {
        Assert.Equal("SqlServer", SqlServerConnection.Name);
    }

    /// <summary>
    /// 🔴 وفي الإنتاج، غياب نص الاتصال <b>بيرمي</b>.
    ///
    /// <para>مابيرجعش لأي قاعدة افتراضية — سيرفر قام على قاعدة
    /// غلط بيكتب شغل الورشة في مكان محدش بيبص فيه.</para>
    /// </summary>
    [Fact]
    public void A_missing_connection_string_throws_outside_development()
    {
        var empty = new ConfigurationBuilder().Build();

        var thrown = Assert.Throws<InvalidOperationException>(
            () => SqlServerConnection.Resolve(empty, isDevelopment: false));

        Assert.Contains("ConnectionStrings__SqlServer", thrown.Message);
    }

    /// <summary>⚠️ وفي التطوير بيرجع للقاعدة المحلية بدل ما يرمي.</summary>
    [Fact]
    public void Development_falls_back_to_the_local_instance()
    {
        var empty = new ConfigurationBuilder().Build();

        string resolved = SqlServerConnection.Resolve(empty, isDevelopment: true);

        // ⚠️ <b>`Data Source=` مش `Server=`.</b>
        // `SqlConnectionStringBuilder` بيعيد كتابة النص بالأسماء
        // القياسية، فاللي يقارن نص الاتصال بالنص اللي كتبه
        // بيلاقيه مختلف وهو نفسه.
        Assert.Contains("Data Source=localhost", resolved);

        // ⚠️ والتشفير بيتحط دايماً — راجع `Harden`.
        Assert.Contains("Encrypt=True", resolved);
    }
}
