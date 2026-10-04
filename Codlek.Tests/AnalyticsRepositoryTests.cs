using Codlek.Core.Analytics;
using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Codlek.Core.Time;
using Codlek.Infrastructure.Data;
using Codlek.Infrastructure.Repositories;

namespace Codlek.Tests;

/// <summary>
/// أرقام اللوحة — <b>على قاعدة حقيقية</b>.
///
/// <para>🔴 <b>كل قاعدة في الملف ده عايشة جوّه الاستعلام:</b>
/// التضييق على الفني، وتعريف «نضيف»، وملء الفجوات بصفر، وتجميع
/// اليوم بتوقيت القاهرة، و«جديد مقابل معاد». ومستودع بديل بيعدّي
/// عليهم كلهم.</para>
///
/// <para>🔴 <b>وفيه استعلام واحد هنا كان بيوقّع نقطة الجرد كلها
/// بـ٥٠٠</b> — الترتيب بعد التحويل لنوع خاص مش قابل للترجمة
/// لـSQL، وEF بيرمي وقت ما يبني الاستعلام مش وقت القراية. يعني مش
/// مسألة بيانات: بيقع دايماً.</para>
/// </summary>
public class AnalyticsRepositoryTests(AnalyticsDbFixture fixture)
    : IClassFixture<AnalyticsDbFixture>
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
        AppDbContext db, Guid tenantId, DateTime startedAtUtc,
        Guid? deviceId = null,
        string technicianCode = "T001",
        int pass = 10, int fail = 0, int error = 0, int notPresent = 0, int skip = 0,
        long durationMs = 420_000,
        bool isDeleted = false,
        Guid? rackId = null)
    {
        var row = new Report
        {
            TenantId = tenantId,
            DeviceId = deviceId,
            DeviceCode = "RAW",
            StartedAtUtc = startedAtUtc,
            ReceivedAtUtc = startedAtUtc,
            TechnicianCode = technicianCode,
            TechnicianName = "فني " + technicianCode,
            PassCount = pass,
            FailCount = fail,
            ErrorCount = error,
            NotPresentCount = notPresent,
            SkipCount = skip,
            DurationMs = durationMs,
            IsDeleted = isDeleted,
            SourceRackId = rackId,
            Manufacturer = "HP",
            Model = "6470b",
        };
        db.Reports.Add(row);
        return row;
    }

    /// <summary>فترة ثابتة مش معتمدة على النهاردة — الفحص لازم يفضل صح بكرة.</summary>
    private static AnalyticsPeriod Window(DateTime firstCairoDay, int days) =>
        AnalyticsPeriod.Resolve("custom", firstCairoDay, firstCairoDay.AddDays(days - 1));

    private static AnalyticsPeriod Recent(int days = 7) =>
        Window(CairoDay.Today.AddDays(-(days - 1)), days);

    // =================================================================
    //  الأرقام الرئيسية
    // =================================================================

    /// <summary>
    /// 🔴 <b>«نضيف» = صفر فشل <u>وصفر</u> خطأ قراءة.</b>
    ///
    /// <para>فحص فيه خطوة ما اشتغلتش <b>مش</b> ناجح — الجهاز مش
    /// متثبت إنه سليم، وحسابه نجاح بيرفع الرقم بشغل ناقص.</para>
    /// </summary>
    [Fact]
    public async Task A_read_error_alone_keeps_a_report_out_of_the_pass_rate()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var day = CairoDay.Today.AddDays(-1);
        var at = CairoDay.StartOf(day).AddHours(10);

        NewReport(db, tenant, at, fail: 0, error: 0);
        NewReport(db, tenant, at.AddMinutes(1), fail: 0, error: 1);

        await db.SaveChangesAsync();

        var (kpis, counts) = await new AnalyticsRepository(db)
            .KpisAsync(tenant, null, Window(day, 1), includeStations: true);

        Assert.Equal(2, kpis.Reports);
        Assert.Equal(1, kpis.NeedsReview);

        // ⚠️ واحد من اتنين — ٥٠٪، مش ١٠٠٪.
        Assert.Equal(50.0, kpis.PassRate);
        Assert.Equal(1, counts.Error);
    }

    /// <summary>
    /// ⚠️ المتوسط على اللي ليه مدة بس — الفحوص المستوردة من شيتات
    /// قديمة مدّتها صفر، وإدخالها بيجيب المتوسط لتحت من غير أي
    /// معنى.
    /// </summary>
    [Fact]
    public async Task Reports_with_no_duration_stay_out_of_the_average()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var day = CairoDay.Today.AddDays(-2);
        var at = CairoDay.StartOf(day).AddHours(9);

        NewReport(db, tenant, at, durationMs: 600_000);
        NewReport(db, tenant, at.AddMinutes(1), durationMs: 0);

        await db.SaveChangesAsync();

        var (kpis, _) = await new AnalyticsRepository(db)
            .KpisAsync(tenant, null, Window(day, 1), includeStations: true);

        // ⚠️ عشر دقايق — مش خمسة.
        Assert.Equal(10.0, kpis.AverageMinutes);
    }

    /// <summary>
    /// 🔴 <b>عدّاد الراكات على مستوى الشركة — والاستعلام نفسه
    /// مابيتنفّذش للفني.</b>
    ///
    /// <para>الفني كان بيعرف كام محطة في الورشة — رقم إداري الصفحة
    /// المقابلة بتمنعه عنه صراحةً.</para>
    /// </summary>
    [Fact]
    public async Task The_station_count_is_zero_unless_asked_for()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        db.Racks.Add(new Rack
        {
            TenantId = tenant, RackCode = "RK-1", Name = "راكة",
            Status = RackStatus.Active, ApiKeyHash = "x", Salt = "y",
        });

        var day = CairoDay.Today.AddDays(-1);
        NewReport(db, tenant, CairoDay.StartOf(day).AddHours(9));

        await db.SaveChangesAsync();

        var repo = new AnalyticsRepository(db);
        var period = Window(day, 1);

        var forManager = await repo.KpisAsync(tenant, null, period, includeStations: true);
        var forTechnician = await repo.KpisAsync(tenant, "T001", period, includeStations: false);

        Assert.Equal(1, forManager.Kpis.ActiveStations);
        Assert.Equal(0, forTechnician.Kpis.ActiveStations);
    }

    /// <summary>
    /// 🔴 <b>التضييق على الفني — أول دالة تنساه بتفتح شغل ورشة
    /// كامل.</b>
    /// </summary>
    [Fact]
    public async Task A_technician_only_ever_sees_their_own_work()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var day = CairoDay.Today.AddDays(-1);
        var at = CairoDay.StartOf(day).AddHours(9);

        NewReport(db, tenant, at, technicianCode: "T001");
        NewReport(db, tenant, at.AddMinutes(1), technicianCode: "T002");
        NewReport(db, tenant, at.AddMinutes(2), technicianCode: "T002");

        await db.SaveChangesAsync();

        var repo = new AnalyticsRepository(db);
        var period = Window(day, 1);

        var mine = await repo.KpisAsync(tenant, "T001", period, includeStations: false);
        var all = await repo.KpisAsync(tenant, null, period, includeStations: true);

        Assert.Equal(1, mine.Kpis.Reports);
        Assert.Equal(3, all.Kpis.Reports);

        // ⚠️ وعدد الفنيين النشطين بيتضيّق كمان.
        Assert.Equal(1, mine.Kpis.ActiveTechnicians);
        Assert.Equal(2, all.Kpis.ActiveTechnicians);
    }

    /// <summary>
    /// ⚠️ <b>الممسوح مستبعد دايماً.</b> لو الفني مسح فحص المفروض
    /// العدد يقل — ودي كانت مشكلة حقيقية في البرنامج المكتبي.
    /// </summary>
    [Fact]
    public async Task A_deleted_report_leaves_every_number_except_the_deleted_count()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var day = CairoDay.Today.AddDays(-1);
        var at = CairoDay.StartOf(day).AddHours(9);

        NewReport(db, tenant, at);
        NewReport(db, tenant, at.AddMinutes(1), isDeleted: true);

        await db.SaveChangesAsync();

        var repo = new AnalyticsRepository(db);
        var period = Window(day, 1);

        var (kpis, _) = await repo.KpisAsync(tenant, null, period, includeStations: true);

        Assert.Equal(1, kpis.Reports);

        // ⚠️ واستعلام مستقل بيعدّ الممسوح — المدير لازم يعرف إن فيه
        // شغل اتشال من أرقام اليوم.
        Assert.Equal(1, await repo.DeletedCountAsync(tenant, period));
    }

    [Fact]
    public async Task Another_tenant_never_shows_up()
    {
        using var db = fixture.Create();
        var mine = NewTenant(db);
        var theirs = NewTenant(db);
        var day = CairoDay.Today.AddDays(-1);
        var at = CairoDay.StartOf(day).AddHours(9);

        NewReport(db, theirs, at);
        NewReport(db, theirs, at.AddMinutes(1));

        await db.SaveChangesAsync();

        var repo = new AnalyticsRepository(db);
        var period = Window(day, 1);

        var (kpis, _) = await repo.KpisAsync(mine, null, period, includeStations: true);

        Assert.Equal(0, kpis.Reports);
        Assert.Equal(0, await repo.DeletedCountAsync(mine, period));
        Assert.Empty(await repo.TechnicianActivityAsync(mine, null, period, 10));
    }

    // =================================================================
    //  المنحنى
    // =================================================================

    /// <summary>
    /// 🔴 <b>الفجوات بتتملّي بصفر.</b>
    ///
    /// <para>لو رجّعنا الأيام اللي فيها شغل بس، المنحنى بيوصّل يوم
    /// الأحد باللي بعده الخميس بخط مستقيم — والقارئ بيشوف شغل مستمر
    /// في أيام ما اشتغلش فيها حد. الصفر الصريح هو الحقيقة.</para>
    /// </summary>
    [Fact]
    public async Task The_daily_trend_has_a_point_for_every_day_even_the_empty_ones()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var first = CairoDay.Today.AddDays(-6);

        // شغل في أول يوم وآخر يوم بس.
        NewReport(db, tenant, CairoDay.StartOf(first).AddHours(10));
        NewReport(db, tenant, CairoDay.StartOf(CairoDay.Today).AddHours(10));

        await db.SaveChangesAsync();

        var points = await new AnalyticsRepository(db).TrendAsync(tenant, null, Recent());

        Assert.Equal(7, points.Count);
        Assert.Equal(2, points.Count(p => p.Total > 0));
        Assert.Equal(5, points.Count(p => p.Total == 0));

        // ⚠️ والترتيب من الأقدم للأحدث — الواجهة بترسم بالترتيب.
        Assert.Equal(
            points.Select(p => p.Bucket).OrderBy(b => b, StringComparer.Ordinal),
            points.Select(p => p.Bucket));
    }

    /// <summary>⚠️ ويوم واحد بيتفكّ على ٢٤ ساعة كاملة.</summary>
    [Fact]
    public async Task A_single_day_trend_has_twenty_four_hourly_points()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var day = CairoDay.Today.AddDays(-1);

        NewReport(db, tenant, CairoDay.StartOf(day).AddHours(14));

        await db.SaveChangesAsync();

        var points = await new AnalyticsRepository(db)
            .TrendAsync(tenant, null, AnalyticsPeriod.SingleDay(day));

        Assert.Equal(24, points.Count);
        Assert.Equal(1, points.Sum(p => p.Total));

        // 🔴 والساعة ١٤ بتوقيت القاهرة، مش بتوقيت UTC.
        Assert.Equal(1, points.Single(p => p.Bucket == "14").Total);
        Assert.Equal("14:00", points.Single(p => p.Bucket == "14").Label);
    }

    /// <summary>
    /// 🔴 <b>فحص بعد نص الليل بتوقيت القاهرة بيتحسب على يومه.</b>
    ///
    /// <para>ودي مش تفصيلة: شغل الوردية المتأخرة كان بيروح لفني في
    /// يوم غلط.</para>
    /// </summary>
    [Fact]
    public async Task Work_just_after_cairo_midnight_lands_on_its_own_day()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var day = CairoDay.Today.AddDays(-1);

        // ١٢:٣٠ بالليل بتوقيت القاهرة.
        NewReport(db, tenant, CairoDay.StartOf(day).AddMinutes(30));

        await db.SaveChangesAsync();

        var points = await new AnalyticsRepository(db)
            .TrendAsync(tenant, null, AnalyticsPeriod.SingleDay(day));

        Assert.Equal(1, points.Single(p => p.Bucket == "00").Total);
    }

    // =================================================================
    //  الفنيين والراكات
    // =================================================================

    [Fact]
    public async Task Technician_activity_groups_by_code_and_keeps_the_stored_name()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var day = CairoDay.Today.AddDays(-1);
        var at = CairoDay.StartOf(day).AddHours(9);

        NewReport(db, tenant, at, technicianCode: "T001", durationMs: 600_000);
        NewReport(db, tenant, at.AddMinutes(1), technicianCode: "T001", fail: 2,
            durationMs: 1_200_000);
        NewReport(db, tenant, at.AddMinutes(2), technicianCode: "T002");

        await db.SaveChangesAsync();

        var rows = await new AnalyticsRepository(db)
            .TechnicianActivityAsync(tenant, null, Window(day, 1), 10);

        // ⚠️ الأكتر شغلاً الأول.
        Assert.Equal(["T001", "T002"], rows.Select(r => r.Code));

        var top = rows[0];

        Assert.Equal("فني T001", top.Name);
        Assert.Equal(2, top.Reports);
        Assert.Equal(1, top.NeedsReview);
        Assert.Equal(15.0, top.AverageMinutes);
    }

    /// <summary>
    /// ⚠️ <b>الراكات كلها بترجع — واللي ماشتغلتش بصفر.</b>
    /// إخفاؤها بيخلّي «مفيش شغل عليها» و«مش موجودة» شكلهم واحد.
    /// </summary>
    [Fact]
    public async Task An_idle_rack_still_appears_with_zero()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var busy = new Rack
        {
            TenantId = tenant, RackCode = "RK-BUSY", Name = "مشغولة",
            Status = RackStatus.Active, ApiKeyHash = "x", Salt = "y",
        };

        var idle = new Rack
        {
            TenantId = tenant, RackCode = "RK-IDLE", Name = "فاضية",
            Status = RackStatus.PendingPairing, ApiKeyHash = "x", Salt = "y",
        };

        var revoked = new Rack
        {
            TenantId = tenant, RackCode = "RK-GONE", Name = "ملغية",
            Status = RackStatus.Revoked, ApiKeyHash = "x", Salt = "y",
        };

        db.Racks.AddRange(busy, idle, revoked);

        var day = CairoDay.Today.AddDays(-1);
        NewReport(db, tenant, CairoDay.StartOf(day).AddHours(9), rackId: busy.Id);

        await db.SaveChangesAsync();

        var rows = await new AnalyticsRepository(db)
            .RackActivityAsync(tenant, null, Window(day, 1));

        Assert.Equal(["RK-BUSY", "RK-IDLE"], rows.Select(r => r.Code));
        Assert.Equal(1, rows[0].Reports);
        Assert.Equal(0, rows[1].Reports);

        // ⚠️ والملغية مابتظهرش خالص — دي مش «فاضية»، دي مشالة.
        Assert.DoesNotContain("RK-GONE", rows.Select(r => r.Code));
    }

    // =================================================================
    //  الأعطال والمدة
    // =================================================================

    /// <summary>
    /// 🔴 <b>الخطوة اللي ما اشتغلتش (<c>٥</c>) مش فشل
    /// (<c>٢</c>).</b>
    ///
    /// <para>دي مشكلة في الفحص نفسه مش في الجهاز، وخلطهم بيخلّي
    /// «الشاشة بايظة» و«ماقدرناش نفحص الشاشة» رقم واحد.</para>
    /// </summary>
    [Fact]
    public async Task Only_failed_steps_count_as_failures()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var day = CairoDay.Today.AddDays(-1);

        var report = NewReport(db, tenant, CairoDay.StartOf(day).AddHours(9), fail: 1);

        db.Steps.Add(new ReportStep
        { ReportId = report.Id, StepId = "scr", Title = "الشاشة", Status = 2 });

        db.Steps.Add(new ReportStep
        { ReportId = report.Id, StepId = "bat", Title = "البطارية", Status = 5 });

        db.Steps.Add(new ReportStep
        { ReportId = report.Id, StepId = "kbd", Title = "الكيبورد", Status = 1 });

        await db.SaveChangesAsync();

        var rows = await new AnalyticsRepository(db)
            .FailureHotspotsAsync(tenant, null, Window(day, 1), 10);

        Assert.Equal(["الشاشة"], rows.Select(r => r.Name));
    }

    [Fact]
    public async Task Duration_buckets_come_back_in_order_with_zeros_filled()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var day = CairoDay.Today.AddDays(-1);
        var at = CairoDay.StartOf(day).AddHours(9);

        NewReport(db, tenant, at, durationMs: 120_000);             // < ٥
        NewReport(db, tenant, at.AddMinutes(1), durationMs: 420_000);  // ٥–١٠
        NewReport(db, tenant, at.AddMinutes(2), durationMs: 1_800_000); // > ٢٠

        // ⚠️ واللي مالوش مدة مابيدخلش خالص.
        NewReport(db, tenant, at.AddMinutes(3), durationMs: 0);

        await db.SaveChangesAsync();

        var buckets = await new AnalyticsRepository(db)
            .DurationBucketsAsync(tenant, null, Window(day, 1));

        Assert.Equal([1, 1, 0, 1], buckets);
    }

    // =================================================================
    //  جديد مقابل معاد
    // =================================================================

    /// <summary>
    /// 🔴 <b>الجهاز «جديد» لو أول فحص ليه <u>على الإطلاق</u> وقع
    /// جوّه الفترة.</b>
    ///
    /// <para>جهاز اتفحص خمس مرات الشهر ده وأول مرة كانت السنة اللي
    /// فاتت هو «إعادة فحص»، مش خمس أجهزة جديدة.</para>
    /// </summary>
    [Fact]
    public async Task A_device_first_seen_before_the_window_counts_as_retested()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var old = NewDevice(db, tenant, "DV-OLD");
        var fresh = NewDevice(db, tenant, "DV-NEW");

        var windowStart = CairoDay.Today.AddDays(-6);

        // أول فحص للقديم بره الفترة.
        NewReport(db, tenant, CairoDay.StartOf(windowStart.AddDays(-60)).AddHours(9),
            deviceId: old.Id);

        // وبعدين اتفحص مرتين جوّه الفترة.
        NewReport(db, tenant, CairoDay.StartOf(windowStart).AddHours(9), deviceId: old.Id);
        NewReport(db, tenant, CairoDay.StartOf(windowStart).AddHours(10), deviceId: old.Id);

        // والجديد أول فحص ليه جوّه الفترة.
        NewReport(db, tenant, CairoDay.StartOf(windowStart).AddHours(11), deviceId: fresh.Id);

        // وفحص لسه مش مربوط بجهاز.
        NewReport(db, tenant, CairoDay.StartOf(windowStart).AddHours(12), deviceId: null);

        await db.SaveChangesAsync();

        var mix = await new AnalyticsRepository(db).DeviceMixAsync(tenant, null, Recent());

        Assert.Equal(1, mix.NewDevices);
        Assert.Equal(1, mix.Retested);

        // ⚠️ واللي مش مربوط لا جديد ولا معاد.
        Assert.Equal(1, mix.Unlinked);
    }

    // =================================================================
    //  القوايم
    // =================================================================

    [Fact]
    public async Task The_attention_list_carries_only_reports_with_a_problem()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "DV-LIST");

        var rack = new Rack
        {
            TenantId = tenant, RackCode = "RK-7", Name = "راكة سبعة",
            Status = RackStatus.Active, ApiKeyHash = "x", Salt = "y",
        };
        db.Racks.Add(rack);

        var day = CairoDay.Today.AddDays(-1);
        var at = CairoDay.StartOf(day).AddHours(9);

        NewReport(db, tenant, at, deviceId: device.Id, rackId: rack.Id);
        NewReport(db, tenant, at.AddMinutes(1), deviceId: device.Id, rackId: rack.Id, fail: 3);
        NewReport(db, tenant, at.AddMinutes(2), deviceId: device.Id, rackId: rack.Id, error: 1);

        await db.SaveChangesAsync();

        var (attention, recent) = await new AnalyticsRepository(db)
            .ListsAsync(tenant, null, Window(day, 1), 10);

        Assert.Equal(2, attention.Count);
        Assert.Equal(3, recent.Count);

        // 🔴 كود الجهاز المربوط هو الأصل، مش اللي متخزّن على الفحص.
        Assert.All(recent, r => Assert.Equal("DV-LIST", r.DevicePublicCode));

        // ⚠️ وكود الراكة واسمها الاتنين في الصف.
        Assert.All(recent, r => Assert.Equal("RK-7", r.RackCode));
        Assert.All(recent, r => Assert.Equal("راكة سبعة", r.RackName));

        // ⚠️ والأحدث الأول.
        Assert.True(recent[0].StartedAtUtc >= recent[1].StartedAtUtc);
    }

    /// <summary>
    /// ⚠️ الفحص اللي لسه مش مربوط بجهاز بياخد الكود المتخزّن عليه —
    /// ده كل اللي عندنا.
    /// </summary>
    [Fact]
    public async Task An_unlinked_report_falls_back_to_its_stored_device_code()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var day = CairoDay.Today.AddDays(-1);

        NewReport(db, tenant, CairoDay.StartOf(day).AddHours(9), deviceId: null);

        await db.SaveChangesAsync();

        var (_, recent) = await new AnalyticsRepository(db)
            .ListsAsync(tenant, null, Window(day, 1), 10);

        Assert.Equal("RAW", Assert.Single(recent).DevicePublicCode);
        Assert.Equal("", Assert.Single(recent).RackCode);
    }

    // =================================================================
    //  الجرد — النقطة اللي كانت بتقع بـ٥٠٠
    // =================================================================

    /// <summary>
    /// 🔴 <b>الاستعلام ده كان بيوقّع النقطة كلها بـ٥٠٠.</b>
    ///
    /// <para>الترتيب بعد التحويل لنوع خاص مش قابل للترجمة لـSQL،
    /// وEF بيرمي وقت ما يبني الاستعلام — مش وقت القراية. يعني مش
    /// مسألة بيانات: بيقع دايماً، على أي قاعدة، حتى لو مفيش ولا
    /// جهاز اتسلّم.</para>
    ///
    /// <para>⚠️ <b>والنقطة كلها بتروح معاه</b>، مش رقم الجهات بس —
    /// «سليم» و«اتسلّم» و«لم تُفحص» كلهم في نفس الرد. وعشان كده
    /// السؤال كان «فين الأجهزة السليمة».</para>
    /// </summary>
    [Fact]
    public async Task The_inventory_summary_runs_and_groups_by_destination()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var warehouse = new Location { TenantId = tenant, Name = "المخزن", Code = "W" };
        var sales = new Location { TenantId = tenant, Name = "المبيعات", Code = "S" };
        db.Locations.AddRange(warehouse, sales);

        var clean = NewDevice(db, tenant, "DV-CLEAN");
        var broken = NewDevice(db, tenant, "DV-BROKEN");
        var handed = NewDevice(db, tenant, "DV-HANDED");
        var untested = NewDevice(db, tenant, "DV-UNTESTED");

        await db.SaveChangesAsync();

        handed.CurrentLocationId = warehouse.Id;
        broken.CurrentLocationId = sales.Id;

        var at = DateTime.UtcNow.AddDays(-1);

        NewReport(db, tenant, at, deviceId: clean.Id);
        NewReport(db, tenant, at, deviceId: handed.Id);
        NewReport(db, tenant, at, deviceId: broken.Id, fail: 2);

        await db.SaveChangesAsync();

        var summary = await new AnalyticsRepository(db).InventoryAsync(tenant);

        Assert.Equal(4, summary.Total);
        Assert.Equal(2, summary.Healthy);
        Assert.Equal(1, summary.NeedsAttention);
        Assert.Equal(1, summary.NeverTested);
        Assert.Equal(2, summary.HandedOver);

        // ⚠️ استعلام مستقل مش طرح: السليم اللي لسه مااتسلّمش.
        Assert.Equal(1, summary.HealthyNotHandedOver);

        Assert.Equal(["المخزن", "المبيعات"],
            summary.ByDestination.Select(d => d.Name).OrderByDescending(n => n == "المخزن"));
    }

    /// <summary>
    /// 🔴 «سليم» = <b>آخر</b> فحص مفيهوش فشل ولا خطأ — نفس تعريف
    /// شاشة التسليم بالحرف، عشان الرقمين يطابقوا بعض.
    /// </summary>
    [Fact]
    public async Task Healthy_is_decided_by_the_latest_test_not_by_any_test()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "DV-FIXED");

        await db.SaveChangesAsync();

        // اتعطل الأول، وبعدين اتصلّح.
        NewReport(db, tenant, DateTime.UtcNow.AddDays(-5), deviceId: device.Id, fail: 3);
        NewReport(db, tenant, DateTime.UtcNow.AddDays(-1), deviceId: device.Id);

        await db.SaveChangesAsync();

        var summary = await new AnalyticsRepository(db).InventoryAsync(tenant);

        Assert.Equal(1, summary.Healthy);
        Assert.Equal(0, summary.NeedsAttention);
    }

    /// <summary>
    /// 🔴 <b>والاتجاه التاني هو الخطر — والمسخ أثبت إن الفحص اللي
    /// فوق مش بيغطّيه.</b>
    ///
    /// <para>لاب اتفحص نضيف وبعدين باظ. لو «سليم» اتحسب بأي فحص بدل
    /// <b>آخر</b> فحص، اللاب ده بيبان سليم — <b>وينفع يتسلّم</b>.
    /// وده بالظبط اللي شرط التسليم موجود عشان يمنعه.</para>
    ///
    /// <para>⚠️ الفحص اللي فوق (باظ وبعدين اتصلّح) بيعدّي على
    /// الاتنين: أقل قيمة في المجموعة صفر، وآخر فحص صفر كمان.</para>
    /// </summary>
    [Fact]
    public async Task A_device_that_broke_after_a_clean_test_is_not_healthy()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "DV-BROKE-LATER");

        await db.SaveChangesAsync();

        // اتفحص نضيف الأول، وبعدين باظ.
        NewReport(db, tenant, DateTime.UtcNow.AddDays(-5), deviceId: device.Id);
        NewReport(db, tenant, DateTime.UtcNow.AddDays(-1), deviceId: device.Id, fail: 2);

        await db.SaveChangesAsync();

        var summary = await new AnalyticsRepository(db).InventoryAsync(tenant);

        Assert.Equal(0, summary.Healthy);
        Assert.Equal(1, summary.NeedsAttention);
        Assert.Equal(0, summary.HealthyNotHandedOver);
    }

    /// <summary>
    /// ⚠️ <b>النشط بس:</b> المدموج صفه بيفضل للتاريخ، وعدّه بيقول ١٠
    /// لابات والحقيقة ٧ — نفس الغلط اللي الدمج اتعمل عشان يصلّحه.
    /// </summary>
    [Fact]
    public async Task A_merged_device_is_out_of_the_inventory()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        NewDevice(db, tenant, "DV-LIVE");

        var merged = NewDevice(db, tenant, "DV-MERGED");
        merged.Status = DeviceLifecycleStatus.Merged;

        await db.SaveChangesAsync();

        var summary = await new AnalyticsRepository(db).InventoryAsync(tenant);

        Assert.Equal(1, summary.Total);
    }

    // =================================================================
    //  التحذيرات وقطع الغيار
    // =================================================================

    [Fact]
    public async Task The_alert_counters_add_up_to_the_total()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var changed = NewDevice(db, tenant, "DV-PART");
        changed.PartChangedAtUtc = DateTime.UtcNow;

        var duplicate = NewDevice(db, tenant, "DV-DUP");
        duplicate.Status = DeviceLifecycleStatus.DuplicateSuspected;

        await db.SaveChangesAsync();

        var unresolved = NewReport(db, tenant, DateTime.UtcNow.AddHours(-1));
        unresolved.NeedsDeviceResolution = true;

        await db.SaveChangesAsync();

        var alerts = await new AnalyticsRepository(db).AlertsAsync(tenant);

        Assert.Equal(1, alerts.PartChanged);
        Assert.Equal(1, alerts.DuplicateSuspected);
        Assert.Equal(1, alerts.NeedsDeviceResolution);
        Assert.Equal(3, alerts.Total);
    }

    /// <summary>
    /// ⚠️ <b>الفترة بتتقاس على فتح أمر الصيانة</b> مش على وقت تركيب
    /// القطعة — القطعة مالهاش تاريخ خاص بيها في الكيان.
    /// </summary>
    [Fact]
    public async Task Fitted_parts_are_filtered_by_when_the_repair_order_opened()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "DV-PARTS");

        await db.SaveChangesAsync();

        var inWindow = new RepairWorkItem
        {
            TenantId = tenant, DeviceId = device.Id, PublicCode = "RP-IN",
            OpenedAtUtc = CairoDay.StartOf(CairoDay.Today.AddDays(-2)).AddHours(9),
            OpenedByActorType = "User", OpenedByName = "كريم",
        };

        var outside = new RepairWorkItem
        {
            TenantId = tenant, DeviceId = device.Id, PublicCode = "RP-OUT",
            OpenedAtUtc = CairoDay.StartOf(CairoDay.Today.AddDays(-90)).AddHours(9),
            OpenedByActorType = "User", OpenedByName = "كريم",
        };

        db.RepairWorkItems.AddRange(inWindow, outside);

        await db.SaveChangesAsync();

        db.RepairParts.Add(new RepairPart
        { TenantId = tenant, WorkItemId = inWindow.Id, Name = "شاشة", Quantity = 1 });

        db.RepairParts.Add(new RepairPart
        { TenantId = tenant, WorkItemId = outside.Id, Name = "بطارية", Quantity = 1 });

        await db.SaveChangesAsync();

        var rows = await new AnalyticsRepository(db).FittedPartsAsync(tenant, Recent());

        Assert.Equal(["شاشة"], rows.Select(r => r.Name));
        Assert.Equal(device.Id, Assert.Single(rows).DeviceId);
    }
}
