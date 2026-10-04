using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Codlek.Core.Text;
using Codlek.Infrastructure.Data;
using Codlek.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Codlek.Tests;

/// <summary>
/// قايمة الأجهزة — <b>على قاعدة حقيقية</b>.
///
/// <para>🔴 <b>وأهم حاجة الملف ده بيثبّتها: القايمة والتصدير
/// بيستعملوا <u>نفس</u> الفلتر.</b> الزرار في الواجهة مكتوب فوقه
/// «اللي مفلتر على الشاشة مفلتر في الإكسل»، وفي القديم ماكانش صح —
/// تلات فلاتر (التحذيرات والتسليم والجهة) كانوا ناقصين من نسخة
/// التصدير، فالمدير يفلتر على «مخزن الجاهز» ويصدّر فيطلعله
/// <b>كل</b> الأجهزة.</para>
/// </summary>
public class DeviceListRepositoryTests(DeviceListDbFixture fixture)
    : IClassFixture<DeviceListDbFixture>
{
    private static Guid NewTenant(AppDbContext db)
    {
        var tenant = new Tenant { Name = "ورشة " + Guid.NewGuid().ToString("N")[..6] };
        db.Tenants.Add(tenant);
        return tenant.Id;
    }

    private static Device NewDevice(
        AppDbContext db, Guid tenantId, string code,
        DeviceLifecycleStatus status = DeviceLifecycleStatus.Active,
        DeviceIdentityConfidence confidence = DeviceIdentityConfidence.A,
        DeviceOperationalStage stage = DeviceOperationalStage.Tested,
        string manufacturer = "HP",
        string model = "6470b",
        DateTime? lastSeenAtUtc = null,
        DateTime? stageChangedAtUtc = null)
    {
        var row = new Device
        {
            TenantId = tenantId,
            PublicCode = code,
            LastKnownManufacturer = manufacturer,
            LastKnownModel = model,
            SearchText = ArabicText.Combine(code, manufacturer, model),
            Status = status,
            Confidence = confidence,
            OperationalStage = stage,
            StageChangedAtUtc = stageChangedAtUtc,
            LastSeenAtUtc = lastSeenAtUtc ?? DateTime.UtcNow,
        };
        db.Devices.Add(row);
        return row;
    }

    private static Report NewReport(
        AppDbContext db, Guid tenantId, Guid deviceId, DateTime startedAtUtc,
        int fail = 0, int error = 0, string technicianCode = "T001", Guid? rackId = null)
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
            FailCount = fail,
            ErrorCount = error,
            SourceRackId = rackId,
        };
        db.Reports.Add(row);
        return row;
    }

    private static DeviceListFilter Any => new();

    private static async Task<List<string>> Codes(
        DeviceRepository repo, Guid tenantId, DeviceListFilter filter)
    {
        var (rows, _) = await repo.ListAsync(tenantId, filter);

        return rows.Select(r => r.PublicCode).OrderBy(c => c, StringComparer.Ordinal).ToList();
    }

    // =================================================================
    //  المدموج
    // =================================================================

    /// <summary>
    /// 🔴 <b>القايمة الافتراضية من غير المدموجين.</b>
    ///
    /// <para>الجهاز المدموج مش لاب مستقل: صفه بيفضل عشان التاريخ
    /// والكود المتقاعد، بس عرضه بيخلّي العدّ يقول ١٠ لابات والحقيقة
    /// ٧ — نفس الغلط اللي الدمج اتعمل عشان يصلّحه.</para>
    /// </summary>
    [Fact]
    public async Task A_merged_device_is_out_of_the_default_list()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        NewDevice(db, tenant, "DV-LIVE");
        NewDevice(db, tenant, "DV-MERGED", status: DeviceLifecycleStatus.Merged);

        await db.SaveChangesAsync();

        var repo = new DeviceRepository(db);

        Assert.Equal(["DV-LIVE"], await Codes(repo, tenant, Any));
    }

    /// <summary>
    /// 🔴 <b>بس البحث بالكود المتقاعد لازم يفضل شغّال.</b>
    ///
    /// <para>الكود ده مطبوع على ليبل ملزوق على لاب حقيقي، والفني
    /// اللي بيدوّر بيه لازم يوصل للجهاز الكانوني مش لصفحة
    /// فاضية.</para>
    /// </summary>
    [Fact]
    public async Task Searching_finds_a_merged_device_by_its_retired_code()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var canonical = NewDevice(db, tenant, "DV-CANON");
        var merged = NewDevice(db, tenant, "DV-OLDLABEL", status: DeviceLifecycleStatus.Merged);

        await db.SaveChangesAsync();

        merged.MergedIntoDeviceId = canonical.Id;
        await db.SaveChangesAsync();

        var repo = new DeviceRepository(db);

        var filter = new DeviceListFilter
        {
            IncludeMerged = true,
            ExactCode = "DV-OLDLABEL",
            IdentityValue = IdentityValues.Normalize("DV-OLDLABEL"),
            SearchPattern = SearchPattern.Contains(ArabicText.Normalize("DV-OLDLABEL")),
        };

        var (rows, _) = await repo.ListAsync(tenant, filter);

        var row = Assert.Single(rows);

        Assert.Equal("DV-OLDLABEL", row.PublicCode);

        // 🔴 والكود الكانوني في الصف — عشان الليبل يوصّل للاب الحقيقي.
        Assert.Equal("DV-CANON", row.MergedIntoCode);
    }

    /// <summary>
    /// ⚠️ وطلب الحالة صراحةً بيشيل الاستبعاد كمان.
    /// </summary>
    [Fact]
    public async Task Asking_for_the_merged_status_shows_them()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        NewDevice(db, tenant, "DV-A");
        NewDevice(db, tenant, "DV-M", status: DeviceLifecycleStatus.Merged);

        await db.SaveChangesAsync();

        var codes = await Codes(new DeviceRepository(db), tenant, new DeviceListFilter
        {
            IncludeMerged = true,
            Status = DeviceLifecycleStatus.Merged,
        });

        Assert.Equal(["DV-M"], codes);
    }

    // =================================================================
    //  البحث بتلات طرق
    // =================================================================

    /// <summary>
    /// 🔴 <b>الفني بيكتب اللي شايفه: كود اللاب، أو سيريال من ستيكر
    /// المصنع، أو اسم موديل.</b>
    ///
    /// <para>ولو واحدة منهم ناقصة، هو بيدوّر ومابيلاقيش ويفتكر إن
    /// اللاب مش مسجّل.</para>
    /// </summary>
    [Fact]
    public async Task Search_matches_the_code_the_serial_anchor_and_the_text()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var byCode = NewDevice(db, tenant, "DV-CODE");
        var bySerial = NewDevice(db, tenant, "DV-SERIAL");
        var byText = NewDevice(db, tenant, "DV-TEXT", manufacturer: "Dell", model: "E7450");

        byText.SearchText = ArabicText.Combine("DV-TEXT", "Dell", "لابتوب ديل");

        await db.SaveChangesAsync();

        db.DeviceIdentifiers.Add(new DeviceIdentifierRow
        {
            TenantId = tenant,
            DeviceId = bySerial.Id,
            Kind = DeviceIdentifierKind.BiosSerial,
            RawValue = "cnu-123 456",
            NormalizedValue = IdentityValues.Normalize("cnu-123 456"),
            IsActive = true,
        });

        await db.SaveChangesAsync();

        var repo = new DeviceRepository(db);

        async Task<List<string>> Find(string term) => await Codes(repo, tenant,
            new DeviceListFilter
            {
                IncludeMerged = true,
                ExactCode = term,
                IdentityValue = IdentityValues.Normalize(term),
                SearchPattern = SearchPattern.Contains(ArabicText.Normalize(term)),
            });

        Assert.Equal(["DV-CODE"], await Find("DV-CODE"));

        // ⚠️ السيريال بحالة أحرف ومساحات مختلفة — التوحيد التقني هو
        // اللي بيخلّيه يلاقي.
        Assert.Equal(["DV-SERIAL"], await Find("CNU-123   456"));

        Assert.Equal(["DV-TEXT"], await Find("ديل"));
    }

    /// <summary>
    /// ⚠️ ومرساة <b>ملغية</b> مابتلاقيش — هي جزء من التاريخ، مش
    /// هوية حالية.
    /// </summary>
    [Fact]
    public async Task A_retired_identity_anchor_does_not_match()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var device = NewDevice(db, tenant, "DV-RETIRED-ANCHOR");

        await db.SaveChangesAsync();

        db.DeviceIdentifiers.Add(new DeviceIdentifierRow
        {
            TenantId = tenant,
            DeviceId = device.Id,
            Kind = DeviceIdentifierKind.BiosSerial,
            RawValue = "OLD-SERIAL",
            NormalizedValue = IdentityValues.Normalize("OLD-SERIAL"),
            IsActive = false,
        });

        await db.SaveChangesAsync();

        var codes = await Codes(new DeviceRepository(db), tenant, new DeviceListFilter
        {
            IncludeMerged = true,
            ExactCode = "OLD-SERIAL",
            IdentityValue = IdentityValues.Normalize("OLD-SERIAL"),
            SearchPattern = SearchPattern.Contains(ArabicText.Normalize("OLD-SERIAL")),
        });

        Assert.Empty(codes);
    }

    // =================================================================
    //  «آخر فحص» مقابل «أي فحص»
    // =================================================================

    /// <summary>
    /// 🔴 <b>فلتر النتيجة بيقيس <u>آخر</u> فحص.</b>
    ///
    /// <para>لاب باظ الشهر اللي فات واتصلّح النهاردة <b>مش</b> في
    /// «فيه مشكلة»؛ ولاب اتفحص نضيف وبعدين باظ مش «سليم». وده نفس
    /// تعريف شاشة التسليم بالحرف.</para>
    /// </summary>
    [Fact]
    public async Task The_outcome_filter_reads_the_latest_test_only()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var fixedUp = NewDevice(db, tenant, "DV-FIXED");
        var brokeLater = NewDevice(db, tenant, "DV-BROKE");
        var never = NewDevice(db, tenant, "DV-NEVER");

        await db.SaveChangesAsync();

        // باظ وبعدين اتصلّح.
        NewReport(db, tenant, fixedUp.Id, DateTime.UtcNow.AddDays(-5), fail: 3);
        NewReport(db, tenant, fixedUp.Id, DateTime.UtcNow.AddDays(-1));

        // اتفحص نضيف وبعدين باظ.
        NewReport(db, tenant, brokeLater.Id, DateTime.UtcNow.AddDays(-5));
        NewReport(db, tenant, brokeLater.Id, DateTime.UtcNow.AddDays(-1), fail: 2);

        await db.SaveChangesAsync();

        var repo = new DeviceRepository(db);

        Assert.Equal(["DV-FIXED"],
            await Codes(repo, tenant, Any with { Outcome = DeviceOutcomeFilter.Clean }));

        Assert.Equal(["DV-BROKE"],
            await Codes(repo, tenant, Any with { Outcome = DeviceOutcomeFilter.Failures }));

        // ⚠️ واللي عمره ما اتفحص مش «سليم» — مفيش فحص ≠ سليم.
        Assert.Equal(["DV-NEVER"],
            await Codes(repo, tenant, Any with { Outcome = DeviceOutcomeFilter.NeverTested }));

        Assert.Equal(3, (await repo.ListAsync(tenant, Any)).TotalItems);

        // وعدم تجاهل أخطاء القراءة.
        Assert.Empty(await Codes(repo, tenant, Any with { Outcome = DeviceOutcomeFilter.Errors }));
    }

    /// <summary>
    /// ⚠️ وباقي الفلاتر على <b>أي</b> فحص — «اتفحص بالراكة دي»
    /// حقيقة تاريخية مش حالة قايمة.
    /// </summary>
    [Fact]
    public async Task The_technician_and_rack_filters_match_any_test_in_history()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var rack = new Rack
        {
            TenantId = tenant, RackCode = "RK-1", Name = "راكة",
            Location = "الدور التاني", ApiKeyHash = "x", Salt = "y",
        };
        db.Racks.Add(rack);

        var device = NewDevice(db, tenant, "DV-HIST");
        var other = NewDevice(db, tenant, "DV-OTHER");

        await db.SaveChangesAsync();

        // فحص قديم بالفني والراكة، وفحص أحدث من غيرهم.
        NewReport(db, tenant, device.Id, DateTime.UtcNow.AddDays(-9),
            technicianCode: "T777", rackId: rack.Id);

        NewReport(db, tenant, device.Id, DateTime.UtcNow.AddDays(-1),
            technicianCode: "T001");

        NewReport(db, tenant, other.Id, DateTime.UtcNow.AddDays(-1), technicianCode: "T001");

        await db.SaveChangesAsync();

        var repo = new DeviceRepository(db);

        Assert.Equal(["DV-HIST"],
            await Codes(repo, tenant, Any with { TechnicianCode = "T777" }));

        Assert.Equal(["DV-HIST"],
            await Codes(repo, tenant, Any with { RackId = rack.Id }));
    }

    // =================================================================
    //  التلات فلاتر اللي كانوا ناقصين من التصدير
    // =================================================================

    /// <summary>
    /// 🔴 <b>الجرس بيعدّ تلات أسباب، والسيرفر كان بيفلتر واحد.</b>
    ///
    /// <para>كان فيه «قطعة اتغيّرت» وبس، فحتى لما الرابط اتصلّح كان
    /// سببين من التلاتة مالهمش طريق — وتحذير مالوش طريق للأجهزة اللي
    /// بيتكلم عنها مجرد إزعاج.</para>
    /// </summary>
    [Fact]
    public async Task The_attention_flag_covers_all_three_bell_reasons()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var partChanged = NewDevice(db, tenant, "DV-PART");
        partChanged.PartChangedAtUtc = DateTime.UtcNow;
        partChanged.PartChangeSummary = "الهارد اتغيّر";

        NewDevice(db, tenant, "DV-DUP", status: DeviceLifecycleStatus.DuplicateSuspected);
        NewDevice(db, tenant, "DV-FINE");

        await db.SaveChangesAsync();

        var repo = new DeviceRepository(db);

        Assert.Equal(["DV-PART"],
            await Codes(repo, tenant, Any with { Flag = DeviceAttentionFlag.PartChanged }));

        Assert.Equal(["DV-DUP"],
            await Codes(repo, tenant,
                Any with { Flag = DeviceAttentionFlag.DuplicateSuspected }));

        // 🔴 ودي اللي الجرس بيبعتها — الاتنين مع بعض.
        Assert.Equal(["DV-DUP", "DV-PART"],
            await Codes(repo, tenant, Any with { Flag = DeviceAttentionFlag.Attention }));
    }

    [Fact]
    public async Task The_handover_filter_reads_the_current_location()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var warehouse = new Location { TenantId = tenant, Name = "المخزن", Code = "W" };
        var sales = new Location { TenantId = tenant, Name = "المبيعات", Code = "S" };
        db.Locations.AddRange(warehouse, sales);

        var inWarehouse = NewDevice(db, tenant, "DV-WH");
        var inSales = NewDevice(db, tenant, "DV-SL");
        NewDevice(db, tenant, "DV-HERE");

        await db.SaveChangesAsync();

        inWarehouse.CurrentLocationId = warehouse.Id;
        inSales.CurrentLocationId = sales.Id;

        await db.SaveChangesAsync();

        var repo = new DeviceRepository(db);

        Assert.Equal(["DV-SL", "DV-WH"],
            await Codes(repo, tenant, Any with { Handover = DeviceHandoverFilter.HandedOver }));

        Assert.Equal(["DV-HERE"],
            await Codes(repo, tenant, Any with { Handover = DeviceHandoverFilter.InWorkshop }));

        // 🔴 وفلتر الجهة بيسأل «فين بالظبط» — سؤال تاني.
        Assert.Equal(["DV-WH"],
            await Codes(repo, tenant, Any with { LocationId = warehouse.Id }));

        // ⚠️ والاتنين بيتجمّعوا.
        Assert.Equal(["DV-WH"], await Codes(repo, tenant, Any with
        {
            Handover = DeviceHandoverFilter.HandedOver,
            LocationId = warehouse.Id,
        }));
    }

    /// <summary>
    /// 🔴 <b>وده الفحص اللي بيثبّت إن التصدير مفلتر زي الشاشة.</b>
    ///
    /// <para>نفس الكائن بيروح للاتنين — فلو فلتر اتزاد للقايمة
    /// ومااتزادش للتصدير، الفحص ده بيقع.</para>
    /// </summary>
    [Fact]
    public async Task The_export_applies_exactly_the_same_filter_as_the_list()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var warehouse = new Location { TenantId = tenant, Name = "المخزن", Code = "W" };
        db.Locations.Add(warehouse);

        var wanted = NewDevice(db, tenant, "DV-WANTED");
        NewDevice(db, tenant, "DV-NOISE-1");
        NewDevice(db, tenant, "DV-NOISE-2", status: DeviceLifecycleStatus.DuplicateSuspected);

        await db.SaveChangesAsync();

        wanted.CurrentLocationId = warehouse.Id;
        wanted.PartChangedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync();

        var repo = new DeviceRepository(db);

        var filter = Any with
        {
            Flag = DeviceAttentionFlag.PartChanged,
            Handover = DeviceHandoverFilter.HandedOver,
            LocationId = warehouse.Id,
        };

        var (rows, total) = await repo.ListAsync(tenant, filter);
        var exported = await repo.ExportAsync(tenant, filter, cap: 1000);

        Assert.Equal(1, total);
        Assert.Equal(["DV-WANTED"], rows.Select(r => r.PublicCode));
        Assert.Equal(["DV-WANTED"], exported.Select(r => r.PublicCode));

        // ⚠️ والجهة عمود في الملف — المدير اللي بيصدّر محتاج يشوفها.
        Assert.Equal("المخزن", Assert.Single(exported).LocationName);
    }

    /// <summary>
    /// ⚠️ والتصدير بيجيب <c>cap + 1</c> عشان المنادي يعرف إن فيه
    /// زيادة ويقولها في الملف — القص الصامت بيتقري على إنه كل
    /// البيانات.
    /// </summary>
    [Fact]
    public async Task The_export_returns_one_row_past_the_cap_so_truncation_is_visible()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        for (int i = 0; i < 6; i++) NewDevice(db, tenant, $"DV-{i:D2}");

        await db.SaveChangesAsync();

        var exported = await new DeviceRepository(db).ExportAsync(tenant, Any, cap: 3);

        Assert.Equal(4, exported.Count);
    }

    // =================================================================
    //  الترتيب
    // =================================================================

    /// <summary>
    /// 🔴 <b>«الأقدم الأول» على السيرفر، والفاضي آخر الطابور.</b>
    ///
    /// <para>صاحب الشغل طلب يشوف اللاب اللي واقف من زمان. ولو
    /// الترتيب اتعمل في الواجهة، هيرتّب <b>الصفحة المعروضة</b> بس —
    /// يعني أقدم لاب في الورشة ممكن يكون في صفحة ٤ وعمره ما يطلع
    /// فوق.</para>
    ///
    /// <para>⚠️ و«آخر تغيير مرحلة» الفاضية معناها «مش معروف» —
    /// تقديمها على لاب واقف فعلاً من أسبوعين بيدفن اللي إحنا بندوّر
    /// عليه.</para>
    /// </summary>
    [Fact]
    public async Task Oldest_stage_first_puts_unknown_last()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        NewDevice(db, tenant, "DV-UNKNOWN", stageChangedAtUtc: null);

        NewDevice(db, tenant, "DV-OLD",
            stageChangedAtUtc: DateTime.UtcNow.AddDays(-30));

        NewDevice(db, tenant, "DV-RECENT",
            stageChangedAtUtc: DateTime.UtcNow.AddHours(-2));

        await db.SaveChangesAsync();

        var (rows, _) = await new DeviceRepository(db)
            .ListAsync(tenant, Any with { OldestStageFirst = true });

        Assert.Equal(["DV-OLD", "DV-RECENT", "DV-UNKNOWN"], rows.Select(r => r.PublicCode));
    }

    [Fact]
    public async Task The_default_order_is_most_recently_seen_first()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        NewDevice(db, tenant, "DV-STALE", lastSeenAtUtc: DateTime.UtcNow.AddDays(-9));
        NewDevice(db, tenant, "DV-FRESH", lastSeenAtUtc: DateTime.UtcNow);

        await db.SaveChangesAsync();

        var (rows, _) = await new DeviceRepository(db).ListAsync(tenant, Any);

        Assert.Equal(["DV-FRESH", "DV-STALE"], rows.Select(r => r.PublicCode));
    }

    /// <summary>
    /// 🔴 وفاصل التعادل إجباري: دفعة مزامنة بتخلّي «آخر ظهور»
    /// متساوي لعشرين لاب، ومن غير الفاصل الصف بيتكرر بين الصفحات.
    /// </summary>
    [Fact]
    public async Task Paging_never_duplicates_rows_that_share_a_last_seen_time()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var moment = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i < 8; i++) NewDevice(db, tenant, $"DV-T{i}", lastSeenAtUtc: moment);

        await db.SaveChangesAsync();

        var repo = new DeviceRepository(db);

        var first = await repo.ListAsync(tenant, Any with { Page = 1, PageSize = 4 });
        var second = await repo.ListAsync(tenant, Any with { Page = 2, PageSize = 4 });

        var seen = first.Rows.Concat(second.Rows).Select(r => r.PublicCode).ToList();

        Assert.Equal(8, seen.Count);
        Assert.Equal(8, seen.Distinct().Count());
    }

    /// <summary>
    /// 🔴 <b>وده الفحص اللي بيقفل الباب فعلاً.</b>
    ///
    /// <para>الفحص اللي فوق <b>مابيلقطش</b> شيل <c>ThenBy(Id)</c> —
    /// جرّبناه: شلنا الفاصل والتمنية فضلوا طالعين صح. السبب إن SQL
    /// Server بيستعمل نفس الخطة في الاستعلامين لما الصفوف قليلة،
    /// فالترتيب بيطلع ثابت <b>بالعرض</b> مش بالعقد. (ونفس الحكاية
    /// بالظبط حصلت في قايمة الصيانة.)</para>
    ///
    /// <para>⚠️ فالفحص ده بيقرا جملة <c>ORDER BY</c> المولّدة ويتأكد
    /// إن فيها عمود <b>فريد</b> — على الفرعين: الافتراضي وترتيب
    /// «الأقدم».</para>
    /// </summary>
    [Fact]
    public async Task Both_orderings_end_with_a_unique_column()
    {
        var sql = new List<string>();

        using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(fixture.ConnectionString)
                .LogTo(sql.Add, [DbLoggerCategory.Database.Command.Name], LogLevel.Information)
                .Options);

        var tenant = NewTenant(db);
        NewDevice(db, tenant, "DV-SQL");

        await db.SaveChangesAsync();

        var repo = new DeviceRepository(db);

        await repo.ListAsync(tenant, Any);
        await repo.ListAsync(tenant, Any with { OldestStageFirst = true });
        await repo.ExportAsync(tenant, Any, cap: 10);

        /*
          🔴 **آخر `ORDER BY` في الجملة، مش أول واحد.**

          إسقاط الصف فيه استعلامات فرعية ليها ترتيبها الخاص («آخر
          فحص» بيرتّب بوقت البداية) — ودي بتظهر **قبل** الترتيب
          الخارجي في نص SQL. ونسخة أولى من الفحص ده قطعت من **أول**
          `ORDER BY`، فالنص اللي اتفحص كان بيشمل الاستعلامات الفرعية
          و`[Id]` بيلاقي فيها — فالفحص كان بيعدّي على المسخ.
        */
        var ordered = sql
            .Where(line => line.Contains("ORDER BY", StringComparison.Ordinal))
            .Select(line => line[line.LastIndexOf("ORDER BY", StringComparison.Ordinal)..])
            .ToList();

        // ⚠️ تلات استعلامات بترتيب: الافتراضي، الأقدم، والتصدير.
        Assert.Equal(3, ordered.Count);

        Assert.All(ordered, clause =>
            Assert.Contains("[Id]", clause, StringComparison.Ordinal));
    }

    // =================================================================
    //  أعمدة الصف
    // =================================================================

    [Fact]
    public async Task The_row_carries_the_last_test_the_container_and_the_whereabouts_ids()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var container = new ImportContainer
        {
            TenantId = tenant, Code = "C-9", NormalizedCode = "C9",
            Name = "شحنة", CreatedByName = "كريم",
        };
        db.Containers.Add(container);

        var location = new Location { TenantId = tenant, Name = "المخزن", Code = "W" };
        db.Locations.Add(location);

        var rack = new Rack
        {
            TenantId = tenant, RackCode = "RK-5", Name = "راكة",
            Location = "الدور التالت", ApiKeyHash = "x", Salt = "y",
        };
        db.Racks.Add(rack);

        var tech = new Technician
        {
            TenantId = tenant, DisplayName = "محمود", Code = "T555",
            Username = "m555", NormalizedUsername = "m555", IsActive = true,
        };
        db.Technicians.Add(tech);

        var device = NewDevice(db, tenant, "DV-FULL");
        device.CommercialModelName = "ProBook 6470b";
        device.MachineType = "Notebook";

        await db.SaveChangesAsync();

        device.ContainerId = container.Id;
        device.CurrentLocationId = location.Id;
        device.CurrentHolderTechnicianId = tech.Id;

        NewReport(db, tenant, device.Id, DateTime.UtcNow.AddDays(-2), fail: 1, rackId: rack.Id);
        NewReport(db, tenant, device.Id, DateTime.UtcNow.AddDays(-1), rackId: rack.Id);

        await db.SaveChangesAsync();

        var repo = new DeviceRepository(db);

        var (rows, _) = await repo.ListAsync(tenant, Any);
        var row = Assert.Single(rows);

        Assert.Equal("C-9", row.ContainerCode);
        Assert.Equal("ProBook 6470b", row.CommercialModelName);
        Assert.Equal("Notebook", row.MachineType);
        Assert.Equal(2, row.TestCount);

        // ⚠️ آخر فحص = الأحدث، واللي مفيهوش فشل.
        Assert.NotNull(row.Last);
        Assert.Equal(0, row.Last.FailCount);
        Assert.Equal(rack.Id, row.Last.SourceRackId);

        // والأسماء بتتجاب بقرايات مجمّعة.
        var places = await repo.LocationNamesAsync(tenant, [row.CurrentLocationId]);
        var holders = await repo.HolderNamesAsync(tenant, [row.CurrentHolderTechnicianId]);
        var codes = await repo.RackCodesAsync(tenant, [row.Last.SourceRackId]);
        var floors = await repo.RackLocationsAsync(tenant, [row.Last.SourceRackId]);

        Assert.Equal("المخزن", places[location.Id]);
        Assert.Equal(new TechnicianLabel("محمود", "T555"), holders[tech.Id]);
        Assert.Equal("RK-5", codes[rack.Id]);

        // ⚠️ ومكان الراكة بيجاوب «اتفحص في أنهي دور» — سؤال تاني.
        Assert.Equal("الدور التالت", floors[rack.Id]);
    }

    /// <summary>
    /// ⚠️ والقرايات المجمّعة بترجّع قاموس فاضي للقايمة الفاضية —
    /// مش بتضرب القاعدة على ولا حاجة.
    /// </summary>
    [Fact]
    public async Task The_label_reads_skip_the_database_when_there_is_nothing_to_look_up()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var repo = new DeviceRepository(db);

        Assert.Empty(await repo.LocationNamesAsync(tenant, [null, null]));
        Assert.Empty(await repo.HolderNamesAsync(tenant, []));
        Assert.Empty(await repo.RackCodesAsync(tenant, [null]));
        Assert.Empty(await repo.RackLocationsAsync(tenant, []));
    }

    [Fact]
    public async Task Another_tenant_is_invisible()
    {
        using var db = fixture.Create();
        var mine = NewTenant(db);
        var theirs = NewTenant(db);

        NewDevice(db, theirs, "DV-THEIRS");

        await db.SaveChangesAsync();

        var repo = new DeviceRepository(db);

        Assert.Empty(await Codes(repo, mine, Any with { IncludeMerged = true }));
        Assert.Null(await repo.FindByCodeAsync(mine, "DV-THEIRS"));
        Assert.NotNull(await repo.FindByCodeAsync(theirs, "DV-THEIRS"));
    }
}
