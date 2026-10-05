using Codlek.Api;
using Codlek.Api.Startup;
using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Maintenance;
using Codlek.Application.Features.Maintenance.ClearOemCodeNames;
using Codlek.Application.Features.Maintenance.GetTenantIds;
using Codlek.Application.Features.Maintenance.HydrateCommercialModels;
using Codlek.Application.Features.Maintenance.RecalculateDuplicateStatus;
using Codlek.Application.Features.Maintenance.ResolveOrphanReports;
using Codlek.Application.Features.Maintenance.SeedFirstRun;
using Codlek.Application.Features.Maintenance.SeedHandoverLocations;
using Codlek.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Codlek.Tests;

/// <summary>
/// خدمة صيانة الإقلاع نفسها — <b>الترتيب، الخلفية، والفشل اللي مابيوقّعش
/// السيرفر</b>. اللفّات نفسها متفحوصة على قاعدة حقيقية في
/// <see cref="StartupMaintenanceSweepTests"/>.
/// </summary>
public class StartupMaintenanceServiceTests
{
    private static readonly Guid A = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid B = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    /// <summary>بيسجّل كل أمر بالترتيب، ويقدر يوقّع أو يفشّل أمر بعينه.</summary>
    private sealed class RecordingSender : ISender
    {
        public readonly List<object> Sent = [];

        public Func<object, Exception?> Throw { get; set; } = _ => null;
        public Func<object, bool> Fail { get; set; } = _ => false;

