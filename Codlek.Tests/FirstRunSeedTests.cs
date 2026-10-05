using Codlek.Application;
using Codlek.Application.Contracts.Maintenance;
using Codlek.Application.Features.Maintenance.SeedFirstRun;
using Codlek.Core.Entities;
using Codlek.Core.Entities.Auth;
using Codlek.Core.Enums;
using Codlek.Infrastructure;
using Codlek.Infrastructure.Data;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Codlek.Tests;

/// <summary>قاعدة فاضية تماماً — لبذرة أول تشغيل.</summary>
public sealed class FirstRunEmptyDbFixture : SqlServerDbFixture
{
    protected override string DatabaseName => "codlek_first_run_empty_test";
}

/// <summary>قاعدة فيها شركة من غير مالك — لبذرة أول تشغيل.</summary>
public sealed class FirstRunOwnerlessDbFixture : SqlServerDbFixture
{
    protected override string DatabaseName => "codlek_first_run_ownerless_test";
}

/// <summary>الشغل المشترك بين فحصين بذرة أول تشغيل.</summary>
internal static class FirstRunSeeding
{
    public static ServiceProvider Services(string connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{SqlServerConnection.Name}"] = connectionString,
                ["Jwt:Key"] = new string('k', 48),
            })
            .Build();

        return new ServiceCollection()
            .AddLogging()
            .AddApplicationServices()
            .AddInfrastructureServices(configuration)
            .BuildServiceProvider();
    }

    public static async Task<FirstRunSeedResult> RunAsync(ServiceProvider services)
    {
        using var scope = services.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new SeedFirstRunCommand());

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : "");
        return result.Value;
    }
}

/// <summary>
/// 🔴 <b>قاعدة فاضية بتتفتح بـ<c>admin / admin</c> — زي القديم.</b> من
/// غيرها قاعدة جديدة مالهاش أي دخول، ولازم حد يفتح القاعدة بإيده.
///
/// <para>⚠️ <b>فحص واحد في الكلاس عن قصد:</b> الحالة اللي بيتفحص عليها
/// («مفيش ولا شركة») بتتلغي أول ما البذرة تشتغل، وxunit مابيضمنش ترتيب
/// الفحوص جوّه الكلاس.</para>
/// </summary>
public class FirstRunSeedTests(FirstRunEmptyDbFixture fixture)
    : IClassFixture<FirstRunEmptyDbFixture>
{
    [Fact]
    public async Task An_empty_database_gets_one_tenant_and_an_owner_who_must_change_admin_admin()
    {
        await using var services = FirstRunSeeding.Services(fixture.ConnectionString);

        var first = await FirstRunSeeding.RunAsync(services);

        Assert.True(first.Seeded);
        Assert.NotNull(first.TenantId);

        await using (var db = fixture.Create())
        {
            var tenant = await db.Tenants.AsNoTracking().SingleAsync();
            Assert.Equal("CODLEK", tenant.Name);
            Assert.Equal(first.TenantId, tenant.Id);
        }

        using (var scope = services.CreateScope())
        {
            var identity = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            var owner = await identity.FindByNameAsync("admin");
            Assert.NotNull(owner);

            Assert.Equal(first.TenantId, owner!.TenantId);
            Assert.Equal(UserRole.Owner, owner.Role);
            Assert.Equal("مدير", owner.DisplayName);
            Assert.True(owner.IsActive);

            // 🔴 الباسورد معروف للكل — لازم يتغيّر قبل أي حاجة.
            Assert.True(owner.MustChangePassword);
            Assert.True(await identity.CheckPasswordAsync(owner, "admin"));
            Assert.Matches("^[0-9]{6}$", owner.Code);
        }

        // ⚠️ التشغيلة التانية مابتعملش حاجة — ولا شركة تانية ولا حساب تاني.
        var second = await FirstRunSeeding.RunAsync(services);
        Assert.False(second.Seeded);

        await using (var db = fixture.Create())
        {
            Assert.Equal(1, await db.Tenants.CountAsync());
            Assert.Equal(1, await db.Set<ApplicationUser>().CountAsync());
        }
    }
}

/// <summary>
/// 🔴 <b>شركة موجودة من غير مالك = مفيش <c>admin</c>.</b>
///
/// <para>⚠️ <b>فرق مقصود عن القديم</b> (اللي كان بيعمل <c>admin</c> لأي
/// شركة مالهاش مالك). الجديد شغّال على نسخة من الإنتاج، ومالك اتشال أو
/// اتغيّرت صلاحيته بالغلط كان هيفتح حساب بباسورد معروف للكل على قاعدة
/// حقيقية.</para>
/// </summary>
public class FirstRunOwnerlessTenantTests(FirstRunOwnerlessDbFixture fixture)
    : IClassFixture<FirstRunOwnerlessDbFixture>
{
    [Fact]
    public async Task A_database_with_a_tenant_but_no_owner_gets_no_default_account()
    {
        await using (var db = fixture.Create())
        {
            db.Tenants.Add(new Tenant { Name = "ورشة من غير مالك" });
            await db.SaveChangesAsync();
        }

        await using var services = FirstRunSeeding.Services(fixture.ConnectionString);

        var result = await FirstRunSeeding.RunAsync(services);

        Assert.False(result.Seeded);

        await using var check = fixture.Create();
        Assert.Equal(1, await check.Tenants.CountAsync());
        Assert.Equal(0, await check.Set<ApplicationUser>().CountAsync());
    }
}
