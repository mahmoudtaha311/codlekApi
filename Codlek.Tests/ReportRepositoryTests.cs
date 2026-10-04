using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Core.Text;
using Codlek.Infrastructure.Data;
using Codlek.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Codlek.Tests;

/// <summary>
/// الفحوص — <b>على قاعدة حقيقية</b>.
///
/// <para>🔴 <b>وأهم حاجتين هنا:</b> الترتيب بوقت <u>السيرفر</u> مش
/// بساعة الراكة، وقراية النسخ من الحمولة الخام بـ<c>JSON_VALUE</c>
/// مع حارس <c>ISJSON</c> — ودول الاتنين مابيتجرّبوش غير على SQL
/// Server حقيقي.</para>
/// </summary>
public class ReportRepositoryTests(ReportDbFixture fixture)
    : IClassFixture<ReportDbFixture>
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
        AppDbContext db, Guid tenantId,
        DateTime startedAtUtc,
        DateTime? receivedAtUtc = null,
        Guid? deviceId = null,
        string deviceCode = "RAW-CODE",
        string technicianCode = "T001",
        int fail = 0, int error = 0,
        string storageText = "SSD 256GB",
        bool isDeleted = false,
        bool needsResolution = false,
        string rawJson = "",
        Guid? rackId = null)
    {
        var row = new Report
        {
            TenantId = tenantId,
            DeviceId = deviceId,
            DeviceCode = deviceCode,
            SearchText = ArabicText.Combine(deviceCode, "HP", "6470b"),
            StartedAtUtc = startedAtUtc,
            ReceivedAtUtc = receivedAtUtc ?? startedAtUtc,
            TechnicianCode = technicianCode,
            TechnicianName = "فني " + technicianCode,
            FailCount = fail,
            ErrorCount = error,
            StorageText = storageText,
            IsDeleted = isDeleted,
            NeedsDeviceResolution = needsResolution,
            RawJson = rawJson,
            SourceRackId = rackId,
            Manufacturer = "HP",
            Model = "6470b",
        };
        db.Reports.Add(row);
        return row;
    }

    private static ReportListFilter Any => new();

    // =================================================================
    //  الترتيب — أهم قاعدة في الملف
    // =================================================================

    /// <summary>
    /// 🔴 <b>الترتيب بوقت السيرفر، مش بساعة الراكة.</b>
    ///
    /// <para>وقت بداية الفحص بيجي من الراكة، وساعة الراكة مش موثوقة —
    /// ودي مش نظرية: راكة في الميدان ساعتها كانت مقدّمة <b>٥٧
    /// دقيقة</b>، فكان فحصها «١٧:٢٢ بتاعها» بيقعد فوق فحص اترفع
    /// بعده فعلاً.</para>
    ///
    /// <para>⚠️ <b>والحل مش تعديل وقت البداية</b> — دي دليل تجاري
    /// على وقت الفحص وبتفضل زي ما هي. الترتيب بس هو اللي بينتقل
    /// لحقل السيرفر بيكتبه بنفسه.</para>
    /// </summary>
    [Fact]
    public async Task A_rack_with_a_fast_clock_cannot_jump_the_queue()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var serverNow = new DateTime(2026, 6, 1, 16, 52, 0, DateTimeKind.Utc);

        // ⚠️ راكة ساعتها مقدّمة ٥٧ دقيقة: بتقول إنها بدأت ١٧:٢٢،
        // والسيرفر استلمها قبل اللي بعدها.
        var fastRack = NewReport(db, tenant,
            startedAtUtc: serverNow.AddMinutes(30),
            receivedAtUtc: serverNow,
            deviceCode: "FAST-RACK");

        // وفحص اترفع بعده فعلاً.
        var later = NewReport(db, tenant,
            startedAtUtc: serverNow.AddMinutes(-10),
            receivedAtUtc: serverNow.AddMinutes(5),
            deviceCode: "REAL-LATER");

        await db.SaveChangesAsync();

        var (rows, _) = await new ReportRepository(db).ListAsync(tenant, Any);

        // 🔴 اللي اترفع بعده بيقعد فوق — بوقت السيرفر.
        Assert.Equal(["REAL-LATER", "FAST-RACK"], rows.Select(r => r.DeviceCode));

        // ⚠️ ووقت البداية الغلط لسه متخزّن زي ما هو — دليل مش بيتعدّل.
        Assert.Equal(serverNow.AddMinutes(30), rows[1].StartedAtUtc);
    }

    /// <summary>
    /// 🔴 <b>والترتيب لازم فيه عمود فريد.</b>
    ///
    /// <para>دفعة مزامنة بتوصل بنفس <c>ReceivedAtUtc</c> بالحرف —
    /// وبنفس وقت البداية كمان لو الراكة بعتت الدفعة في طلب
    /// واحد.</para>
    ///
    /// <para>⚠️ والفحص بيقرا جملة <c>ORDER BY</c> المولّدة: الفحص
    /// اللي بيعتمد على الصفوف وحدها بيعدّي على المسخ، لأن SQL Server
    /// بيستعمل نفس الخطة في الاستعلامين لما الصفوف قليلة. (نفس
    /// الحكاية حصلت في الصيانة والأجهزة.)</para>
    /// </summary>
    [Fact]
    public async Task The_order_ends_with_a_unique_column()
    {
        var sql = new List<string>();

        using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(fixture.ConnectionString)
                .LogTo(sql.Add, [DbLoggerCategory.Database.Command.Name], LogLevel.Information)
                .Options);

        var tenant = NewTenant(db);
        var moment = new DateTime(2026, 6, 2, 10, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i < 6; i++)
            NewReport(db, tenant, moment, moment, deviceCode: $"BATCH-{i}");

        await db.SaveChangesAsync();

        var repo = new ReportRepository(db);

        var first = await repo.ListAsync(tenant, Any with { Page = 1, PageSize = 3 });
        var second = await repo.ListAsync(tenant, Any with { Page = 2, PageSize = 3 });
        await repo.ExportAsync(tenant, Any, cap: 10);

        var seen = first.Rows.Concat(second.Rows).Select(r => r.DeviceCode).ToList();

        Assert.Equal(6, seen.Count);
        Assert.Equal(6, seen.Distinct().Count());

        // ⚠️ آخر `ORDER BY` مش أول واحد — عشان أي استعلام فرعي.
        var ordered = sql
            .Where(line => line.Contains("ORDER BY", StringComparison.Ordinal))
            .Select(line => line[line.LastIndexOf("ORDER BY", StringComparison.Ordinal)..])
            .ToList();

        Assert.Equal(3, ordered.Count);

        Assert.All(ordered, clause =>
            Assert.Contains("[Id]", clause, StringComparison.Ordinal));
    }

    // =================================================================
    //  الفلاتر
    // =================================================================

    [Fact]
    public async Task The_result_filter_splits_the_four_cases()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var at = new DateTime(2026, 6, 3, 9, 0, 0, DateTimeKind.Utc);

        NewReport(db, tenant, at, deviceCode: "R-HEALTHY");
        NewReport(db, tenant, at.AddMinutes(1), deviceCode: "R-FAIL", fail: 2);
        NewReport(db, tenant, at.AddMinutes(2), deviceCode: "R-NOHARD",
            storageText: "No Hard");
        NewReport(db, tenant, at.AddMinutes(3), deviceCode: "R-UNRESOLVED",
            needsResolution: true);

        await db.SaveChangesAsync();

        var repo = new ReportRepository(db);

        async Task<List<string>> Of(ReportResultFilter result)
        {
            var (rows, _) = await repo.ListAsync(tenant, Any with { Result = result });

            return rows.Select(r => r.DeviceCode)
                .OrderBy(c => c, StringComparer.Ordinal)
                .ToList();
        }

        // ⚠️ «سليم» = مفيش فشل — واللي فيه «No Hard» سليم برضو
        // (مفيش فشل) واللي محتاج ربط كمان.
        Assert.Equal(["R-HEALTHY", "R-NOHARD", "R-UNRESOLVED"],
            await Of(ReportResultFilter.Healthy));

        Assert.Equal(["R-FAIL"], await Of(ReportResultFilter.NeedsRepair));
        Assert.Equal(["R-NOHARD"], await Of(ReportResultFilter.NoHard));

        // 🔴 السبب التالت في جرس التحذيرات — وده الباب بتاعه.
        Assert.Equal(["R-UNRESOLVED"], await Of(ReportResultFilter.NeedsDeviceResolution));
    }

    /// <summary>
    /// 🔴 <b>الفلتر ده كان في الشاشة والسيرفر مابياخدوش.</b>
    ///
    /// <para>قايمة «الحاوية» اتضافت في الواجهة وبتبعت الباراميتر،
    /// وASP.NET بيرمي أي باراميتر مش معرَّف <b>في صمت</b> — فالمدير
    /// بيختار شحنة، الفلتر بيولّع بلون، والصفوف تفضل صفوف الكل.
    /// أرقام غلط معروضة كأنها مفلترة.</para>
    ///
    /// <para>⚠️ والحاوية على <b>الجهاز</b> مش على الفحص.</para>
    /// </summary>
    [Fact]
    public async Task The_container_filter_reaches_through_the_device()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var container = new ImportContainer
        {
            TenantId = tenant, Code = "C-1", NormalizedCode = "C1",
            Name = "شحنة", CreatedByName = "كريم",
        };
        db.Containers.Add(container);

        var inShipment = NewDevice(db, tenant, "DV-IN");
        var outside = NewDevice(db, tenant, "DV-OUT");

        await db.SaveChangesAsync();

        inShipment.ContainerId = container.Id;

        var at = new DateTime(2026, 6, 4, 9, 0, 0, DateTimeKind.Utc);

        NewReport(db, tenant, at, deviceId: inShipment.Id, deviceCode: "R-IN");
        NewReport(db, tenant, at.AddMinutes(1), deviceId: outside.Id, deviceCode: "R-OUT");

        // ⚠️ وفحص مش مربوط بجهاز خالص — مالوش حاوية.
        NewReport(db, tenant, at.AddMinutes(2), deviceId: null, deviceCode: "R-UNLINKED");

        await db.SaveChangesAsync();

        var (rows, _) = await new ReportRepository(db)
            .ListAsync(tenant, Any with { ContainerId = container.Id });

        Assert.Equal(["R-IN"], rows.Select(r => r.DeviceCode));
    }

    /// <summary>
    /// ⚠️ والحاوية الفاضية (<c>Guid.Empty</c>) معناها «مفيش فلتر» —
    /// الواجهة بتبعتها كده لما المدير يختار «الكل».
    /// </summary>
    [Fact]
    public async Task An_empty_container_id_means_no_filter()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        NewReport(db, tenant, new DateTime(2026, 6, 5, 9, 0, 0, DateTimeKind.Utc));

        await db.SaveChangesAsync();

        var (rows, _) = await new ReportRepository(db)
            .ListAsync(tenant, Any with { ContainerId = Guid.Empty });

        Assert.Single(rows);
    }

    /// <summary>
    /// ⚠️ <b>المطابقة الحرفية على الكود جمب البحث النصي.</b>
    ///
    /// <para>العمود المطبَّع بيتبني وقت الاستقبال، فالفحوص اللي
    /// اتخزّنت بنسخة أقدم عمودها مافيهوش الكود لحد ما الملء يعدّي
    /// عليها — والمقارنة دي بتخلّيهم يتلاقوا فوراً.</para>
    /// </summary>
    [Fact]
    public async Task Search_matches_an_old_row_by_its_exact_device_code()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var at = new DateTime(2026, 6, 6, 9, 0, 0, DateTimeKind.Utc);

        // ⚠️ صف قديم: نص البحث مافيهوش الكود.
        var old = NewReport(db, tenant, at, deviceCode: "DV-OLDROW");
        old.SearchText = ArabicText.Combine("HP", "6470b");

        NewReport(db, tenant, at.AddMinutes(1), deviceCode: "DV-OTHER");

        await db.SaveChangesAsync();

        var (rows, _) = await new ReportRepository(db).ListAsync(tenant, Any with
        {
            ExactDeviceCode = "DV-OLDROW",
            SearchPattern = SearchPattern.Contains(ArabicText.Normalize("DV-OLDROW")),
        });

        Assert.Equal(["DV-OLDROW"], rows.Select(r => r.DeviceCode));
    }

    [Fact]
    public async Task The_technician_scope_hides_other_peoples_work()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var at = new DateTime(2026, 6, 7, 9, 0, 0, DateTimeKind.Utc);

        NewReport(db, tenant, at, technicianCode: "T001", deviceCode: "R-MINE");
        NewReport(db, tenant, at.AddMinutes(1), technicianCode: "T002", deviceCode: "R-THEIRS");

        await db.SaveChangesAsync();

        var repo = new ReportRepository(db);

        var mine = await repo.ListAsync(tenant, Any with { TechnicianCode = "T001" });
        var all = await repo.ListAsync(tenant, Any);

        Assert.Equal(["R-MINE"], mine.Rows.Select(r => r.DeviceCode));
        Assert.Equal(2, all.TotalItems);
    }

    [Fact]
    public async Task The_date_range_is_half_open_on_cairo_days()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        NewReport(db, tenant,
            new DateTime(2026, 6, 8, 22, 0, 0, DateTimeKind.Utc), deviceCode: "R-BEFORE");

        NewReport(db, tenant,
            new DateTime(2026, 6, 10, 20, 0, 0, DateTimeKind.Utc), deviceCode: "R-INSIDE");

        // ⚠️ على الحد بالظبط — لازم يتستبعد.
        NewReport(db, tenant,
            new DateTime(2026, 6, 11, 0, 0, 0, DateTimeKind.Utc), deviceCode: "R-BOUNDARY");

        await db.SaveChangesAsync();

        var (rows, _) = await new ReportRepository(db).ListAsync(tenant, Any with
        {
            FromUtc = new DateTime(2026, 6, 9, 0, 0, 0, DateTimeKind.Utc),
            ToUtc = new DateTime(2026, 6, 11, 0, 0, 0, DateTimeKind.Utc),
        });

        Assert.Equal(["R-INSIDE"], rows.Select(r => r.DeviceCode));
    }

    // =================================================================
    //  الممسوح
    // =================================================================

    /// <summary>
    /// 🔴 <b>الممسوح برّه القايمة، وجوّه صفحة الفحص الواحد.</b>
    ///
    /// <para>لو الفني مسح فحص المفروض العدد يقل. لكن الصف ده
    /// <b>دليل</b>: سجل المراجعة بيشاور عليه، وإخفاؤه بيخلّي الرابط
    /// يوصل لصفحة ميتة.</para>
    /// </summary>
    [Fact]
    public async Task A_deleted_report_leaves_the_list_but_its_page_still_opens()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var at = new DateTime(2026, 6, 12, 9, 0, 0, DateTimeKind.Utc);

        var deleted = NewReport(db, tenant, at, deviceCode: "R-GONE", isDeleted: true);
        deleted.DeletedReason = "اتفحص بالغلط";
        deleted.DeletedByName = "مدير المخزن";
        deleted.DeletedAtUtc = at.AddHours(1);

        NewReport(db, tenant, at.AddMinutes(1), deviceCode: "R-LIVE");

        await db.SaveChangesAsync();

        var repo = new ReportRepository(db);

        var (rows, total) = await repo.ListAsync(tenant, Any);

        Assert.Equal(1, total);
        Assert.Equal(["R-LIVE"], rows.Select(r => r.DeviceCode));

        // 🔴 وصفحته بتفتح — ومعاها السبب ومين مسحه.
        var detail = await repo.FindDetailAsync(tenant, deleted.Id);

        Assert.NotNull(detail);
        Assert.True(detail.IsDeleted);
        Assert.Equal("اتفحص بالغلط", detail.DeletedReason);
        Assert.Equal("مدير المخزن", detail.DeletedByName);
    }

    // =================================================================
    //  النسخ من الحمولة الخام
    // =================================================================

    /// <summary>
    /// 🔴 <b><c>JSON_VALUE</c> في SQL — الحمولة الخام ٢٠ كيلوبايت
    /// للفحص.</b>
    ///
    /// <para>صفحة فيها ٢٥ فحص كانت هتسحب نص مليون بايت عشان تقرا
    /// كلمتين.</para>
    /// </summary>
    [Fact]
    public async Task The_versions_are_read_out_of_the_raw_payload()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var report = NewReport(db, tenant,
            new DateTime(2026, 6, 13, 9, 0, 0, DateTimeKind.Utc),
            rawJson: """
                {"ApplicationVersion":"2.4.1","TestDefinitionVersion":"7"}
                """);

        await db.SaveChangesAsync();

        var facts = await new ReportRepository(db).VersionsAsync(tenant, report.Id);

        Assert.NotNull(facts);
        Assert.Equal("2.4.1", facts.ApplicationVersion);
        Assert.Equal("7", facts.TestDefinitionVersion);
    }

    /// <summary>
    /// 🔴 <b><c>ISJSON</c> مش رفاهية.</b>
    ///
    /// <para><c>JSON_VALUE</c> على نص مش JSON سليم <b>بيرمي</b> —
    /// يعني صف واحد بايظ كان هيوقّع الصفحة كلها. والحارس بيخلّيه
    /// يرجّع فاضي، والعرض بيقول «غير متاح».</para>
    /// </summary>
    [Fact]
    public async Task A_broken_raw_payload_does_not_take_the_page_down()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var broken = NewReport(db, tenant,
            new DateTime(2026, 6, 14, 9, 0, 0, DateTimeKind.Utc),
            rawJson: "{ this is not json at all");

        var empty = NewReport(db, tenant,
            new DateTime(2026, 6, 14, 10, 0, 0, DateTimeKind.Utc),
            rawJson: "");

        await db.SaveChangesAsync();

        var repo = new ReportRepository(db);

        var fromBroken = await repo.VersionsAsync(tenant, broken.Id);
        var fromEmpty = await repo.VersionsAsync(tenant, empty.Id);

        Assert.NotNull(fromBroken);
        Assert.Null(fromBroken.ApplicationVersion);

        Assert.NotNull(fromEmpty);
        Assert.Null(fromEmpty.ApplicationVersion);
    }

    /// <summary>
    /// ⚠️ <b>والاستعلام مقيّد بالشركة جوّه نصه</b> — لو المنادي غلط
    /// في المعرّف، الشرط ده لسه بيمنع.
    /// </summary>
    [Fact]
    public async Task The_raw_read_is_tenant_scoped_inside_the_sql()
    {
        using var db = fixture.Create();
        var mine = NewTenant(db);
        var theirs = NewTenant(db);

        var theirReport = NewReport(db, theirs,
            new DateTime(2026, 6, 15, 9, 0, 0, DateTimeKind.Utc),
            rawJson: """{"ApplicationVersion":"سرّي"}""");

        await db.SaveChangesAsync();

        Assert.Null(await new ReportRepository(db).VersionsAsync(mine, theirReport.Id));
    }

    // =================================================================
    //  التفاصيل والأسماء
    // =================================================================

    [Fact]
    public async Task The_detail_read_includes_the_steps_and_the_parts()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var report = NewReport(db, tenant,
            new DateTime(2026, 6, 16, 9, 0, 0, DateTimeKind.Utc));

        db.Steps.Add(new ReportStep
        { ReportId = report.Id, StepId = "screen", Title = "الشاشة", Status = 1 });

        db.Steps.Add(new ReportStep
        { ReportId = report.Id, StepId = "specs", Title = "المواصفات", Status = 0 });

        db.Parts.Add(new ReportPart { ReportId = report.Id, Name = "شاشة" });

        await db.SaveChangesAsync();

        var detail = await new ReportRepository(db).FindDetailAsync(tenant, report.Id);

        Assert.NotNull(detail);
        Assert.Equal(2, detail.Steps.Count);
        Assert.Single(detail.Parts);
    }

    [Fact]
    public async Task Rack_and_device_labels_come_back_in_one_read_each()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var rack = new Rack
        {
            TenantId = tenant, RackCode = "RK-9", Name = "راكة تسعة",
            ApiKeyHash = "x", Salt = "y",
        };
        db.Racks.Add(rack);

        var device = NewDevice(db, tenant, "DV-LIVE-CODE");

        await db.SaveChangesAsync();

        var report = NewReport(db, tenant,
            new DateTime(2026, 6, 17, 9, 0, 0, DateTimeKind.Utc),
            deviceId: device.Id, deviceCode: "DV-STALE-CODE", rackId: rack.Id);

        await db.SaveChangesAsync();

        var repo = new ReportRepository(db);

        var racks = await repo.RackLabelsAsync(tenant, [report.SourceRackId]);
        var codes = await repo.DeviceCodesAsync(tenant, [report.DeviceId]);

        Assert.Equal(new RackLabel("RK-9", "راكة تسعة"), racks[rack.Id]);

        // 🔴 كود اللاب الحالي، مش اللي متخزّن على الفحص.
        Assert.Equal("DV-LIVE-CODE", codes[device.Id]);
    }

    [Fact]
    public async Task The_label_reads_skip_the_database_when_there_is_nothing_to_look_up()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var repo = new ReportRepository(db);

        Assert.Empty(await repo.RackLabelsAsync(tenant, [null, null]));
        Assert.Empty(await repo.DeviceCodesAsync(tenant, []));
    }

    [Fact]
    public async Task Another_tenant_is_invisible()
    {
        using var db = fixture.Create();
        var mine = NewTenant(db);
        var theirs = NewTenant(db);

        var theirReport = NewReport(db, theirs,
            new DateTime(2026, 6, 18, 9, 0, 0, DateTimeKind.Utc));

        await db.SaveChangesAsync();

        var repo = new ReportRepository(db);

        Assert.Equal(0, (await repo.ListAsync(mine, Any)).TotalItems);
        Assert.Null(await repo.FindDetailAsync(mine, theirReport.Id));
        Assert.Equal(0, await repo.SnapshotComponentCountAsync(mine, theirReport.Id));
    }

    [Fact]
    public async Task The_export_returns_one_row_past_the_cap()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var at = new DateTime(2026, 6, 19, 9, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i < 5; i++)
            NewReport(db, tenant, at.AddMinutes(i), deviceCode: $"R-{i}");

        await db.SaveChangesAsync();

        var exported = await new ReportRepository(db).ExportAsync(tenant, Any, cap: 2);

        Assert.Equal(3, exported.Count);
    }
}