        /// <summary>بيتفتح لما أول أمر يوصل — عشان نثبت إنه ماوصلش بدري.</summary>
        public readonly TaskCompletionSource FirstSend =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<TResponse> Send<TResponse>(
            IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            lock (Sent) Sent.Add(request);
            FirstSend.TrySetResult();

            if (Throw(request) is { } ex) throw ex;

            object response = request switch
            {
                GetTenantIdsQuery => Result.Success<IReadOnlyList<Guid>>([A, B]),
                SeedFirstRunCommand => Result.Success(new FirstRunSeedResult(false, null)),
                ClearOemCodeNamesCommand => Result.Success(new OemCleanupResult(0, 0)),
                ResolveOrphanReportsCommand => Result.Success(new OrphanSweepResult(0, 0, 0)),
                HydrateCommercialModelsCommand => Result.Success(new HydrationSweepResult(0, 0)),
                _ when Fail(request) => Result.Failure<int>(new Error("x", "فشل للفحص", 500)),
                _ => Result.Success(0),
            };

            return Task.FromResult((TResponse)response);
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest => throw new NotSupportedException();

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(
            object request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeLock(bool free) : IMaintenanceLock
    {
        public int Released;

        public Task<IAsyncDisposable?> TryAcquireAsync(string name, CancellationToken ct = default) =>
            Task.FromResult<IAsyncDisposable?>(free ? new Held(this) : null);

        private sealed class Held(FakeLock owner) : IAsyncDisposable
        {
            public ValueTask DisposeAsync()
            {
                owner.Released++;
                return ValueTask.CompletedTask;
            }
        }
    }

    private sealed class FakeLifetime : IHostApplicationLifetime
    {
        public readonly CancellationTokenSource Started = new();

        public CancellationToken ApplicationStarted => Started.Token;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication() { }
    }

    private static (StartupMaintenanceService Service, RecordingSender Sender, FakeLock Lock, FakeLifetime Lifetime)
        Build(bool enabled = true, bool lockFree = true)
    {
        var sender = new RecordingSender();
        var maintenanceLock = new FakeLock(lockFree);
        var lifetime = new FakeLifetime();

        var provider = new ServiceCollection()
            .AddSingleton<ISender>(sender)
            .AddSingleton<IMaintenanceLock>(maintenanceLock)
            .BuildServiceProvider();

        var service = new StartupMaintenanceService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            lifetime,
            Options.Create(new StartupMaintenanceOptions { Enabled = enabled, StartDelaySeconds = 0 }),
            NullLogger<StartupMaintenanceService>.Instance);

        return (service, sender, maintenanceLock, lifetime);
    }

    private static string Describe(object request) => request switch
    {
        SeedFirstRunCommand => "seed",
        GetTenantIdsQuery => "tenants",
        SeedHandoverLocationsCommand c => "locations:" + Tag(c.TenantId),
        ClearOemCodeNamesCommand c => "oem:" + Tag(c.TenantId),
        ResolveOrphanReportsCommand c => "orphans:" + Tag(c.TenantId),
        RecalculateDuplicateStatusCommand c => "duplicates:" + Tag(c.TenantId),
        HydrateCommercialModelsCommand c => "hydrate:" + Tag(c.TenantId),
        _ => request.GetType().Name,
    };

    private static string Tag(Guid id) => id == A ? "A" : id == B ? "B" : "?";

    private static readonly string[] FullRun =
    [
        "seed", "tenants",
        "locations:A", "locations:B",
        "oem:A", "oem:B",
        "orphans:A", "orphans:B",
        "duplicates:A", "duplicates:B",
        "hydrate:A", "hydrate:B",
    ];

    /// <summary>
    /// 🔴 <b>الإقلاع مابيستناش الصيانة.</b> <c>StartAsync</c> بيرجع والسيرفر
    /// بيرد قبل ما أي أمر يتبعت — الصيانة بتبتدي بعد <c>ApplicationStarted</c>
    /// وبترتيب القديم بالحرف: البذرة، التسليم، التنضيف، الربط، التكرار،
    /// وبعدين الاسم التجاري آخر حاجة.
    /// </summary>
    [Fact]
    public async Task Start_returns_at_once_and_the_sweeps_run_after_startup_in_legacy_order()
    {
        var (service, sender, maintenanceLock, lifetime) = Build();

        await service.StartAsync(CancellationToken.None);

        await Task.Delay(100);
        Assert.Empty(sender.Sent);

        lifetime.Started.Cancel();

        await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(FullRun, sender.Sent.Select(Describe));
        Assert.Equal(1, maintenanceLock.Released);
    }

    /// <summary>
    /// 🔴 <b>خطوة وقعت مابتوقّفش الباقي ولا السيرفر.</b> <c>BackgroundService</c>
    /// بيقفل البرنامج كله لو استثناء طلع منه.
    /// </summary>
    [Fact]
    public async Task A_step_that_throws_or_fails_is_logged_and_the_rest_still_run()
    {
        var (service, sender, maintenanceLock, _) = Build();

        sender.Throw = r => r is ResolveOrphanReportsCommand { TenantId: var t } && t == A
            ? new InvalidOperationException("القاعدة وقعت")
            : null;

        sender.Fail = r => r is RecalculateDuplicateStatusCommand;

        await service.RunAsync(CancellationToken.None);

        Assert.Equal(FullRun, sender.Sent.Select(Describe));
        Assert.Equal(1, maintenanceLock.Released);
    }

    /// <summary>
    /// 🔴 <b>عملية تانية ماسكة القفل = مفيش ولا أمر.</b> ده اللي بيمنع
    /// نسختين من البرنامج يعملوا نفس الشغل في نفس اللحظة.
    /// </summary>
    [Fact]
    public async Task Nothing_runs_while_another_process_holds_the_lock()
    {
        var (service, sender, _, _) = Build(lockFree: false);

        await service.RunAsync(CancellationToken.None);

        Assert.Empty(sender.Sent);
    }

    [Fact]
    public async Task A_disabled_service_sends_nothing_even_after_startup()
    {
        var (service, sender, _, lifetime) = Build(enabled: false);

        await service.StartAsync(CancellationToken.None);
        lifetime.Started.Cancel();

        await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Empty(sender.Sent);
    }

    /// <summary>السيرفر بيقفل قبل ما يقوم خالص — الخدمة بتخرج بهدوء.</summary>
    [Fact]
    public async Task Stopping_before_startup_exits_quietly()
    {
        var (service, sender, _, _) = Build();

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        Assert.True(service.ExecuteTask!.IsCompletedSuccessfully);
        Assert.Empty(sender.Sent);
    }

    /// <summary>
    /// 🔴 <b>الخدمة متسجّلة — ومقفولة في التطوير.</b> فحوص العقد بتقوّم
    /// السيرفر في بيئة التطوير، وبعضها على <c>codlek_dev</c> — والصيانة
    /// بتكتب. وفي الإنتاج مفتوحة من <c>appsettings.json</c>.
    /// </summary>
    [Fact]
    public void The_service_is_registered_on_in_production_and_off_in_development()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CodlekApi.sln")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        string api = Path.Combine(dir!.FullName, "Codlek.Api");

        var production = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(api, "appsettings.json"))
            .AddJsonFile(Path.Combine(api, "appsettings.Production.json"))
            .Build();

        var development = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(api, "appsettings.json"))
            .AddJsonFile(Path.Combine(api, "appsettings.Development.json"))
            .Build();

        Assert.True(Bind(production).Enabled);
        Assert.False(Bind(development).Enabled);

        var settings = new Dictionary<string, string?>
        {
            ["Jwt:Key"] = new string('k', 48),
            ["Jwt:Issuer"] = "codlek",
            ["Jwt:Audience"] = "codlek",
            ["Server:PublicBaseUrl"] = "http://localhost:5097",
        };

        var services = new ServiceCollection()
            .AddLogging()
            .AddApiServices(
                new ConfigurationBuilder().AddInMemoryCollection(settings).Build(),
                isDevelopment: true);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(IHostedService)
            && d.ImplementationType == typeof(StartupMaintenanceService));
    }

    private static StartupMaintenanceOptions Bind(IConfiguration configuration)
    {
        var options = new StartupMaintenanceOptions();
        configuration.GetSection(StartupMaintenanceOptions.Section).Bind(options);
        return options;
    }
}
