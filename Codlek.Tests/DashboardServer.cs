using System.Net.Http.Headers;
using Codlek.Application.Contracts.Auth;
using Codlek.Application.Interfaces;
using Codlek.Core.Entities;
using Codlek.Core.Entities.Auth;
using Codlek.Core.Enums;
using Codlek.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Codlek.Tests;

/// <summary>
/// سيرفر حقيقي على <b>قاعدة فحص لوحدها</b> — عشان طلبات اللوحة بتوكن.
///
/// <para>🔴 <b>ليه محتاج قاعدة.</b> كل طلب بتوكن بيتفحص على صف الحساب
/// (<c>AccountStanding</c>): موقوف؟ نسخته اتغيّرت؟ اتمسح؟ فتوكن لمستخدم
/// مش موجود — اللي كانت فحوص الأنبوب بتعمله بـ<c>Guid.NewGuid()</c> —
/// بقى بيترفض <c>401</c>، وده الصح.</para>
///
/// <para>⚠️ القاعدة بتتبدّل في الحاوية نفسها زي <c>RackBatchServer</c>
/// بالظبط، والسيرفر مابيقبلش يقوم لو متوصّل بقاعدة غير قاعدة الفحص.</para>
/// </summary>
public sealed class DashboardServer : WebApplicationFactory<Program>
{
    private sealed class Db : SqlServerDbFixture
    {
        protected override string DatabaseName => "codlek_dashboard_session_test";
    }

    private readonly Db _db = new();

    public const string Collection = "DashboardServer";

    public const string Password = "Session-Pass-1";

    public Guid TenantId { get; }

    public DashboardServer()
    {
        using var scope = Services.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        string actual = db.Database.GetDbConnection().Database;
        string expected = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(
            _db.ConnectionString).InitialCatalog;

        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"السيرفر متوصّل بـ{actual} مش بقاعدة الفحص {expected} — وقفنا قبل أي كتابة.");

        var tenant = new Tenant { Name = "ورشة الجلسات" };
        db.Tenants.Add(tenant);
        db.SaveChanges();

        TenantId = tenant.Id;
    }

    public AppDbContext CreateDb() => _db.Create();

    /// <summary>حساب حقيقي بباسورد <see cref="Password"/> — من <c>CreateAsync</c> زي الحقيقة.</summary>
    public async Task<ApplicationUser> AddUserAsync(UserRole role, bool mustChange = false)
    {
        using var scope = Services.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            UserName = "s" + Guid.NewGuid().ToString("N")[..12],
            DisplayName = "حساب فحص",
            Code = "S" + Guid.NewGuid().ToString("N")[..5],
            Role = role,
            IsActive = true,
            CredentialVersion = 1,
            MustChangePassword = mustChange,
        };

        var created = await identity.CreateAsync(user, Password);
        Assert.True(created.Succeeded, string.Join(" ", created.Errors.Select(e => e.Description)));

        return user;
    }

    /// <summary>
    /// توكن وصول من نفس المُصدِر — <b>بالقيم اللي في الصف</b>، زي الدخول
    /// بالظبط. (من غير مسار الدخول عشان حدّ المحاولات مايدخلش في الفحص.)
    /// </summary>
    public string AccessTokenFor(ApplicationUser user)
    {
        using var scope = Services.CreateScope();

        return scope.ServiceProvider.GetRequiredService<ITokenIssuer>().Issue(new TokenSubject(
            UserId: user.Id,
            TenantId: user.TenantId,
            Username: user.UserName ?? "",
            DisplayName: user.DisplayName,
            Code: user.Code,
            Role: user.Role.ToString(),
            CredentialVersion: user.CredentialVersion,
            MustChangePassword: user.MustChangePassword)).AccessToken;
    }

    public HttpClient ClientWith(string accessToken)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    public HttpClient ClientFor(ApplicationUser user) => ClientWith(AccessTokenFor(user));

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureHostConfiguration(config =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Server:PublicBaseUrl"] = "http://localhost:5097",
            }));

        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            var doomed = services
                .Where(d => d.ServiceType == typeof(AppDbContext)
                         || d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                         || d.ServiceType == typeof(DbContextOptions)
                         || (d.ServiceType.IsGenericType
                             && d.ServiceType.Name.StartsWith("IDbContextOptionsConfiguration")
                             && d.ServiceType.GenericTypeArguments[0] == typeof(AppDbContext)))
                .ToList();

            foreach (var d in doomed) services.Remove(d);

            services.AddDbContext<AppDbContext>(o => o.UseSqlServer(
                _db.ConnectionString,
                sql =>
                {
                    sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
                    sql.CommandTimeout(60);
                }));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing) _db.Dispose();
    }
}

/// <summary>
/// ⚠️ <b>مجموعة مش fixture لكل كلاس.</b> xunit بيعمل نسخة لكل كلاس
/// وبيشغّلهم بالتوازي — واسم القاعدة واحد، فالاتنين كانوا بيعملوا
/// <c>EnsureCreated</c> على نفس القاعدة في نفس اللحظة. المجموعة = سيرفر
/// واحد وقاعدة واحدة، وكل فحص بيزرع حساباته لوحده.
/// </summary>
[CollectionDefinition(DashboardServer.Collection)]
public sealed class DashboardServerCollection : ICollectionFixture<DashboardServer>;
