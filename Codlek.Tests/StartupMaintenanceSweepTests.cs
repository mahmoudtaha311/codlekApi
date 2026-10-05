using System.Text.Json;
using Codlek.Application;
using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Sync;
using Codlek.Application.Features.Maintenance.ClearOemCodeNames;
using Codlek.Application.Features.Maintenance.GetTenantIds;
using Codlek.Application.Features.Maintenance.HydrateCommercialModels;
using Codlek.Application.Features.Maintenance.RecalculateDuplicateStatus;
using Codlek.Application.Features.Maintenance.ResolveOrphanReports;
using Codlek.Application.Features.Maintenance.SeedHandoverLocations;
using Codlek.Application.Features.Rack.IngestReports;
using Codlek.Application.Interfaces;
using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Codlek.Core.Text;
using Codlek.Infrastructure;
using Codlek.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Codlek.Tests;

/// <summary>قاعدة فحوص لفّات صيانة الإقلاع.</summary>
public sealed class StartupMaintenanceDbFixture : SqlServerDbFixture
{
    protected override string DatabaseName => "codlek_startup_maintenance_test";
}

/// <summary>
/// لفّات صيانة الإقلاع — <b>على قاعدة حقيقية، ومن خلال <c>ISender</c></b>
/// بنفس توصيل الإنتاج (المتحقّقات والمستودعات والتحديث المباشر).
///
/// <para>⚠️ <b>كل فحص ليه شركة لوحده.</b> اللفّات كلها بالشركة، فالفحوص
/// بتشارك القاعدة من غير ما تأثّر في بعض — وده نفسه بيثبت عزل الشركات:
/// لو لفّة سرّبت لشركة تانية، فحوص تانية بتقع.</para>
///
/// <para>⚠️ والقراية بعد كل لفّة من <b>سياق جديد</b>: التحديث المباشر
/// مابيعدّيش على المتتبّع، فالكيان اللي في الذاكرة بيكدب.</para>
/// </summary>
public class StartupMaintenanceSweepTests(StartupMaintenanceDbFixture fixture)
    : IClassFixture<StartupMaintenanceDbFixture>
{
    private const string Trusted = "SMBIOS (Product Version)";

    private ServiceProvider Services()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{SqlServerConnection.Name}"] = fixture.ConnectionString,
                ["Jwt:Key"] = new string('k', 48),
            })
            .Build();

        return new ServiceCollection()
            .AddLogging()
            .AddApplicationServices()
            .AddInfrastructureServices(configuration)
            .BuildServiceProvider();
    }

    private async Task<T> Send<T>(IRequest<Result<T>> request)
    {
        await using var services = Services();
        using var scope = services.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : "");
        return result.Value;
    }

    // =================================================================
    //  الزرع
    // =================================================================

    private async Task<Guid> TenantAsync()
    {
        var tenant = new Tenant { Name = "ورشة " + Guid.NewGuid().ToString("N")[..6] };

        await using var db = fixture.Create();
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        return tenant.Id;
    }

    private async Task<Guid> DeviceAsync(
        Guid tenantId,
        DeviceLifecycleStatus status = DeviceLifecycleStatus.Active,
        params (DeviceIdentifierKind Kind, string Value, bool Active)[] anchors)
    {
        var device = new Device
        {
            TenantId = tenantId,
            PublicCode = "LP-" + Guid.NewGuid().ToString("N")[..8],
            Status = status,
            LastKnownManufacturer = "LENOVO",
            LastKnownModel = "81FK",
        };

        foreach (var (kind, value, active) in anchors)
        {
            device.Identifiers.Add(new DeviceIdentifierRow
            {
                TenantId = tenantId,
                Kind = kind,
                RawValue = value,
                NormalizedValue = IdentityValues.Normalize(value),
                IsActive = active,
            });
        }

        await using var db = fixture.Create();
        db.Devices.Add(device);
        await db.SaveChangesAsync();

        return device.Id;
    }

    private static string Raw(
        string uuid = "", string bios = "", string board = "", params string[] disks)
    {
        var dto = new LaptopReportPayload
        {
            Id = Guid.NewGuid(),
            Specs = new DeviceSpecsPayload
            {
                SystemUuid = uuid,
                SerialNumber = bios,
                BoardSerial = board,
                InternalDisks = [.. disks.Select(d => new DiskInfoPayload { SerialNumber = d })],
            },
        };

        return JsonSerializer.Serialize(dto, IngestReportsCommandHandler.Json);
    }

    private async Task<Guid> ReportAsync(
        Guid tenantId,
        string raw = "",
        Guid? deviceId = null,
        bool needsResolution = false,
        bool deleted = false,
        string? name = null,
        string? source = null,
        string? machineType = null,
        DateTime? startedAtUtc = null)
    {
        var report = new Report
        {
            TenantId = tenantId,
            RawJson = raw,
            DeviceId = deviceId,
            NeedsDeviceResolution = needsResolution,
            IsDeleted = deleted,
            CommercialModelName = name,
            CommercialModelSource = source,
            MachineType = machineType,
            StartedAtUtc = startedAtUtc ?? new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc),
            ReceivedAtUtc = new DateTime(2026, 10, 1, 9, 5, 0, DateTimeKind.Utc),
            SearchText = "-",
        };

        await using var db = fixture.Create();
        db.Reports.Add(report);
        await db.SaveChangesAsync();

        return report.Id;
    }

    private async Task<Report> StoredReportAsync(Guid id)
    {
        await using var db = fixture.Create();
        return await db.Reports.AsNoTracking().SingleAsync(r => r.Id == id);
    }

    private async Task<Device> StoredDeviceAsync(Guid id)
    {
        await using var db = fixture.Create();
        return await db.Devices.AsNoTracking().SingleAsync(d => d.Id == id);
    }

    // =================================================================
    //  M1 — ربط الفحوص اليتيمة
    // =================================================================

    /// <summary>
    /// 🔴 <b>الفحص اللي وصل قبل جهازه بيتربط بيه في التشغيلة الجاية.</b>
    /// من غير ده بيفضل يتيم للأبد وتنبيه «فحوص من غير جهاز» مابينزلش.
    /// </summary>
    [Fact]
    public async Task An_orphan_scan_is_linked_to_the_device_its_uuid_points_at()
    {
        var tenant = await TenantAsync();
        var device = await DeviceAsync(tenant, anchors: (DeviceIdentifierKind.SystemUuid, "4C4C4544-0042-3510-8051-B4C04F4E4D32", true));
        var report = await ReportAsync(tenant, Raw(uuid: "4c4c4544-0042-3510-8051-b4c04f4e4d32"), needsResolution: true);

        var result = await Send(new ResolveOrphanReportsCommand(tenant));

        Assert.Equal((1, 1, 0), (result.Scanned, result.Linked, result.Flagged));

        var stored = await StoredReportAsync(report);
        Assert.Equal(device, stored.DeviceId);
        Assert.False(stored.NeedsDeviceResolution);
    }

    /// <summary>الأقراص آخر مرساة — بس لسه بتربط لما الباقي فاضي.</summary>
    [Fact]
    public async Task A_disk_serial_links_when_the_stronger_anchors_are_placeholders()
    {
        var tenant = await TenantAsync();
        var device = await DeviceAsync(tenant, anchors: (DeviceIdentifierKind.DiskSerial, "WD-WX11A12B3456", true));
        var report = await ReportAsync(tenant, Raw("To Be Filled By O.E.M.", "Default string", "", "", "WD-WX11A12B3456"));

        await Send(new ResolveOrphanReportsCommand(tenant));

        Assert.Equal(device, (await StoredReportAsync(report)).DeviceId);
    }

    /// <summary>
    /// 🔴 <b>نفس ترتيب القوة اللي على الراكة.</b> الـUUID بيكسب القرص
    /// حتى لو القرص بيشاور على جهاز تاني — القرص بيتنقل بين اللابات.
    /// </summary>
    [Fact]
    public async Task The_stronger_anchor_wins_over_a_disk_that_points_elsewhere()
    {
        var tenant = await TenantAsync();
        var byUuid = await DeviceAsync(tenant, anchors: (DeviceIdentifierKind.SystemUuid, "UUID-STRONG-0001", true));
        await DeviceAsync(tenant, anchors: (DeviceIdentifierKind.DiskSerial, "DISK-MOVED-0001", true));

        var report = await ReportAsync(tenant, Raw("UUID-STRONG-0001", "", "", "DISK-MOVED-0001"));

        await Send(new ResolveOrphanReportsCommand(tenant));

        Assert.Equal(byUuid, (await StoredReportAsync(report)).DeviceId);
    }

    /// <summary>
    /// 🔴 <b>مرساة على جهازين = مراجعة، مش اختيار عشوائي.</b>
    /// </summary>
    [Fact]
    public async Task An_anchor_shared_by_two_devices_flags_the_scan_instead_of_guessing()
    {
        var tenant = await TenantAsync();
        await DeviceAsync(tenant, anchors: (DeviceIdentifierKind.BiosSerial, "5CG1234XYZ", true));
        await DeviceAsync(tenant, anchors: (DeviceIdentifierKind.BiosSerial, "5CG1234XYZ", true));

        var report = await ReportAsync(tenant, Raw(bios: "5CG1234XYZ"));

        var result = await Send(new ResolveOrphanReportsCommand(tenant));

        Assert.Equal((1, 0, 1), (result.Scanned, result.Linked, result.Flagged));

        var stored = await StoredReportAsync(report);
        Assert.Null(stored.DeviceId);
        Assert.True(stored.NeedsDeviceResolution);
    }

    /// <summary>نسخة خام فاضية أو مكسورة أو من غير مواصفات = مراجعة، واللفة ماتقفش.</summary>
    [Fact]
    public async Task Missing_or_broken_raw_json_flags_the_scan_and_the_sweep_goes_on()
    {
        var tenant = await TenantAsync();
        var device = await DeviceAsync(tenant, anchors: (DeviceIdentifierKind.SystemUuid, "UUID-AFTER-BROKEN", true));

        var empty = await ReportAsync(tenant, "");
        var broken = await ReportAsync(tenant, "{ this is not json");
        var noSpecs = await ReportAsync(tenant, "{\"id\":\"" + Guid.NewGuid() + "\",\"specs\":null}");
        var nullDisks = await ReportAsync(tenant, "{\"specs\":{\"systemUuid\":\"UUID-AFTER-BROKEN\",\"internalDisks\":null}}");

        var result = await Send(new ResolveOrphanReportsCommand(tenant));

        Assert.Equal((4, 1, 3), (result.Scanned, result.Linked, result.Flagged));

        Assert.True((await StoredReportAsync(empty)).NeedsDeviceResolution);
        Assert.True((await StoredReportAsync(broken)).NeedsDeviceResolution);
        Assert.True((await StoredReportAsync(noSpecs)).NeedsDeviceResolution);
        Assert.Equal(device, (await StoredReportAsync(nullDisks)).DeviceId);
    }

    /// <summary>
    /// 🔴 <b>النسخ الخام اللي على الإنتاج مكتوبة بشكل القديم</b> — <c>"Specs"</c>
    /// بحرف كبير — وخانة مالهاش علاقة بالهوية بنوع غريب مابتمنعش الربط.
    /// </summary>
    [Fact]
    public async Task A_legacy_shaped_raw_json_links_even_when_unrelated_fields_do_not_fit()
    {
        var tenant = await TenantAsync();
        var device = await DeviceAsync(tenant, anchors: (DeviceIdentifierKind.DiskSerial, "WD-WX52E603Y0VT", true));

        const string legacy =
            "{\"Id\":\"9b4992d0-2be9-4fe2-80da-53c772396e21\",\"StartedAtUtc\":\"not a date\"," +
            "\"Steps\":\"not an array\",\"Specs\":{\"Manufacturer\":\"LENOVO\",\"SerialNumber\":\"Default string\"," +
            "\"BoardSerial\":\"\",\"SystemUuid\":null,\"CpuCores\":\"six\"," +
            "\"InternalDisks\":[{\"Model\":\"WDC\",\"SerialNumber\":\"WD-WX52E603Y0VT\",\"SizeBytes\":1}]}}";

        var report = await ReportAsync(tenant, legacy);

        await Send(new ResolveOrphanReportsCommand(tenant));

        Assert.Equal(device, (await StoredReportAsync(report)).DeviceId);
    }

    /// <summary>🔴 <b>جهاز شركة تانية بنفس المرساة مايتلمسش.</b></summary>
    [Fact]
    public async Task A_device_in_another_tenant_is_never_matched()
    {
        var tenant = await TenantAsync();
        var other = await TenantAsync();

        await DeviceAsync(other, anchors: (DeviceIdentifierKind.SystemUuid, "UUID-CROSS-TENANT", true));
        var report = await ReportAsync(tenant, Raw("UUID-CROSS-TENANT"));

        var result = await Send(new ResolveOrphanReportsCommand(tenant));

        Assert.Equal(0, result.Linked);
        Assert.Null((await StoredReportAsync(report)).DeviceId);
    }

    /// <summary>
    /// 🔴 <b>ولفّة شركة مابتلمسش فحوص شركة تانية.</b> اليتيم بتاع
    /// الشركة التانية بيفضل زي ما هو — مش متعلّم ومش مربوط.
    /// </summary>
    [Fact]
    public async Task The_sweep_never_touches_another_tenants_orphans()
    {
        var tenant = await TenantAsync();
        var other = await TenantAsync();

        await DeviceAsync(other, anchors: (DeviceIdentifierKind.SystemUuid, "UUID-OTHER-ORPHAN", true));
        var theirs = await ReportAsync(other, Raw("UUID-OTHER-ORPHAN"), needsResolution: false);

        var result = await Send(new ResolveOrphanReportsCommand(tenant));

        Assert.Equal(0, result.Scanned);

        var stored = await StoredReportAsync(theirs);
        Assert.Null(stored.DeviceId);
        Assert.False(stored.NeedsDeviceResolution);
    }

    /// <summary>مرساة موقوفة مش دليل.</summary>
    [Fact]
    public async Task An_inactive_anchor_does_not_match()
    {
        var tenant = await TenantAsync();
        await DeviceAsync(tenant, anchors: (DeviceIdentifierKind.SystemUuid, "UUID-RETIRED-ANCHOR", false));

        var report = await ReportAsync(tenant, Raw("UUID-RETIRED-ANCHOR"));

        await Send(new ResolveOrphanReportsCommand(tenant));

        Assert.Null((await StoredReportAsync(report)).DeviceId);
    }

    /// <summary>
    /// 🔴 <b>ربط بس — مفيش إنشاء أجهزة خالص.</b> والفحص المربوط أصلاً
    /// مابيتلمسش حتى لو مراسيه بتشاور على جهاز تاني.
    /// </summary>
    [Fact]
    public async Task No_device_is_ever_created_and_linked_scans_are_left_alone()
    {
        var tenant = await TenantAsync();
        var owner = await DeviceAsync(tenant);
        var other = await DeviceAsync(tenant, anchors: (DeviceIdentifierKind.SystemUuid, "UUID-ALREADY-LINKED", true));

        var linked = await ReportAsync(tenant, Raw("UUID-ALREADY-LINKED"), deviceId: owner);
        await ReportAsync(tenant, Raw("UUID-NOBODY-HAS-THIS"));

        int before;
        await using (var db = fixture.Create())
            before = await db.Devices.CountAsync(d => d.TenantId == tenant);

        var result = await Send(new ResolveOrphanReportsCommand(tenant));

        Assert.Equal(1, result.Scanned);
        Assert.Equal(owner, (await StoredReportAsync(linked)).DeviceId);
        Assert.NotEqual(other, owner);

        await using (var db = fixture.Create())
            Assert.Equal(before, await db.Devices.CountAsync(d => d.TenantId == tenant));
    }

    /// <summary>
    /// 🔴 <b>المتعلّم بيتعاد تقييمه.</b> الغموض اللي منع المطابقة بيتحل
    /// بعدين (التوأم اتوقفت مرساته) — والفحص لازم يلتحق ساعتها.
    /// </summary>
    [Fact]
    public async Task A_flagged_scan_is_re_evaluated_once_the_ambiguity_is_gone()
    {
        var tenant = await TenantAsync();
        var real = await DeviceAsync(tenant, anchors: (DeviceIdentifierKind.BiosSerial, "SN-AMBIGUOUS-1", true));
        var twin = await DeviceAsync(tenant, anchors: (DeviceIdentifierKind.BiosSerial, "SN-AMBIGUOUS-1", true));

        var report = await ReportAsync(tenant, Raw(bios: "SN-AMBIGUOUS-1"));

        await Send(new ResolveOrphanReportsCommand(tenant));
        Assert.True((await StoredReportAsync(report)).NeedsDeviceResolution);

        await using (var db = fixture.Create())
        {
            await db.DeviceIdentifiers
                .Where(i => i.DeviceId == twin)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.IsActive, false));
        }

        var second = await Send(new ResolveOrphanReportsCommand(tenant));

        Assert.Equal(1, second.Linked);
        Assert.Equal(real, (await StoredReportAsync(report)).DeviceId);
    }

    /// <summary>الممسوح كمان — القديم فلتره «مفيش جهاز» وبس.</summary>
    [Fact]
    public async Task A_deleted_orphan_is_linked_too_like_legacy()
    {
        var tenant = await TenantAsync();
        var device = await DeviceAsync(tenant, anchors: (DeviceIdentifierKind.BoardSerial, "BOARD-DELETED-SCAN", true));
        var report = await ReportAsync(tenant, Raw(board: "BOARD-DELETED-SCAN"), deleted: true);

        await Send(new ResolveOrphanReportsCommand(tenant));

        Assert.Equal(device, (await StoredReportAsync(report)).DeviceId);
    }

    /// <summary>
    /// ⚠️ <b>الدفعات بالمفتاح بتخلص وبتلف على الكل</b> — حتى لما كل صف في
    /// الدفعة يفضل من غير جهاز (فلتر «مفيش جهاز» لوحده كان هيلف للأبد).
    /// والتشغيلة التانية مابتربطش حاجة تاني.
    /// </summary>
    [Fact]
    public async Task Keyset_batches_cover_every_orphan_and_a_second_run_changes_nothing()
    {
        var tenant = await TenantAsync();
        var device = await DeviceAsync(tenant, anchors: (DeviceIdentifierKind.SystemUuid, "UUID-BATCHED", true));

        var ids = new List<Guid>();
        for (int i = 0; i < 3; i++) ids.Add(await ReportAsync(tenant, Raw("UUID-BATCHED")));
        for (int i = 0; i < 4; i++) ids.Add(await ReportAsync(tenant, Raw("UUID-NO-MATCH-" + i)));

        var first = await Send(new ResolveOrphanReportsCommand(tenant, BatchSize: 2));

        Assert.Equal((7, 3, 4), (first.Scanned, first.Linked, first.Flagged));

        var second = await Send(new ResolveOrphanReportsCommand(tenant, BatchSize: 2));

        Assert.Equal((4, 0, 4), (second.Scanned, second.Linked, second.Flagged));

        await using var db = fixture.Create();
        Assert.Equal(3, await db.Reports.CountAsync(r => r.TenantId == tenant && r.DeviceId == device));
    }

    /// <summary>🔴 <b>حجم دفعة صفر بيترفض</b> — كانت هتخلص من أول لفة من غير ما تعمل حاجة.</summary>
    [Fact]
    public async Task A_zero_batch_size_is_rejected()
    {
        await using var services = Services();
        using var scope = services.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new ResolveOrphanReportsCommand(Guid.NewGuid(), BatchSize: 0));

        Assert.True(result.IsFailure);
        Assert.IsType<ValidationError>(result.Error);
    }

    // =================================================================
    //  M2 — «مشكوك إنه مكرر»
    // =================================================================

    /// <summary>
    /// 🔴 <b>العلم القديم بيتشال لما سببه يروح.</b> من غير ده الجرس بيكبر
    /// وبس.
    /// </summary>
    [Fact]
    public async Task A_stale_duplicate_flag_is_cleared()
    {
        var tenant = await TenantAsync();
        var lonely = await DeviceAsync(tenant, DeviceLifecycleStatus.DuplicateSuspected,
            (DeviceIdentifierKind.BiosSerial, "SN-LONELY", true));

        int changed = await Send(new RecalculateDuplicateStatusCommand(tenant));

        Assert.Equal(1, changed);
        Assert.Equal(DeviceLifecycleStatus.Active, (await StoredDeviceAsync(lonely)).Status);
    }

    /// <summary>تكرار حقيقي لسه قايم = الطرفين متعلّمين.</summary>
    [Fact]
    public async Task Real_twins_are_both_flagged()
    {
        var tenant = await TenantAsync();
        var a = await DeviceAsync(tenant, anchors: (DeviceIdentifierKind.BoardSerial, "BOARD-TWIN", true));
        var b = await DeviceAsync(tenant, anchors: (DeviceIdentifierKind.BoardSerial, "BOARD-TWIN", true));
        var bystander = await DeviceAsync(tenant, anchors: (DeviceIdentifierKind.BoardSerial, "BOARD-ALONE", true));

        Assert.Equal(2, await Send(new RecalculateDuplicateStatusCommand(tenant)));

        Assert.Equal(DeviceLifecycleStatus.DuplicateSuspected, (await StoredDeviceAsync(a)).Status);
        Assert.Equal(DeviceLifecycleStatus.DuplicateSuspected, (await StoredDeviceAsync(b)).Status);
        Assert.Equal(DeviceLifecycleStatus.Active, (await StoredDeviceAsync(bystander)).Status);
    }

    /// <summary>
    /// 🔴 <b>المدموج برّه الحساب.</b> مراسيه بتفضل نشطة عن قصد — ومن غير
    /// الاستبعاد الكانوني بيتعلّم في كل إقلاع. والمدموج نفسه مابيتلمسش.
    /// </summary>
    [Fact]
    public async Task A_merged_device_neither_flags_its_canonical_nor_changes_itself()
    {
        var tenant = await TenantAsync();
        var canonical = await DeviceAsync(tenant, DeviceLifecycleStatus.DuplicateSuspected,
            (DeviceIdentifierKind.SystemUuid, "UUID-MERGED-PAIR", true));
        var tombstone = await DeviceAsync(tenant, DeviceLifecycleStatus.Merged,
            (DeviceIdentifierKind.SystemUuid, "UUID-MERGED-PAIR", true));

        await Send(new RecalculateDuplicateStatusCommand(tenant));

        Assert.Equal(DeviceLifecycleStatus.Active, (await StoredDeviceAsync(canonical)).Status);
        Assert.Equal(DeviceLifecycleStatus.Merged, (await StoredDeviceAsync(tombstone)).Status);
    }

    /// <summary>
    /// 🔴 <b>المتقاعد قرار بني آدم — مابيتلمسش.</b> بس مراسيه لسه بتتحسب
    /// زي القديم (القديم بيستبعد المدموج بس)، فتوأمه الشغّال بيتعلّم.
    /// </summary>
    [Fact]
    public async Task A_retired_device_is_never_moved_but_still_counts_as_a_twin()
    {
        var tenant = await TenantAsync();
        var retired = await DeviceAsync(tenant, DeviceLifecycleStatus.Retired,
            (DeviceIdentifierKind.BiosSerial, "SN-RETIRED-TWIN", true));
        var active = await DeviceAsync(tenant, anchors: (DeviceIdentifierKind.BiosSerial, "SN-RETIRED-TWIN", true));

        await Send(new RecalculateDuplicateStatusCommand(tenant));

        Assert.Equal(DeviceLifecycleStatus.Retired, (await StoredDeviceAsync(retired)).Status);
        Assert.Equal(DeviceLifecycleStatus.DuplicateSuspected, (await StoredDeviceAsync(active)).Status);
    }

    /// <summary>مرساة موقوفة مش تكرار.</summary>
    [Fact]
    public async Task An_inactive_shared_anchor_is_not_a_duplicate()
    {
        var tenant = await TenantAsync();
        var a = await DeviceAsync(tenant, DeviceLifecycleStatus.DuplicateSuspected,
            (DeviceIdentifierKind.DiskSerial, "DISK-SWAPPED-OUT", true));
        await DeviceAsync(tenant, anchors: (DeviceIdentifierKind.DiskSerial, "DISK-SWAPPED-OUT", false));

        await Send(new RecalculateDuplicateStatusCommand(tenant));

        Assert.Equal(DeviceLifecycleStatus.Active, (await StoredDeviceAsync(a)).Status);
    }

    /// <summary>
    /// ⚠️ <b>القيمة المشتركة بتتحسب بالنوع، والمشكوك فيه بالقيمة بس</b> —
    /// القديم بالحرف. قيمة على نوعين مختلفين لجهازين مش تكرار لوحدها؛
    /// بس لو بقت مشتركة، أي جهاز شايلها بأي نوع بيتعلّم.
    /// </summary>
    [Fact]
    public async Task Shared_values_group_by_kind_but_suspects_match_by_value_like_legacy()
    {
        var tenant = await TenantAsync();

        var crossKindA = await DeviceAsync(tenant, anchors: (DeviceIdentifierKind.BiosSerial, "VALUE-CROSS-KIND", true));
        var crossKindB = await DeviceAsync(tenant, anchors: (DeviceIdentifierKind.BoardSerial, "VALUE-CROSS-KIND", true));

        await Send(new RecalculateDuplicateStatusCommand(tenant));

        Assert.Equal(DeviceLifecycleStatus.Active, (await StoredDeviceAsync(crossKindA)).Status);
        Assert.Equal(DeviceLifecycleStatus.Active, (await StoredDeviceAsync(crossKindB)).Status);

        // تالت بنفس النوع بتاع A ← القيمة بقت مشتركة، والتلاتة بيتعلّموا.
        var third = await DeviceAsync(tenant, anchors: (DeviceIdentifierKind.BiosSerial, "VALUE-CROSS-KIND", true));

        await Send(new RecalculateDuplicateStatusCommand(tenant));

        Assert.Equal(DeviceLifecycleStatus.DuplicateSuspected, (await StoredDeviceAsync(crossKindA)).Status);
        Assert.Equal(DeviceLifecycleStatus.DuplicateSuspected, (await StoredDeviceAsync(crossKindB)).Status);
        Assert.Equal(DeviceLifecycleStatus.DuplicateSuspected, (await StoredDeviceAsync(third)).Status);
    }

    /// <summary>🔴 <b>نفس السيريال في شركة تانية مش تكرار.</b></summary>
    [Fact]
    public async Task The_same_serial_in_another_tenant_is_not_a_duplicate()
    {
        var tenant = await TenantAsync();
        var other = await TenantAsync();

        var mine = await DeviceAsync(tenant, DeviceLifecycleStatus.DuplicateSuspected,
            (DeviceIdentifierKind.BiosSerial, "SN-TWO-TENANTS", true));
        var theirs = await DeviceAsync(other, DeviceLifecycleStatus.DuplicateSuspected,
            (DeviceIdentifierKind.BiosSerial, "SN-TWO-TENANTS", true));

        await Send(new RecalculateDuplicateStatusCommand(tenant));

        Assert.Equal(DeviceLifecycleStatus.Active, (await StoredDeviceAsync(mine)).Status);

        // ⚠️ ولفّة الشركة دي مالمستش جهاز الشركة التانية.
        Assert.Equal(DeviceLifecycleStatus.DuplicateSuspected, (await StoredDeviceAsync(theirs)).Status);
    }

    /// <summary>دفعات صغيرة بتلف على الكل، والتشغيلة التانية صفر.</summary>
    [Fact]
    public async Task Duplicate_recompute_is_batched_and_idempotent()
    {
        var tenant = await TenantAsync();

        for (int i = 0; i < 3; i++)
            await DeviceAsync(tenant, DeviceLifecycleStatus.DuplicateSuspected,
                (DeviceIdentifierKind.BiosSerial, "SN-STALE-" + i, true));

        Assert.Equal(3, await Send(new RecalculateDuplicateStatusCommand(tenant, BatchSize: 1)));
        Assert.Equal(0, await Send(new RecalculateDuplicateStatusCommand(tenant, BatchSize: 1)));
    }

    // =================================================================
    //  أسامي أكواد المصنّع
    // =================================================================

    /// <summary>
    /// 🔴 <b><c>103C_5336AN HP EliteBook</c> بيتمسح من الجهاز والفحص.</b>
    /// </summary>
    [Fact]
    public async Task An_oem_code_name_from_system_family_is_cleared_on_devices_and_scans()
    {
        var tenant = await TenantAsync();
        var device = await DeviceAsync(tenant);

        await using (var db = fixture.Create())
        {
            await db.Devices.Where(d => d.Id == device).ExecuteUpdateAsync(s => s
                .SetProperty(d => d.CommercialModelName, "103C_5336AN HP EliteBook")
                .SetProperty(d => d.CommercialModelSource, "SystemFamily"));
        }

        var report = await ReportAsync(tenant, name: "103C_5336AN HP EliteBook", source: "SystemFamily");

        var result = await Send(new ClearOemCodeNamesCommand(tenant));

        Assert.Equal((1, 1), (result.Devices, result.Reports));

        var storedDevice = await StoredDeviceAsync(device);
        Assert.Null(storedDevice.CommercialModelName);
        Assert.Null(storedDevice.CommercialModelSource);

        var storedReport = await StoredReportAsync(report);
        Assert.Null(storedReport.CommercialModelName);
        Assert.Null(storedReport.CommercialModelSource);

        var again = await Send(new ClearOemCodeNamesCommand(tenant));
        Assert.Equal(0, again.Total);
    }

    /// <summary>
    /// ⚠️ <b>الشرط ضيّق بقصد — زي القديم بالحرف.</b> اسم لينوفو الحقيقي،
    /// ومصدر تاني، وكلمة واحدة كلها كود (القديم بيشترط مسافة) — مايتلمسوش.
    /// </summary>
    [Fact]
    public async Task Only_system_family_names_that_start_with_an_oem_code_and_a_space_are_cleared()
    {
        var tenant = await TenantAsync();

        var lenovo = await ReportAsync(tenant, name: "ThinkPad T480", source: "SystemFamily");
        var otherSource = await ReportAsync(tenant, name: "103C_5336AN HP EliteBook", source: Trusted);
        var singleToken = await ReportAsync(tenant, name: "103C_5336AN", source: "SystemFamily");
        var g8 = await ReportAsync(tenant, name: "EliteBook 840 G8", source: "SystemFamily");
        var doomed = await ReportAsync(tenant, name: "  8470_ABC1 HP ProBook", source: "SystemFamily");

        var result = await Send(new ClearOemCodeNamesCommand(tenant, BatchSize: 2));

        Assert.Equal(1, result.Reports);
        Assert.Equal("ThinkPad T480", (await StoredReportAsync(lenovo)).CommercialModelName);
        Assert.Equal("103C_5336AN HP EliteBook", (await StoredReportAsync(otherSource)).CommercialModelName);
        Assert.Equal("103C_5336AN", (await StoredReportAsync(singleToken)).CommercialModelName);
        Assert.Equal("EliteBook 840 G8", (await StoredReportAsync(g8)).CommercialModelName);
        Assert.Null((await StoredReportAsync(doomed)).CommercialModelName);
    }

    /// <summary>🔴 <b>تنضيف شركة مابيلمسش صفوف شركة تانية.</b></summary>
    [Fact]
    public async Task Oem_cleanup_stays_inside_the_tenant()
    {
        var tenant = await TenantAsync();
        var other = await TenantAsync();

        var theirs = await ReportAsync(other, name: "103C_5336AN HP EliteBook", source: "SystemFamily");

        await Send(new ClearOemCodeNamesCommand(tenant));

        Assert.Equal("103C_5336AN HP EliteBook", (await StoredReportAsync(theirs)).CommercialModelName);
    }

    // =================================================================
    //  الاسم التجاري على كل الأجهزة
    // =================================================================

    private static readonly DateTime Older = new(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Newer = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// 🔴 <b>جهاز كسب فحوص من غير استقبال بياخد اسمه.</b> أحدث فحص
    /// <b>بمصدر موثوق واسم صالح</b> — الأحدث من غير مصدر أو بكود مصنّع
    /// مابيدهسوش.
    /// </summary>
    [Fact]
    public async Task A_device_takes_the_newest_trusted_usable_name_from_its_scans()
    {
        var tenant = await TenantAsync();
        var device = await DeviceAsync(tenant);

        await ReportAsync(tenant, deviceId: device, name: "ideapad 330-15ICH", source: Trusted, machineType: "81FK", startedAtUtc: Older);
        await ReportAsync(tenant, deviceId: device, name: "Something manual", source: "إدخال يدوي", startedAtUtc: Newer);
        await ReportAsync(tenant, deviceId: device, name: "103C_5336AN HP EliteBook", source: "SystemFamily", startedAtUtc: Newer.AddDays(1));
        await ReportAsync(tenant, deviceId: device, name: "Deleted Name", source: Trusted, deleted: true, startedAtUtc: Newer.AddDays(2));

        var result = await Send(new HydrateCommercialModelsCommand(tenant));

        Assert.Equal(1, result.Updated);

        var stored = await StoredDeviceAsync(device);
        Assert.Equal("ideapad 330-15ICH", stored.CommercialModelName);
        Assert.Equal(Trusted, stored.CommercialModelSource);
        Assert.Equal("81FK", stored.MachineType);

        var again = await Send(new HydrateCommercialModelsCommand(tenant));
        Assert.Equal(0, again.Updated);
    }

    /// <summary>⚠️ كود المصنع مابيتمسحش بفاضي.</summary>
    [Fact]
    public async Task An_empty_machine_type_never_wipes_a_stored_one()
    {
        var tenant = await TenantAsync();
        var device = await DeviceAsync(tenant);

        await using (var db = fixture.Create())
        {
            await db.Devices.Where(d => d.Id == device)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.MachineType, "20L5"));
        }

        await ReportAsync(tenant, deviceId: device, name: "ThinkPad T480", source: "SystemFamily", startedAtUtc: Newer);

        await Send(new HydrateCommercialModelsCommand(tenant));

        var stored = await StoredDeviceAsync(device);
        Assert.Equal("ThinkPad T480", stored.CommercialModelName);
        Assert.Equal("20L5", stored.MachineType);
    }

    /// <summary>
    /// مفيش دليل موثوق ← الجهاز على الخام ومابيتلمسش. والمدموج بيتملى
    /// زي القديم. وكل الأجهزة بتتعد، بالدفعات.
    /// </summary>
    [Fact]
    public async Task Devices_without_evidence_are_left_alone_and_merged_devices_are_hydrated()
    {
        var tenant = await TenantAsync();
        var bare = await DeviceAsync(tenant);
        var merged = await DeviceAsync(tenant, DeviceLifecycleStatus.Merged);

        await ReportAsync(tenant, deviceId: merged, name: "Latitude 5490", source: "Model", startedAtUtc: Newer);

        var result = await Send(new HydrateCommercialModelsCommand(tenant, BatchSize: 1));

        Assert.Equal((2, 1), (result.Examined, result.Updated));
        Assert.Null((await StoredDeviceAsync(bare)).CommercialModelName);
        Assert.Equal("Latitude 5490", (await StoredDeviceAsync(merged)).CommercialModelName);
    }

    /// <summary>🔴 <b>لفّة شركة مابتملاش أجهزة شركة تانية.</b></summary>
    [Fact]
    public async Task Hydration_stays_inside_the_tenant()
    {
        var tenant = await TenantAsync();
        var other = await TenantAsync();

        var theirs = await DeviceAsync(other);
        await ReportAsync(other, deviceId: theirs, name: "ProBook 450 G5", source: "Model", startedAtUtc: Newer);

        var result = await Send(new HydrateCommercialModelsCommand(tenant));

        Assert.Equal(0, result.Examined);
        Assert.Null((await StoredDeviceAsync(theirs)).CommercialModelName);
    }

    // =================================================================
    //  جهات التسليم
    // =================================================================

    /// <summary>شركة من غير جهات بتاخد الأربعة بأساميهم وأنواعهم وترتيبهم.</summary>
    [Fact]
    public async Task A_tenant_without_destinations_gets_the_four_and_a_second_run_adds_nothing()
    {
        var tenant = await TenantAsync();

        Assert.Equal(4, await Send(new SeedHandoverLocationsCommand(tenant)));
        Assert.Equal(0, await Send(new SeedHandoverLocationsCommand(tenant)));

        await using var db = fixture.Create();
        var rows = await db.Locations.AsNoTracking()
            .Where(l => l.TenantId == tenant)
            .OrderBy(l => l.SortOrder)
            .Select(l => new { l.Code, l.Name, l.Kind, l.SortOrder, l.IsActive })
            .ToListAsync();

        Assert.Equal(
            new[]
            {
                new { Code = "WH-5", Name = "مخزن الخامس", Kind = LocationKind.Warehouse, SortOrder = 10, IsActive = true },
                new { Code = "WH-6", Name = "مخزن السادس", Kind = LocationKind.Warehouse, SortOrder = 20, IsActive = true },
                new { Code = "WH-CPU", Name = "مخزن معالجات اللابات", Kind = LocationKind.Warehouse, SortOrder = 30, IsActive = true },
                new { Code = "SALES-LAPS", Name = "مبيعات اللابات", Kind = LocationKind.Sales, SortOrder = 40, IsActive = true },
            },
            rows);
    }

    /// <summary>
    /// ⚠️ <b>الموجود مابيتلمسش</b> — اسم اتغيّر، موقع موقوف، وكود
    /// بحروف صغيرة — والناقص بس بيرجع.
    /// </summary>
    [Fact]
    public async Task Existing_destinations_are_left_as_people_changed_them_and_only_the_missing_one_returns()
    {
        var tenant = await TenantAsync();

        await using (var db = fixture.Create())
        {
            db.Locations.AddRange(
                new Location { TenantId = tenant, Code = "WH-5", Name = "مخزن الدور الخامس", SortOrder = 99 },
                new Location { TenantId = tenant, Code = "WH-6", Name = "مخزن السادس", IsActive = false },
                new Location { TenantId = tenant, Code = "wh-cpu", Name = "معالجات" });
            await db.SaveChangesAsync();
        }

        Assert.Equal(1, await Send(new SeedHandoverLocationsCommand(tenant)));

        await using var check = fixture.Create();
        var rows = await check.Locations.AsNoTracking().Where(l => l.TenantId == tenant).ToListAsync();

        Assert.Equal(4, rows.Count);
        Assert.Equal("مخزن الدور الخامس", rows.Single(l => l.Code == "WH-5").Name);
        Assert.False(rows.Single(l => l.Code == "WH-6").IsActive);
        Assert.Single(rows, l => l.Code == "SALES-LAPS");
    }

    // =================================================================
    //  الشركات والقفل
    // =================================================================

    [Fact]
    public async Task Every_tenant_is_listed_for_the_sweeps()
    {
        var a = await TenantAsync();
        var b = await TenantAsync();

        var all = await Send(new GetTenantIdsQuery());

        Assert.Contains(a, all);
        Assert.Contains(b, all);
    }

    /// <summary>
    /// 🔴 <b>عملية واحدة بس تمسك الصيانة.</b> التانية بتاخد <c>null</c>
    /// على طول (مابتستناش)، وبعد السيب القفل بيتاخد تاني.
    /// </summary>
    [Fact]
    public async Task The_maintenance_lock_is_held_by_one_process_at_a_time()
    {
        const string name = "codlek:test-lock";

        await using var services = Services();

        using var first = services.CreateScope();
        using var second = services.CreateScope();

        var held = await first.ServiceProvider.GetRequiredService<IMaintenanceLock>().TryAcquireAsync(name);
        Assert.NotNull(held);

        Assert.Null(await second.ServiceProvider.GetRequiredService<IMaintenanceLock>().TryAcquireAsync(name));

        await held!.DisposeAsync();

        var again = await second.ServiceProvider.GetRequiredService<IMaintenanceLock>().TryAcquireAsync(name);
        Assert.NotNull(again);
        await again!.DisposeAsync();
    }
}
