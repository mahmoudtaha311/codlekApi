using System.Diagnostics;
using Codlek.Application.Abstractions;
using Codlek.Application.Features.Maintenance.ClearOemCodeNames;
using Codlek.Application.Features.Maintenance.GetTenantIds;
using Codlek.Application.Features.Maintenance.HydrateCommercialModels;
using Codlek.Application.Features.Maintenance.RecalculateDuplicateStatus;
using Codlek.Application.Features.Maintenance.ResolveOrphanReports;
using Codlek.Application.Features.Maintenance.SeedFirstRun;
using Codlek.Application.Features.Maintenance.SeedHandoverLocations;
using Codlek.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Options;

namespace Codlek.Api.Startup;

/// <summary>
/// صيانة الإقلاع — اللي القديم كان بيعمله مع <b>كل</b> تشغيل
/// (<c>CodlekWeb/Program.cs:250-346</c>): كل نشر، كل إعادة تدوير، وكل
/// صحيان بعد ما الاستضافة تنيّم البرنامج. فعملياً كانت دورية.
///
/// <para><b>بالترتيب ده — نفس ترتيب القديم:</b></para>
/// <list type="number">
/// <item>بذرة أول تشغيل (شركة + <c>admin</c>) — على قاعدة مافيهاش شركة بس.</item>
/// <item>جهات التسليم الأربعة الناقصة.</item>
/// <item>مسح الأسامي التجارية اللي أصلها كود مصنّع.</item>
/// <item>ربط الفحوص اليتيمة بأجهزتها.</item>
/// <item>إعادة حساب «مشكوك إنه مكرر».</item>
/// <item>ملء الاسم التجاري على كل الأجهزة — <b>آخر واحدة عن قصد</b>:
/// الأجهزة اللي كسبت فحوص من الربط في نفس التشغيلة بتاخد اسمها
/// على طول، والتنضيف اللي قبلها مايتلغيش.</item>
/// </list>
///
/// <para>🔴 <b>في الخلفية — مش قبل ما السيرفر يرد.</b> القديم كان بيعملها
/// قبل ما يبتدي يرد، يعني الراكة اللي بتخبط على <c>/api/health</c> بتستنى
/// لفّة على كل الفحوص اليتيمة. هنا بتبتدي بعد <c>ApplicationStarted</c>.</para>
///
/// <para>🔴 <b>والفشل مابيوقّعش السيرفر.</b> <c>BackgroundService</c>
/// بيوقّف البرنامج كله لو استثناء طلع منه. كل خطوة ليها نطاق لوحدها
/// ومحاوطة: خطوة وقعت بتتسجّل والباقي بيكمّل — القديم كان بيوقّف الإقلاع،
/// وده هنا كان معناه سيرفر واقع عشان اسم تجاري.</para>
///
/// <para>⚠️ <b>وآمنة على كل تشغيل.</b> كل خطوة بتكتب الفرق بس — التشغيلة
/// التانية على نفس البيانات بتطلع أصفار. وقفل على القاعدة بيمنع عمليتين
/// يعملوا نفس الشغل في نفس اللحظة (<see cref="IMaintenanceLock"/>).</para>
/// </summary>
public sealed class StartupMaintenanceService(
    IServiceScopeFactory scopes,
    IHostApplicationLifetime lifetime,
    IOptions<StartupMaintenanceOptions> options,
    ILogger<StartupMaintenanceService> log) : BackgroundService
{
    /// <summary>اسم القفل على القاعدة.</summary>
    public const string LockName = "codlek:startup-maintenance";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            log.LogInformation("صيانة الإقلاع مقفولة من الإعدادات — مااشتغلتش");
            return;
        }

        try
        {
            await WhenStartedAsync(stoppingToken);

            if (options.Value.StartDelaySeconds > 0)
                await Task.Delay(TimeSpan.FromSeconds(options.Value.StartDelaySeconds), stoppingToken);

            await RunAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // ⚠️ السيرفر بيقفل — اللي خلص اتحفظ دفعة دفعة، والتشغيلة
            //    الجاية بتكمّل.
            log.LogInformation("صيانة الإقلاع وقفت عشان السيرفر بيقفل");
        }
        catch (Exception ex)
        {
            log.LogError(ex, "صيانة الإقلاع وقعت");
        }
    }

    /// <summary>
    /// اللفّة كلها مرة واحدة.
    ///
    /// <para>⚠️ <b>عامة عشان الفحوص</b> — الإقلاع نفسه بيعدّي من
    /// <see cref="ExecuteAsync"/>.</para>
    /// </summary>
    public async Task RunAsync(CancellationToken ct)
    {
        var clock = Stopwatch.StartNew();

        await using var lockScope = scopes.CreateAsyncScope();

        var held = await lockScope.ServiceProvider
            .GetRequiredService<IMaintenanceLock>()
            .TryAcquireAsync(LockName, ct);

        if (held is null)
        {
            log.LogInformation("صيانة الإقلاع شغّالة في عملية تانية — العملية دي مش هتكرّرها");
            return;
        }

        await using (held)
        {
            log.LogInformation("صيانة الإقلاع ابتدت");

            await StepAsync("بذرة أول تشغيل", null, new SeedFirstRunCommand(), ct);

            var tenants = await TenantsAsync(ct);

            foreach (var tenant in tenants)
                await StepAsync("جهات التسليم", tenant, new SeedHandoverLocationsCommand(tenant), ct);

            foreach (var tenant in tenants)
                await StepAsync("أسامي أكواد المصنّع", tenant, new ClearOemCodeNamesCommand(tenant), ct);

            foreach (var tenant in tenants)
                await StepAsync("ربط الفحوص اليتيمة", tenant, new ResolveOrphanReportsCommand(tenant), ct);

            foreach (var tenant in tenants)
                await StepAsync("مشكوك إنه مكرر", tenant, new RecalculateDuplicateStatusCommand(tenant), ct);

            foreach (var tenant in tenants)
                await StepAsync("الاسم التجاري", tenant, new HydrateCommercialModelsCommand(tenant), ct);

            log.LogInformation(
                "صيانة الإقلاع خلصت في {Seconds:0.0} ثانية على {Tenants} شركة",
                clock.Elapsed.TotalSeconds, tenants.Count);
        }
    }

    private async Task WhenStartedAsync(CancellationToken ct)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // ⚠️ لو السيرفر قام خلاص، التسجيل بينده على طول.
        await using var onStarted = lifetime.ApplicationStarted.Register(() => started.TrySetResult());
        await using var onStopping = ct.Register(() => started.TrySetCanceled(ct));

        await started.Task;
    }

    private async Task<IReadOnlyList<Guid>> TenantsAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();

        var result = await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new GetTenantIdsQuery(), ct);

        return result.IsSuccess ? result.Value : [];
    }

    /// <summary>
    /// خطوة واحدة — <b>بنطاق لوحدها</b>.
    ///
    /// <para>⚠️ نطاق جديد = <c>DbContext</c> جديد. القديم اتعلّم ده بالطريقة
    /// الصعبة: مشاركة السياق بين الخطوات خلّت حفظ خطوة يدفع كيانات متتبّعة
    /// من خطوة قبلها، وطلّع أخطاء مفتاح مكرر مالهاش علاقة بيها.</para>
    /// </summary>
    private async Task StepAsync<T>(
        string name, Guid? tenant, IRequest<Result<T>> request, CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();

            var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(request, ct);

            if (result.IsFailure)
            {
                log.LogWarning(
                    "صيانة الإقلاع: خطوة «{Step}» للشركة {Tenant} رجعت فشل: {Reason}",
                    name, tenant, result.Error.Description);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            log.LogError(
                ex, "صيانة الإقلاع: خطوة «{Step}» للشركة {Tenant} وقعت — الباقي هيكمّل",
                name, tenant);
        }
    }
}
