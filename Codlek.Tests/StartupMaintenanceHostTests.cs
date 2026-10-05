using System.Net;
using Codlek.Api.Startup;
using Codlek.Application.Interfaces;
using Codlek.Core.Entities.Auth;
using Codlek.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Codlek.Tests;

/// <summary>قاعدة فاضية لإقلاع السيرفر الحقيقي بالصيانة.</summary>
public sealed class StartupMaintenanceHostDbFixture : SqlServerDbFixture
{
    protected override string DatabaseName => "codlek_startup_maintenance_host_test";
}

/// <summary>
/// السيرفر الحقيقي بالصيانة مفتوحة — <b>على قاعدة فحص فاضية</b>.
///
/// <para>🔴 <b>القاعدة بتتبدّل في الحاوية نفسها</b> — زي
/// <see cref="RackBatchServer"/>: نص الاتصال في التطوير جاي من أسرار
/// المستخدم، والصيانة بتكتب.</para>
///
/// <para>⚠️ <b>القفل متمسوك لحد ما الفحص يقول.</b> ده اللي بيثبت إن
/// <c>/api/health</c> بيرد والصيانة لسه ماخلصتش — مش بس إنها خلصت بسرعة.</para>
/// </summary>
public sealed class StartupMaintenanceHostServer : WebApplicationFactory<Program>
{
    private readonly StartupMaintenanceHostDbFixture _db = new();

    /// <summary>بيتفتح لما الفحص يسمح للصيانة تكمّل.</summary>
    public TaskCompletionSource Release { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>بيتفتح لما الصيانة توصل للقفل — يعني بدأت فعلاً.</summary>
    public TaskCompletionSource Reached { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public AppDbContext CreateDb() => _db.Create();

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
                sql => sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null)));

            // ⚠️ التطوير قافلها من `appsettings.Development.json` — هنا
            //    بنفتحها، ومن غير استنّى عشان الفحص مايطوّلش.
            services.Configure<StartupMaintenanceOptions>(o =>
            {
                o.Enabled = true;
                o.StartDelaySeconds = 0;
            });

            services.Replace(ServiceDescriptor.Scoped<IMaintenanceLock>(sp =>
                new GatedLock(new MaintenanceLock(sp.GetRequiredService<AppDbContext>()), this)));
        });
    }

    protected override void Dispose(bool disposing)
    {
        Release.TrySetResult();
        base.Dispose(disposing);

        if (disposing) _db.Dispose();
    }

    private sealed class GatedLock(IMaintenanceLock inner, StartupMaintenanceHostServer server)
        : IMaintenanceLock
    {
        public async Task<IAsyncDisposable?> TryAcquireAsync(string name, CancellationToken ct = default)
        {
            server.Reached.TrySetResult();
            await server.Release.Task.WaitAsync(ct);
            return await inner.TryAcquireAsync(name, ct);
        }
    }
}

/// <summary>
/// 🔴 <b>الصيانة شغّالة في السيرفر الحقيقي — ومابتأخّرش أول رد.</b> الراكة
/// بتخبط على <c>/api/health</c> أول ما السيرفر يقوم، والقديم كان بيخلّيها
/// تستنى لفّة على كل الفحوص اليتيمة.
/// </summary>
public class StartupMaintenanceHostTests(StartupMaintenanceHostServer server)
    : IClassFixture<StartupMaintenanceHostServer>
{
    [Fact]
    public async Task Health_answers_while_maintenance_is_still_running_and_a_fresh_db_gets_seeded()
    {
        var client = server.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        await server.Reached.Task.WaitAsync(TimeSpan.FromSeconds(30));

        var health = await client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);

        await using (var db = server.CreateDb())
            Assert.Equal(0, await db.Tenants.CountAsync());

        server.Release.TrySetResult();

        // ⚠️ الصيانة في الخلفية — بنستنى نتيجتها بحد أقصى.
        var deadline = DateTime.UtcNow.AddSeconds(30);
        int locations = 0;

        while (DateTime.UtcNow < deadline)
        {
            await using var db = server.CreateDb();
            locations = await db.Locations.CountAsync();

            if (locations == 4) break;

            await Task.Delay(100);
        }

        Assert.Equal(4, locations);

        await using (var db = server.CreateDb())
        {
            var tenant = await db.Tenants.SingleAsync();
            Assert.Equal("CODLEK", tenant.Name);

            var owner = await db.Set<ApplicationUser>().SingleAsync();
            Assert.Equal("admin", owner.UserName);
            Assert.True(owner.MustChangePassword);
        }
    }
}
