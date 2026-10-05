using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Users;
using Codlek.Application.Features.Users.ResetUserPassword;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Core.Entities.Auth;
using Codlek.Core.Enums;
using Codlek.Infrastructure;
using Codlek.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Codlek.Tests;

/// <summary>قاعدة فحوص إعادة تعيين باسورد اللوحة.</summary>
public sealed class UserPasswordResetDbFixture : SqlServerDbFixture
{
    protected override string DatabaseName => "codlek_user_reset_test";
}

/// <summary>
/// المدير بيعيّن باسورد جديد لحساب تاني — <b>على قاعدة حقيقية وبنفس
/// توصيل الإنتاج</b> (<c>AddInfrastructureServices</c>).
///
/// <para>🔴 <b>الفحوص دي موجودة بسبب حساب كان بيتقفل.</b> الإعادة كانت
/// بتشيل الباسورد القديم (وده بيتحفظ على طول) وبعدين تفحص الجديد —
/// فباسورد أقصر من ٨ كان بيسيب الحساب <b>من غير باسورد خالص</b>، واللوحة
/// كانت بتقبل ٤ حروف. اتلقط في فحص نسخة التجربة على بيانات حقيقية
/// يوم ٥ أكتوبر.</para>
///
/// <para>⚠️ والقراية بعد كل عملية من <b>نطاق جديد</b> — يعني
/// <c>DbContext</c> جديد. الكيان اللي في الذاكرة ممكن يكون لسه شايل
/// البصمة القديمة وهي اتمسحت من القاعدة، وده بالظبط اللي كان مستخبّي.</para>
/// </summary>
public class UserPasswordResetTests(UserPasswordResetDbFixture fixture)
    : IClassFixture<UserPasswordResetDbFixture>
{
    private const string OldPassword = "old-password-1";
    private const string NewPassword = "new-password-2";

    private sealed class Owner(Guid tenantId) : ICurrentUser
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Guid TenantId => tenantId;
        public string DisplayName => "صاحب الورشة";
        public string Code => "O001";
        public UserRole Role => UserRole.Owner;
        public bool IsAuthenticated => true;
    }

    private ServiceProvider Services(Guid tenantId)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{SqlServerConnection.Name}"] = fixture.ConnectionString,

                // ⚠️ مفتاح للفحص بس — الجلسات بتحتاجه عشان تتبني.
                ["Jwt:Key"] = new string('k', 48),
            })
            .Build();

        var services = new ServiceCollection()
            .AddLogging()
            .AddInfrastructureServices(configuration);

        services.AddScoped<ICurrentUser>(_ => new Owner(tenantId));

        return services.BuildServiceProvider();
    }

    /// <summary>شركة + حساب فني بباسورد معروف، من <c>CreateAsync</c> زي الحقيقة.</summary>
    private async Task<(ServiceProvider Services, Guid UserId)> SeedAsync()
    {
        var tenant = new Tenant { Name = "ورشة " + Guid.NewGuid().ToString("N")[..6] };

        await using (var db = fixture.Create())
        {
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();
        }

        var services = Services(tenant.Id);

        using var scope = services.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            UserName = "t" + Guid.NewGuid().ToString("N")[..10],
            DisplayName = "فني الورشة",
            Code = "T" + Guid.NewGuid().ToString("N")[..5],
            Role = UserRole.Technician,
            IsActive = true,
            CredentialVersion = 1,
        };

        var created = await identity.CreateAsync(user, OldPassword);
        Assert.True(created.Succeeded, string.Join(" ", created.Errors.Select(e => e.Description)));

        return (services, user.Id);
    }

    private static async Task<Result<UserAccountResult>> ResetAsync(
        ServiceProvider services, Guid userId, string password)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        var handler = new ResetUserPasswordCommandHandler(
            sp.GetRequiredService<IUserAccountRepository>(),
            sp.GetRequiredService<UserManager<ApplicationUser>>(),
            sp.GetRequiredService<ILoginSessions>(),
            sp.GetRequiredService<IAuditTrail>(),
            sp.GetRequiredService<IUnitOfWork>(),
            sp.GetRequiredService<ICurrentUser>(),
            sp.GetRequiredService<IAccountStanding>());

        return await handler.Handle(new ResetUserPasswordCommand(userId, password), CancellationToken.None);
    }

    /// <summary>اللي في القاعدة فعلاً — من نطاق جديد.</summary>
    private static async Task<(ApplicationUser User, bool OldWorks, bool NewWorks)> StoredAsync(
        ServiceProvider services, Guid userId)
    {
        using var scope = services.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await identity.FindByIdAsync(userId.ToString());
        Assert.NotNull(user);

        return (user!,
            await identity.CheckPasswordAsync(user!, OldPassword),
            await identity.CheckPasswordAsync(user!, NewPassword));
    }

    /// <summary>
    /// 🔴 <b>باسورد مرفوض مابيلمسش القديم.</b> الحساب لسه بيدخل بباسورده،
    /// ولا اتجبر على تغيير، ولا جلساته اتقفلت — كأن المحاولة ماحصلتش.
    /// </summary>
    [Theory]
    [InlineData("1234")]
    [InlineData("abcdefg")]
    public async Task A_rejected_password_leaves_the_old_one_working(string tooShort)
    {
        var (services, userId) = await SeedAsync();
        await using var owned = services;

        var result = await ResetAsync(services, userId, tooShort);

        Assert.False(result.IsSuccess);
        Assert.Equal("user.password_rejected", result.Error.Code);

        var (user, oldWorks, _) = await StoredAsync(services, userId);

        Assert.NotNull(user.PasswordHash);
        Assert.True(oldWorks, "الباسورد القديم اتشال مع إن الجديد اترفض — الحساب اتقفل");
        Assert.False(user.MustChangePassword);
        Assert.Equal(1, user.CredentialVersion);
    }

    /// <summary>
    /// ⚠️ <b>والرفض بيقول الحد بالعربي</b> — مش «Passwords must be at least
    /// 8 characters» جوّه شاشة عربي.
    /// </summary>
    [Fact]
    public async Task The_rejection_names_the_minimum_in_arabic()
    {
        var (services, userId) = await SeedAsync();
        await using var owned = services;

        var result = await ResetAsync(services, userId, "1234");

        Assert.Equal("كلمة المرور قصيرة — لازم ٨ حروف على الأقل.", result.Error.Description);
    }

    /// <summary>
    /// 🔴 <b>الطول هو الشرط الوحيد</b> — زي القديم. أرقام بس (رقم
    /// موبايل)، أو عربي بس، أو حروف كبيرة بس: كله مقبول طالما ٨ أو أكتر.
    /// Identity كانت لسه طالبة «حرف صغير» لأن السطر اللي بيلغيه كان ناقص.
    /// </summary>
    [Theory]
    [InlineData("01012345678")]
    [InlineData("كلمةسرطويلة")]
    [InlineData("ABCDEFGH")]
    public async Task Only_the_length_matters(string password)
    {
        var (services, userId) = await SeedAsync();
        await using var owned = services;

        var result = await ResetAsync(services, userId, password);

        Assert.True(result.IsSuccess, result.Error.Description);
    }

    /// <summary>
    /// ✅ <b>والباسورد المقبول بيحلّ محل القديم</b>، وصاحب الحساب لازم
    /// يغيّره أول ما يدخل، وجلساته المفتوحة بتقف (النسخة زادت).
    /// </summary>
    [Fact]
    public async Task An_accepted_password_replaces_the_old_one_and_forces_a_change()
    {
        var (services, userId) = await SeedAsync();
        await using var owned = services;

        var result = await ResetAsync(services, userId, NewPassword);

        Assert.True(result.IsSuccess, result.Error.Description);

        var (user, oldWorks, newWorks) = await StoredAsync(services, userId);

        Assert.False(oldWorks);
        Assert.True(newWorks);
        Assert.True(user.MustChangePassword);
        Assert.Equal(2, user.CredentialVersion);
    }
}
