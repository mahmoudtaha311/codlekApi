using Codlek.Core.Analytics;
using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Codlek.Infrastructure.Data;
using Codlek.Infrastructure.Repositories;

namespace Codlek.Tests;

/// <summary>
/// إنتاجية الفنيين — <b>على قاعدة حقيقية</b>.
///
/// <para>🔴 <b>الملف ده اتكتب بعد ما النقطة رجّعت ٥٠٠ على HTTP
/// حقيقي.</b> الاستعلام كان بيبني <c>record</c> موضعي جوّه
/// <c>GroupBy</c> ومعاه استعلام فرعي مرتّب (اسم الفني من أحدث فحص)
/// — وده <b>مابيترجمش</b>. EF بيعرف يرتّب على خاصية نوع مجهول لأنه
/// متتبّع أصلها؛ ومع باراميتر مُنشئ بيبقى قيمة مالهاش أصل معروف.
/// </para>
///
/// <para>⚠️ <b>وفحوص الوحدة كلها كانت خضراء</b> — هي بتجرّب الدوال
/// النقية، والترجمة مابتبانش غير على SQL Server. والمشروع القديم
/// فيه نفس التحذير مكتوب في مكانين، ووقعنا فيه تالت مرة.</para>
/// </summary>
public class TechnicianProductivityRepositoryTests(TechnicianDbFixture fixture)
    : IClassFixture<TechnicianDbFixture>
{
    private static Guid NewTenant(AppDbContext db)
    {
        var tenant = new Tenant { Name = "ورشة " + Guid.NewGuid().ToString("N")[..6] };
        db.Tenants.Add(tenant);
        return tenant.Id;
    }

    private static Report NewReport(
        AppDbContext db, Guid tenantId, DateTime startedAtUtc,
        string technicianCode = "T001",
        string technicianName = "محمود",
        int fail = 0, int error = 0,
        long durationMs = 600_000,
        bool isDeleted = false)
    {
        var row = new Report
        {
            TenantId = tenantId,
            DeviceCode = "RAW",
            StartedAtUtc = startedAtUtc,
            ReceivedAtUtc = startedAtUtc,
            TechnicianCode = technicianCode,
            TechnicianName = technicianName,
            PassCount = 8,
            FailCount = fail,
            ErrorCount = error,
            DurationMs = durationMs,
            IsDeleted = isDeleted,
        };
        db.Reports.Add(row);
        return row;
    }

    private static Technician NewTechnician(
        AppDbContext db, Guid tenantId, string code, string name)
    {
        var row = new Technician
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = name,
            Username = Guid.NewGuid().ToString("N")[..8],
            NormalizedUsername = Guid.NewGuid().ToString("N")[..8],
            IsActive = true,
            CanRepair = true,
        };
        db.Technicians.Add(row);
        return row;
    }

    /// <summary>
    /// ⚠️ <b>أمر الصيانة محتاج جهاز حقيقي.</b> أول نسخة من الفحوص
    /// دي حطّت <c>Guid.NewGuid()</c> وقعت على المفتاح الأجنبي —
    /// والقاعدة الحقيقية هي اللي لقطته.
    /// </summary>
    private static Device NewDevice(AppDbContext db, Guid tenantId)
    {
        var row = new Device
        {
            TenantId = tenantId,
            PublicCode = "DV-" + Guid.NewGuid().ToString("N")[..6],
            LastKnownManufacturer = "HP",
            LastKnownModel = "6470b",
            LastSeenAtUtc = DateTime.UtcNow,
        };
        db.Devices.Add(row);
        return row;
    }

    private static AnalyticsPeriod All => AnalyticsPeriod.Everything();

    // =================================================================
    //  الترجمة — الفحص اللي الملف ده اتعمل عشانه
    // =================================================================

    /// <summary>
    /// 🔴 <b>الاستعلام ده كان بيرمي وقت التشغيل.</b>
    ///
    /// <para>والفحص ده هو الحاجة الوحيدة اللي بتلاقيه: الترجمة
    /// مابتبانش غير على SQL Server، وفحوص الدوال النقية كلها كانت
    /// خضراء.</para>
    /// </summary>
    [Fact]
    public async Task The_testing_aggregate_translates_and_returns_the_name_from_the_latest_report()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var day = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);

        // ⚠️ الاسم اتغيّر: الصف الأحدث هو اللي بيحكم.
        NewReport(db, tenant, day, technicianName: "محمود القديم");
        NewReport(db, tenant, day.AddDays(1), technicianName: "محمود الجديد");

        await db.SaveChangesAsync();

        var rows = await new TechnicianProductivityRepository(db).TestingAsync(tenant, All);

        var row = Assert.Single(rows);

        Assert.Equal("T001", row.Code);
        Assert.Equal("محمود الجديد", row.Name);
        Assert.Equal(2, row.Total);
    }

    /// <summary>
    /// ⚠️ والفحص اللي اسم فنيه فاضي بيرجّع اسم فاضي — مش
    /// <c>null</c>، ومش استثناء.
    /// </summary>
    [Fact]
    public async Task A_report_with_no_technician_name_yields_an_empty_name()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        NewReport(db, tenant,
            new DateTime(2026, 8, 2, 9, 0, 0, DateTimeKind.Utc), technicianName: "");

        await db.SaveChangesAsync();

        var rows = await new TechnicianProductivityRepository(db).TestingAsync(tenant, All);

        Assert.Equal("", Assert.Single(rows).Name);
    }

    [Fact]
    public async Task The_single_technician_aggregate_translates_too()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var day = new DateTime(2026, 8, 3, 9, 0, 0, DateTimeKind.Utc);

        NewReport(db, tenant, day, fail: 2, durationMs: 600_000);
        NewReport(db, tenant, day.AddHours(1), durationMs: 1_200_000);

        // ⚠️ وفحص مالوش مدة — المقام لازم يفضل اتنين.
        NewReport(db, tenant, day.AddHours(2), durationMs: 0);

        await db.SaveChangesAsync();

        var facts = await new TechnicianProductivityRepository(db)
            .OneAsync(tenant, "T001", All);

        Assert.NotNull(facts);
        Assert.Equal(3, facts.Total);
        Assert.Equal(2, facts.Fail);
        Assert.Equal(2, facts.Timed);
        Assert.Equal(1_800_000, facts.DurationMs);
    }

    [Fact]
    public async Task The_single_aggregate_is_null_when_the_window_is_empty()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        NewReport(db, tenant, new DateTime(2026, 8, 4, 9, 0, 0, DateTimeKind.Utc));

        await db.SaveChangesAsync();

        var empty = AnalyticsPeriod.Resolve(
            "custom",
            new DateTime(2026, 1, 1),
            new DateTime(2026, 1, 2));

        Assert.Null(await new TechnicianProductivityRepository(db)
            .OneAsync(tenant, "T001", empty));
    }

    // =================================================================
    //  الجسر بين المفتاحين
    // =================================================================

    /// <summary>
    /// 🔴 <b>الفحص مفتاحه كود نص، وأمر الصيانة مفتاحه
    /// <c>Guid</c>.</b>
    ///
    /// <para>والقراية بترجّع الاتنين <b>بالكود</b> عشان المنادي
    /// يضمّهم من غير ما يعرف بالفخ ده.</para>
    /// </summary>
    [Fact]
    public async Task Repair_productivity_comes_back_keyed_by_the_technician_code()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var tech = NewTechnician(db, tenant, "T555", "كريم الصيانة");
        var device = NewDevice(db, tenant);

        await db.SaveChangesAsync();

        var opened = new DateTime(2026, 8, 5, 9, 0, 0, DateTimeKind.Utc);

        db.RepairWorkItems.Add(new RepairWorkItem
        {
            TenantId = tenant,
            DeviceId = device.Id,
            PublicCode = "RP-1",
            Status = RepairStatus.Completed,
            CompletedByTechnicianId = tech.Id,
            StartedAtUtc = opened,
            CompletedAtUtc = opened.AddMinutes(30),
            OpenedAtUtc = opened,
            OpenedByActorType = "User",
            OpenedByName = "كريم",
        });

        await db.SaveChangesAsync();

        var repairs = await new TechnicianProductivityRepository(db).RepairsAsync(tenant, All);

        Assert.True(repairs.ContainsKey("T555"));

        var facts = repairs["T555"];

        Assert.Equal("كريم الصيانة", facts.Name);
        Assert.Equal(1, facts.Count);
        Assert.Equal(30, facts.AverageMinutes);
    }

    /// <summary>
    /// ⚠️ <b>والأمر اللي مالوش «اللي قفله» بيرجع للمتسند</b> —
    /// الصفوف القديمة كانت بتقفل من غير ما تسجّل مين قفلها.
    /// </summary>
    [Fact]
    public async Task An_order_with_no_closer_falls_back_to_the_assignee()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var tech = NewTechnician(db, tenant, "T777", "محمود");
        var device = NewDevice(db, tenant);

        await db.SaveChangesAsync();

        var opened = new DateTime(2026, 8, 6, 9, 0, 0, DateTimeKind.Utc);

        db.RepairWorkItems.Add(new RepairWorkItem
        {
            TenantId = tenant,
            DeviceId = device.Id,
            PublicCode = "RP-OLD",
            Status = RepairStatus.Completed,

            // ⚠️ مفيش «اللي قفله» — زي الصفوف القديمة.
            CompletedByTechnicianId = null,
            AssignedTechnicianId = tech.Id,

            StartedAtUtc = opened,
            CompletedAtUtc = opened.AddHours(1),
            OpenedAtUtc = opened,
            OpenedByActorType = "User",
            OpenedByName = "كريم",
        });

        await db.SaveChangesAsync();

        var repairs = await new TechnicianProductivityRepository(db).RepairsAsync(tenant, All);

        Assert.Equal(1, repairs["T777"].Count);
        Assert.Equal(60, repairs["T777"].AverageMinutes);
    }

    /// <summary>
    /// 🔴 <b>المدة بالثواني، والقسمة بعد الجمع.</b>
    ///
    /// <para>فرق الدقايق في SQL Server بيعدّ <b>عبور حدود
    /// الدقيقة</b> مش الوقت اللي فات: من ١٠:٠٠:٥٩ لـ١٠:٠١:٠٠ بيقول
    /// دقيقة كاملة. على تصليحة عشر دقايق ده خطأ عشرة في
    /// المية.</para>
    /// </summary>
    [Fact]
    public async Task The_repair_average_is_measured_in_seconds_not_minute_boundaries()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var tech = NewTechnician(db, tenant, "T888", "محمود");
        var device = NewDevice(db, tenant);

        await db.SaveChangesAsync();

        var opened = new DateTime(2026, 8, 7, 10, 0, 59, DateTimeKind.Utc);

        // ⚠️ ثانية واحدة بالظبط — وفرق الدقايق كان بيقول «دقيقة».
        db.RepairWorkItems.Add(new RepairWorkItem
        {
            TenantId = tenant,
            DeviceId = device.Id,
            PublicCode = "RP-SEC",
            Status = RepairStatus.Completed,
            CompletedByTechnicianId = tech.Id,
            StartedAtUtc = opened,
            CompletedAtUtc = opened.AddSeconds(1),
            OpenedAtUtc = opened,
            OpenedByActorType = "User",
            OpenedByName = "كريم",
        });

        await db.SaveChangesAsync();

        var repairs = await new TechnicianProductivityRepository(db).RepairsAsync(tenant, All);

        // ثانية / ٦٠ = ٠٫٠ بعد التقريب لخانة واحدة.
        Assert.Equal(0, repairs["T888"].AverageMinutes);
    }

    /// <summary>
    /// ⚠️ واللي مالوش بداية مابيدخلش المتوسط، والمقام بيتعدّ لوحده.
    /// </summary>
    [Fact]
    public async Task An_order_with_no_start_stays_out_of_the_repair_average()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var tech = NewTechnician(db, tenant, "T999", "محمود");
        var device = NewDevice(db, tenant);

        await db.SaveChangesAsync();

        var opened = new DateTime(2026, 8, 8, 9, 0, 0, DateTimeKind.Utc);

        db.RepairWorkItems.Add(new RepairWorkItem
        {
            TenantId = tenant, DeviceId = device.Id, PublicCode = "RP-TIMED",
            Status = RepairStatus.Completed, CompletedByTechnicianId = tech.Id,
            StartedAtUtc = opened, CompletedAtUtc = opened.AddMinutes(20),
            OpenedAtUtc = opened, OpenedByActorType = "User", OpenedByName = "كريم",
        });

        db.RepairWorkItems.Add(new RepairWorkItem
        {
            TenantId = tenant, DeviceId = device.Id, PublicCode = "RP-NOSTART",
            Status = RepairStatus.Completed, CompletedByTechnicianId = tech.Id,
            StartedAtUtc = null, CompletedAtUtc = opened.AddMinutes(30),
            OpenedAtUtc = opened, OpenedByActorType = "User", OpenedByName = "كريم",
        });

        await db.SaveChangesAsync();

        var repairs = await new TechnicianProductivityRepository(db).RepairsAsync(tenant, All);

        Assert.Equal(2, repairs["T999"].Count);

        // ⚠️ عشرين دقيقة (على الواحد اللي ليه بداية) — مش عشرة.
        Assert.Equal(20, repairs["T999"].AverageMinutes);
    }

    /// <summary>
    /// ⚠️ والأمر اللي لسه مقفلش مابيدخلش خالص — الإنتاجية بتعدّ
    /// <b>اللي خلص</b>.
    /// </summary>
    [Fact]
    public async Task An_open_order_is_not_counted()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var tech = NewTechnician(db, tenant, "T100", "محمود");
        var device = NewDevice(db, tenant);

        await db.SaveChangesAsync();

        var opened = new DateTime(2026, 8, 9, 9, 0, 0, DateTimeKind.Utc);

        db.RepairWorkItems.Add(new RepairWorkItem
        {
            TenantId = tenant, DeviceId = device.Id, PublicCode = "RP-OPEN",
            Status = RepairStatus.InProgress, AssignedTechnicianId = tech.Id,
            StartedAtUtc = opened, CompletedAtUtc = null,
            OpenedAtUtc = opened, OpenedByActorType = "User", OpenedByName = "كريم",
        });

        await db.SaveChangesAsync();

        var repairs = await new TechnicianProductivityRepository(db).RepairsAsync(tenant, All);

        Assert.Empty(repairs);
    }

    // =================================================================
    //  الوجود والاسم
    // =================================================================

    /// <summary>
    /// ⚠️ <b>الوجود والاسم بيتقاسوا على كل التاريخ</b> — اختيار
    /// أسبوع فاضي مالوش يخلّي الفني يختفي.
    /// </summary>
    [Fact]
    public async Task Existence_and_name_ignore_the_selected_window()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        // ⚠️ شغل من سنة.
        NewReport(db, tenant,
            new DateTime(2025, 1, 15, 9, 0, 0, DateTimeKind.Utc),
            technicianName: "محمود القديم");

        await db.SaveChangesAsync();

        var repo = new TechnicianProductivityRepository(db);

        Assert.True(await repo.HasAnyReportAsync(tenant, "T001"));
        Assert.Equal("محمود القديم", await repo.NameFromReportsAsync(tenant, "T001"));

        Assert.False(await repo.HasAnyReportAsync(tenant, "NOSUCH"));
        Assert.Null(await repo.NameFromReportsAsync(tenant, "NOSUCH"));

        // ⚠️ والكود الفاضي بيرجّع `null` من غير ما يضرب القاعدة.
        Assert.Null(await repo.NameFromReportsAsync(tenant, "  "));
    }

    /// <summary>
    /// ⚠️ والاسم من <b>أحدث فحص فيه اسم</b> — الفحص الأحدث اللي
    /// اسمه فاضي مابياخدش الدور.
    /// </summary>
    [Fact]
    public async Task The_name_comes_from_the_latest_report_that_actually_has_one()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var day = new DateTime(2026, 8, 10, 9, 0, 0, DateTimeKind.Utc);

        NewReport(db, tenant, day, technicianName: "محمود");
        NewReport(db, tenant, day.AddDays(1), technicianName: "");

        await db.SaveChangesAsync();

        Assert.Equal("محمود",
            await new TechnicianProductivityRepository(db)
                .NameFromReportsAsync(tenant, "T001"));
    }

    // =================================================================
    //  الممسوح والشركة
    // =================================================================

    [Fact]
    public async Task A_deleted_report_is_out_of_every_number()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var day = new DateTime(2026, 8, 11, 9, 0, 0, DateTimeKind.Utc);

        NewReport(db, tenant, day);
        NewReport(db, tenant, day.AddHours(1), isDeleted: true);

        await db.SaveChangesAsync();

        var repo = new TechnicianProductivityRepository(db);

        Assert.Equal(1, Assert.Single(await repo.TestingAsync(tenant, All)).Total);
        Assert.Equal(1, (await repo.OneAsync(tenant, "T001", All))!.Total);
        Assert.Single(await repo.RecentAsync(tenant, "T001", All, 10));
    }

    [Fact]
    public async Task Another_tenant_is_invisible()
    {
        using var db = fixture.Create();
        var mine = NewTenant(db);
        var theirs = NewTenant(db);

        NewReport(db, theirs, new DateTime(2026, 8, 12, 9, 0, 0, DateTimeKind.Utc));

        await db.SaveChangesAsync();

        var repo = new TechnicianProductivityRepository(db);

        Assert.Empty(await repo.TestingAsync(mine, All));
        Assert.False(await repo.HasAnyReportAsync(mine, "T001"));
        Assert.Null(await repo.OneAsync(mine, "T001", All));
        Assert.Empty(await repo.RecentAsync(mine, "T001", All, 10));
    }

    [Fact]
    public async Task The_recent_list_is_newest_first_and_capped()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var day = new DateTime(2026, 8, 13, 9, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i < 5; i++) NewReport(db, tenant, day.AddHours(i));

        await db.SaveChangesAsync();

        var recent = await new TechnicianProductivityRepository(db)
            .RecentAsync(tenant, "T001", All, take: 3);

        Assert.Equal(3, recent.Count);
        Assert.Equal(day.AddHours(4), recent[0].StartedAtUtc);
    }
}
