using Codlek.Core.Entities;
using Codlek.Core.Hardware;
using Codlek.Infrastructure.Data;
using Codlek.Infrastructure.Repositories;

namespace Codlek.Tests;

/// <summary>
/// مستودع لقطات العتاد — <b>على قاعدة حقيقية</b>.
///
/// <para>🔴 <b>كل قاعدة في الملف ده عايشة جوّه الاستعلام.</b>
/// «أحدث لقطة فيها قطع فعلاً»، و«الفحص بتاع الجهاز ده»، وترشيح
/// المراحل بالشركة — مستودع بديل بيعدّي عليهم كلهم.</para>
/// </summary>
public class HardwareRepositoryTests(HardwareDbFixture fixture)
    : IClassFixture<HardwareDbFixture>
{
    private static Guid NewTenant(AppDbContext db)
    {
        var tenant = new Tenant { Name = "ورشة " + Guid.NewGuid().ToString("N")[..6] };
        db.Tenants.Add(tenant);
        return tenant.Id;
    }

    private static Device NewDevice(AppDbContext db, Guid tenantId, string code)
    {
        var row = new Device
        {
            TenantId = tenantId,
            PublicCode = code,
            LastKnownManufacturer = "HP",
            LastKnownModel = "6470b",
            LastSeenAtUtc = DateTime.UtcNow,
        };
        db.Devices.Add(row);
        return row;
    }

    private static Report NewReport(
        AppDbContext db, Guid tenantId, Guid? deviceId, DateTime startedAtUtc,
        bool withComponents = true, bool isDeleted = false,
        string technicianCode = "T001", string deviceCode = "SNAP-CODE")
    {
        var report = new Report
        {
            TenantId = tenantId,
            DeviceId = deviceId,
            DeviceCode = deviceCode,
            StartedAtUtc = startedAtUtc,
            TechnicianName = "محمود",
            TechnicianCode = technicianCode,
            IsDeleted = isDeleted,
            SnapshotCapturedAtUtc = startedAtUtc,
            SnapshotCollectorVersion = "1.4",
            SnapshotRanAsAdministrator = true,
        };
        db.Reports.Add(report);

        if (withComponents)
        {
            db.SnapshotComponents.Add(new ReportSnapshotComponent
            {
                TenantId = tenantId,
                ReportId = report.Id,
                Type = ComponentType.Storage,
                IdentityConfidence = 1,
                ManufacturerSerial = "SSD-" + report.Id.ToString("N")[..4],
                Model = "Samsung 860",
                IsPresent = true,
            });
        }

        return report;
    }

    // =================================================================
    //  أحدث لقطة
    // =================================================================

    /// <summary>
    /// 🔴 <b>أحدث فحص <u>ليه لقطة</u> — مش أحدث فحص.</b>
    ///
    /// <para>فيه فحوص اترفعت من غير لقطة عتاد خالص. لو أخدنا أحدث
    /// فحص وخلاص، اللاب اللي آخر فحص له من غير لقطة كان هيبان «مفيش
    /// قطع» وهو عنده لقطة كاملة من الفحص اللي قبله.</para>
    /// </summary>
    [Fact]
    public async Task The_latest_snapshot_skips_reports_that_have_no_components()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "D-LATEST");

        var withParts = NewReport(db, tenant, device.Id,
            new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc));

        // ⚠️ ده أحدث — ومفيش فيه ولا قطعة.
        NewReport(db, tenant, device.Id,
            new DateTime(2026, 3, 5, 9, 0, 0, DateTimeKind.Utc), withComponents: false);

        await db.SaveChangesAsync();

        var header = await new HardwareRepository(db).FindLatestSnapshotAsync(tenant, device.Id);

        Assert.NotNull(header);
        Assert.Equal(withParts.Id, header.ReportId);
    }

    [Fact]
    public async Task The_latest_snapshot_is_the_newest_one_that_has_components()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "D-ORDER");

        NewReport(db, tenant, device.Id, new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc));

        var newest = NewReport(db, tenant, device.Id,
            new DateTime(2026, 3, 4, 9, 0, 0, DateTimeKind.Utc));

        await db.SaveChangesAsync();

        var header = await new HardwareRepository(db).FindLatestSnapshotAsync(tenant, device.Id);

        Assert.Equal(newest.Id, header!.ReportId);
    }

    /// <summary>
    /// ⚠️ والفحص الممسوح منطقياً مش فحص موجود — على نقط الجهاز.
    /// </summary>
    [Fact]
    public async Task A_soft_deleted_report_is_invisible_to_the_device_endpoints()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "D-DELETED");

        var deleted = NewReport(db, tenant, device.Id,
            new DateTime(2026, 3, 2, 9, 0, 0, DateTimeKind.Utc), isDeleted: true);

        await db.SaveChangesAsync();

        var repo = new HardwareRepository(db);

        Assert.Null(await repo.FindLatestSnapshotAsync(tenant, device.Id));
        Assert.Null(await repo.FindHeaderForDeviceAsync(tenant, device.Id, deleted.Id));

        /*
          ⚠️ <b>وبرضو مقروء بمعرّفه — فرق منقول من القديم بقرار.</b>

          نقطة <c>/reports/{id}/hardware</c> بتقرا الجدول مباشرةً من
          غير ترشيح <c>IsDeleted</c>، ونقط الجهاز بتفلتر. الفحص ده
          بيثبّت الفرق ده عشان حد ميفتكرش إنه سهو.
        */
        Assert.NotNull(await repo.FindReportHeaderAsync(tenant, deleted.Id));
    }

    // =================================================================
    //  الحارس: الفحص بتاع الجهاز ده
    // =================================================================

    /// <summary>
    /// 🔴 <b>التقييد بالشركة لوحده مش كفاية.</b>
    ///
    /// <para>صفحة المقارنة بتاخد معرّفي فحص من المستخدم، فلو اتقيّدوا
    /// بالشركة بس، حد يقدر يقارن <b>جهازين مختلفين</b> بمجرد تغيير
    /// الأرقام في الرابط — ويطلّع فروق عتاد مالهاش أي معنى.</para>
    /// </summary>
    [Fact]
    public async Task A_report_from_another_device_is_refused_even_inside_the_same_tenant()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var mine = NewDevice(db, tenant, "D-MINE");
        var other = NewDevice(db, tenant, "D-OTHER");

        var onOther = NewReport(db, tenant, other.Id,
            new DateTime(2026, 3, 3, 9, 0, 0, DateTimeKind.Utc));

        await db.SaveChangesAsync();

        var repo = new HardwareRepository(db);

        Assert.Null(await repo.FindHeaderForDeviceAsync(tenant, mine.Id, onOther.Id));
        Assert.NotNull(await repo.FindHeaderForDeviceAsync(tenant, other.Id, onOther.Id));
    }

    [Fact]
    public async Task Another_tenant_sees_nothing()
    {
        using var db = fixture.Create();
        var mine = NewTenant(db);
        var theirs = NewTenant(db);

        var theirDevice = NewDevice(db, theirs, "D-THEIRS");

        var theirReport = NewReport(db, theirs, theirDevice.Id,
            new DateTime(2026, 3, 3, 9, 0, 0, DateTimeKind.Utc));

        await db.SaveChangesAsync();

        var repo = new HardwareRepository(db);

        Assert.Null(await repo.FindReportHeaderAsync(mine, theirReport.Id));
        Assert.Null(await repo.FindLatestSnapshotAsync(mine, theirDevice.Id));
        Assert.Null(await repo.FindDeviceAsync(mine, theirDevice.Id));
        Assert.Empty(await repo.ComponentsAsync(mine, theirReport.Id));
    }

    // =================================================================
    //  المراحل
    // =================================================================

    /// <summary>
    /// 🔴 <b>جدول المراحل مالوش <c>TenantId</c>.</b>
    ///
    /// <para>هو مربوط بالفحص وبس، فالترشيح بالشركة لازم يمرّ على
    /// الفحص. ومن غير الشرط ده، معرّف فحص بتاع شركة تانية في الرابط
    /// كان بيرجّع مراحلها.</para>
    /// </summary>
    [Fact]
    public async Task Steps_are_filtered_through_the_report_tenant()
    {
        using var db = fixture.Create();
        var mine = NewTenant(db);
        var theirs = NewTenant(db);

        var myDevice = NewDevice(db, mine, "D-STEPS");
        var theirDevice = NewDevice(db, theirs, "D-STEPS-X");

        var myReport = NewReport(db, mine, myDevice.Id,
            new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc));

        var theirReport = NewReport(db, theirs, theirDevice.Id,
            new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc));

        db.Steps.Add(new ReportStep
        { ReportId = myReport.Id, StepId = "battery", Title = "البطارية", Status = 1 });

        db.Steps.Add(new ReportStep
        { ReportId = theirReport.Id, StepId = "battery", Title = "سرّي", Status = 2 });

        await db.SaveChangesAsync();

        var rows = await new HardwareRepository(db)
            .StepsAsync(mine, myReport.Id, theirReport.Id);

        // ⚠️ المرحلة بتاعتي بس — رغم إن المعرّف التاني اتبعت.
        Assert.Equal(["البطارية"], rows.Select(r => r.Title));
    }

    [Fact]
    public async Task Steps_for_both_sides_come_back_in_one_read()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "D-BOTH");

        var left = NewReport(db, tenant, device.Id,
            new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc));

        var right = NewReport(db, tenant, device.Id,
            new DateTime(2026, 3, 2, 9, 0, 0, DateTimeKind.Utc));

        db.Steps.Add(new ReportStep
        { ReportId = left.Id, StepId = "batt", Title = "البطارية", Status = 2 });

        db.Steps.Add(new ReportStep
        { ReportId = right.Id, StepId = "batt", Title = "البطارية", Status = 1 });

        await db.SaveChangesAsync();

        var rows = await new HardwareRepository(db).StepsAsync(tenant, left.Id, right.Id);

        Assert.Equal(2, rows.Count);
        Assert.Equal(2, rows.Single(r => r.ReportId == left.Id).Status);
        Assert.Equal(1, rows.Single(r => r.ReportId == right.Id).Status);
    }

    // =================================================================
    //  الأكواد
    // =================================================================

    /// <summary>
    /// ⚠️ الراكة المش موجودة أو المحذوفة بترجّع كود فاضي — مش
    /// استثناء. الفحص القديم بتاعها لسه له قيمة.
    /// </summary>
    [Fact]
    public async Task A_missing_rack_yields_an_empty_code()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var repo = new HardwareRepository(db);

        Assert.Equal("", await repo.RackCodeAsync(tenant, null));
        Assert.Equal("", await repo.RackCodeAsync(tenant, Guid.NewGuid()));
    }

    [Fact]
    public async Task The_header_carries_the_snapshot_device_code_as_a_fallback()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        // ⚠️ فحص لسه مش مربوط بجهاز — اللي في اللقطة هو كل اللي عندنا.
        var unlinked = NewReport(db, tenant, deviceId: null,
            new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc), deviceCode: "RAW-7788");

        await db.SaveChangesAsync();

        var header = await new HardwareRepository(db).FindReportHeaderAsync(tenant, unlinked.Id);

        Assert.NotNull(header);
        Assert.Null(header.DeviceId);
        Assert.Equal("RAW-7788", header.SnapshotDeviceCode);
    }

    [Fact]
    public async Task Components_come_back_ordered_by_type_then_instance()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "D-ORDERED");

        var report = NewReport(db, tenant, device.Id,
            new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc), withComponents: false);

        foreach (var (type, index) in new[]
                 {
                     (ComponentType.Memory, 1), (ComponentType.System, 0),
                     (ComponentType.Memory, 0),
                 })
        {
            db.SnapshotComponents.Add(new ReportSnapshotComponent
            {
                TenantId = tenant,
                ReportId = report.Id,
                Type = type,
                InstanceIndex = index,
                IsPresent = true,
            });
        }

        await db.SaveChangesAsync();

        var rows = await new HardwareRepository(db).ComponentsAsync(tenant, report.Id);

        Assert.Equal(
            [(ComponentType.System, 0), (ComponentType.Memory, 0), (ComponentType.Memory, 1)],
            rows.Select(c => (c.Type, c.InstanceIndex)));
    }
}
