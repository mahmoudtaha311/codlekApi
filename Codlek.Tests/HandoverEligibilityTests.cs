using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Codlek.Core.Handover;
using Codlek.Core.Text;
using Codlek.Infrastructure.Data;
using Codlek.Infrastructure.Repositories;

namespace Codlek.Tests;

/// <summary>
/// شرط أهلية التسليم — <b>على قاعدة حقيقية</b>.
///
/// <para>🔴 <b>العيب اللي الشرط ده اتعمل عشانه:</b> القايمة كانت
/// بتبص على <b>نتيجة آخر فحص وبس</b>. يعني لاب عليه أمر صيانة
/// مفتوح، أو لاب في إيد فني دلوقتي، كان بيظهر «جاهز للتسليم» لو
/// آخر فحص عليه كان نضيف — <b>ولاب تحت التصليح كان ممكن يروح
/// المخزن</b>.</para>
///
/// <para>🔴 <b>والشرط كان متكتوب مرتين بنصّين مختلفين</b> (القايمة
/// والتحقّق وقت التسليم)، وواحدة منهم ناقصة الصيانة والفني الحائز.
/// فالفحوص دي بتقيس الاتنين من نفس المكان.</para>
/// </summary>
public class HandoverEligibilityTests(HandoverDbFixture fixture)
    : IClassFixture<HandoverDbFixture>
{
    private static Guid NewTenant(AppDbContext db)
    {
        var tenant = new Tenant { Name = "ورشة " + Guid.NewGuid().ToString("N")[..6] };
        db.Tenants.Add(tenant);
        return tenant.Id;
    }

    /// <summary>
    /// لاب <b>مؤهّل</b>: اتفحص ونضيف، مفيش صيانة، مش مع فني،
    /// ومرحلته سليمة.
    /// </summary>
    private static Device NewDevice(
        AppDbContext db, Guid tenantId, string code,
        int failCount = 0, int errorCount = 0, bool withReport = true,
        DeviceOperationalStage stage = DeviceOperationalStage.Ready,
        Guid? holder = null, bool reviewed = true,
        DeviceLifecycleStatus status = DeviceLifecycleStatus.Active)
    {
        var device = new Device
        {
            TenantId = tenantId,
            PublicCode = code,
            LastKnownManufacturer = "HP",
            LastKnownModel = "6470b",
            SearchText = ArabicText.Combine(code, "HP", "6470b"),
            OperationalStage = stage,
            CurrentHolderTechnicianId = holder,
            Status = status,
            LastSeenAtUtc = DateTime.UtcNow,
            ReadyForHandoverAtUtc = reviewed ? DateTime.UtcNow.AddHours(-1) : null,
            ReadyByName = reviewed ? "مدير المخزن" : null,
        };
        db.Devices.Add(device);

        if (withReport)
        {
            db.Reports.Add(new Report
            {
                TenantId = tenantId,
                DeviceId = device.Id,
                DeviceCode = code,
                StartedAtUtc = DateTime.UtcNow.AddDays(-1),
                TechnicianName = "محمود",
                TechnicianCode = "T001",
                FailCount = failCount,
                ErrorCount = errorCount,
            });
        }

        return device;
    }

    private static Technician NewTechnician(AppDbContext db, Guid tenantId)
    {
        var tech = new Technician
        {
            TenantId = tenantId,
            DisplayName = "محمود",
            Code = "T" + Guid.NewGuid().ToString("N")[..4],
            Username = Guid.NewGuid().ToString("N")[..8],
            NormalizedUsername = Guid.NewGuid().ToString("N")[..8],
            IsActive = true,
            CanRepair = true,
        };
        db.Technicians.Add(tech);
        return tech;
    }

    private static HandoverCandidateFilter Any => new() { Review = ReviewFilter.Any };

    // =================================================================
    //  الشرط
    // =================================================================

    [Fact]
    public async Task A_clean_reviewed_laptop_is_eligible()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "DV-OK");

        await db.SaveChangesAsync();

        var repo = new HandoverRepository(db);

        var (rows, total) = await repo.CandidatesAsync(tenant, Any);

        Assert.Equal(1, total);
        Assert.Equal("DV-OK", Assert.Single(rows).PublicCode);
        Assert.Equal([device.Id], await repo.EligibleIdsAsync(tenant, [device.Id]));
    }

    /// <summary>
    /// 🔴 <b>«سليم» = صفر فشل <u>وصفر</u> خطأ قراءة.</b>
    ///
    /// <para>فيه تعريف تاني في التقارير بيتجاهل أخطاء القراءة؛
    /// اتاخد الأشد هنا عن قصد — التسليم مالوش رجعة، والقطعة اللي
    /// مااتقرتش مش «سليمة».</para>
    /// </summary>
    [Theory]
    [InlineData(1, 0)]
    [InlineData(0, 1)]
    [InlineData(2, 3)]
    public async Task A_laptop_whose_last_test_had_any_problem_is_not_eligible(
        int failCount, int errorCount)
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "DV-BAD", failCount, errorCount);

        await db.SaveChangesAsync();

        var repo = new HandoverRepository(db);

        Assert.Empty((await repo.CandidatesAsync(tenant, Any)).Rows);
        Assert.Empty(await repo.EligibleIdsAsync(tenant, [device.Id]));
    }

    /// <summary>⚠️ واللي عمره ما اتفحص مابيظهرش — مفيش فحص ≠ سليم.</summary>
    [Fact]
    public async Task A_laptop_that_was_never_tested_is_not_eligible()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "DV-UNTESTED", withReport: false);

        await db.SaveChangesAsync();

        Assert.Empty(await new HandoverRepository(db).EligibleIdsAsync(tenant, [device.Id]));
    }

    /// <summary>
    /// 🔴 <b>أمر صيانة مفتوح = اللاب مشغول</b>، مهما كان آخر فحص
    /// بيقول إيه. ودي الحالة اللي كانت بتخلّي لاب تحت التصليح يروح
    /// المخزن.
    /// </summary>
    [Theory]
    [InlineData(RepairStatus.New)]
    [InlineData(RepairStatus.WaitingForRepair)]
    [InlineData(RepairStatus.InProgress)]
    public async Task An_open_repair_order_blocks_handover(RepairStatus status)
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "DV-REPAIR");

        db.RepairWorkItems.Add(new RepairWorkItem
        {
            TenantId = tenant,
            DeviceId = device.Id,
            PublicCode = "RP-X",
            Status = status,
            OpenedByActorType = "User",
            OpenedByName = "كريم",
        });

        await db.SaveChangesAsync();

        Assert.Empty(await new HandoverRepository(db).EligibleIdsAsync(tenant, [device.Id]));
    }

    /// <summary>⚠️ والأمر المقفول مابيمنعش — الشغل خلص.</summary>
    [Theory]
    [InlineData(RepairStatus.Completed)]
    [InlineData(RepairStatus.UnableToRepair)]
    [InlineData(RepairStatus.Cancelled)]
    public async Task A_closed_repair_order_does_not_block(RepairStatus status)
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "DV-DONE");

        db.RepairWorkItems.Add(new RepairWorkItem
        {
            TenantId = tenant,
            DeviceId = device.Id,
            PublicCode = "RP-Y",
            Status = status,
            OpenedByActorType = "User",
            OpenedByName = "كريم",
        });

        await db.SaveChangesAsync();

        Assert.Equal(
            [device.Id],
            await new HandoverRepository(db).EligibleIdsAsync(tenant, [device.Id]));
    }

    /// <summary>
    /// 🔴 <b>في إيد فني = مش على الرف.</b> تسليمه معناه إن السجل
    /// بيقول إنه في المخزن وهو مع الفني.
    /// </summary>
    [Fact]
    public async Task A_laptop_held_by_a_technician_is_not_eligible()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var tech = NewTechnician(db, tenant);
        await db.SaveChangesAsync();

        var device = NewDevice(db, tenant, "DV-HELD", holder: tech.Id);
        await db.SaveChangesAsync();

        Assert.Empty(await new HandoverRepository(db).EligibleIdsAsync(tenant, [device.Id]));
    }

    [Theory]
    [InlineData(DeviceOperationalStage.NeedsRepair)]
    [InlineData(DeviceOperationalStage.UnderRepair)]
    public async Task A_repair_stage_blocks_handover(DeviceOperationalStage stage)
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "DV-STAGE", stage: stage);

        await db.SaveChangesAsync();

        Assert.Empty(await new HandoverRepository(db).EligibleIdsAsync(tenant, [device.Id]));
    }

    [Fact]
    public async Task A_retired_laptop_is_not_eligible()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var device = NewDevice(db, tenant, "DV-MERGED",
            status: DeviceLifecycleStatus.Merged);

        await db.SaveChangesAsync();

        Assert.Empty(await new HandoverRepository(db).EligibleIdsAsync(tenant, [device.Id]));
    }

    [Fact]
    public async Task Another_tenant_laptop_is_never_eligible()
    {
        using var db = fixture.Create();
        var mine = NewTenant(db);
        var theirs = NewTenant(db);

        var theirDevice = NewDevice(db, theirs, "DV-THEIRS");

        await db.SaveChangesAsync();

        var repo = new HandoverRepository(db);

        Assert.Empty(await repo.EligibleIdsAsync(mine, [theirDevice.Id]));
        Assert.Empty((await repo.CandidatesAsync(mine, Any)).Rows);
        Assert.Equal(0, await repo.CountExistingAsync(mine, [theirDevice.Id]));
    }

    // =================================================================
    //  فلتر المراجعة
    // =================================================================

    /// <summary>
    /// 🔴 <b>تلات شاشات مختلفة — والافتراضي «اتراجع».</b>
    ///
    /// <para>لو الافتراضي كان «الكل»، صفحة التسليم كانت هترجع
    /// لسلوكها القديم بالظبط والمراجعة تبقى شاشة مالهاش أثر.</para>
    /// </summary>
    [Fact]
    public async Task The_review_filter_splits_three_screens()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        NewDevice(db, tenant, "DV-REVIEWED", reviewed: true);
        NewDevice(db, tenant, "DV-PENDING", reviewed: false);

        await db.SaveChangesAsync();

        var repo = new HandoverRepository(db);

        var reviewed = await repo.CandidatesAsync(
            tenant, new HandoverCandidateFilter { Review = ReviewFilter.Reviewed });

        var pending = await repo.CandidatesAsync(
            tenant, new HandoverCandidateFilter { Review = ReviewFilter.NotReviewed });

        var all = await repo.CandidatesAsync(tenant, Any);

        Assert.Equal(["DV-REVIEWED"], reviewed.Rows.Select(r => r.PublicCode));
        Assert.Equal(["DV-PENDING"], pending.Rows.Select(r => r.PublicCode));
        Assert.Equal(2, all.TotalItems);

        // ⚠️ والافتراضي من النص الفاضي أو المش مفهوم هو «اتراجع».
        Assert.Equal(ReviewFilter.Reviewed, HandoverPolicy.Review(null));
        Assert.Equal(ReviewFilter.Reviewed, HandoverPolicy.Review("كلام"));
        Assert.Equal(ReviewFilter.Reviewed, HandoverPolicy.Review("ready"));
        Assert.Equal(ReviewFilter.NotReviewed, HandoverPolicy.Review("PENDING"));
        Assert.Equal(ReviewFilter.Any, HandoverPolicy.Review(" All "));
    }

    /// <summary>
    /// 🔴 <b>«اختر كل اللي طلع» لازم يختار بالظبط اللي الشاشة
    /// عارضاها.</b>
    ///
    /// <para>لو الفلترين اختلفوا، المدير بيسلّم حاجة مش اللي شافها
    /// — وده أسوأ من إن الزرار مايشتغلش.</para>
    /// </summary>
    [Fact]
    public async Task The_select_all_ids_match_the_rows_the_screen_shows()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        for (int i = 0; i < 7; i++) NewDevice(db, tenant, $"DV-{i:D2}");

        NewDevice(db, tenant, "DV-PENDING", reviewed: false);
        NewDevice(db, tenant, "DV-BROKEN", failCount: 1);

        await db.SaveChangesAsync();

        var repo = new HandoverRepository(db);
        var filter = new HandoverCandidateFilter { PageSize = 3 };

        var (rows, total) = await repo.CandidatesAsync(tenant, filter);
        var (ids, idTotal) = await repo.CandidateIdsAsync(tenant, filter, 500);

        // الصفحة ٣ صفوف، والمعرّفات السبعة كلهم، والعدد واحد.
        Assert.Equal(3, rows.Count);
        Assert.Equal(7, total);
        Assert.Equal(7, idTotal);
        Assert.Equal(7, ids.Count);

        // وأول ٣ معرّفات هما بالظبط صفوف الصفحة الأولى.
        Assert.Equal(rows.Select(r => r.Id), ids.Take(3));
    }

    /// <summary>
    /// 🔴 <b>العدّ قبل القص، مش بعده.</b>
    ///
    /// <para>لو اتحسب بعد القص هيساوي عدد اللي رجع دايماً و«اتقص»
    /// هيبقى <c>false</c> على طول — فمدير عنده ٦٠٠ مرشّح ياخد ٥٠٠
    /// والشاشة تقوله «٥٠٠ من ٥٠٠»، فيسلّمهم وهو فاكر إنه خلّص
    /// الستمية.</para>
    ///
    /// <para>⚠️ وعشان كده <c>take</c> موجود: من غيره، السلوك ده
    /// مايتجرّبش غير بزرع ٥٠١ جهاز في قاعدة الفحوص.</para>
    /// </summary>
    [Fact]
    public async Task Truncation_is_reported_not_hidden()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        for (int i = 0; i < 5; i++) NewDevice(db, tenant, $"DV-T{i}");

        await db.SaveChangesAsync();

        var repo = new HandoverRepository(db);

        var (ids, total) = await repo.CandidateIdsAsync(
            tenant, new HandoverCandidateFilter(), cap: 2);

        Assert.Equal(2, ids.Count);
        Assert.Equal(5, total);
        Assert.True(total > ids.Count);
    }

    [Fact]
    public async Task The_cap_can_never_exceed_the_batch_limit()
    {
        Assert.Equal(HandoverPolicy.BatchLimit, HandoverPolicy.Cap(null));
        Assert.Equal(HandoverPolicy.BatchLimit, HandoverPolicy.Cap(10_000));
        Assert.Equal(1, HandoverPolicy.Cap(0));
        Assert.Equal(1, HandoverPolicy.Cap(-5));
        Assert.Equal(7, HandoverPolicy.Cap(7));
    }

    // =================================================================
    //  البحث والترتيب
    // =================================================================

    [Fact]
    public async Task Search_matches_the_exact_code_and_the_normalized_text()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var dell = NewDevice(db, tenant, "DV-DELL");
        dell.LastKnownManufacturer = "Dell";
        dell.SearchText = ArabicText.Combine("DV-DELL", "Dell", "لابتوب ديل");

        NewDevice(db, tenant, "DV-HP01");

        await db.SaveChangesAsync();

        var repo = new HandoverRepository(db);

        var byCode = await repo.CandidatesAsync(tenant, new HandoverCandidateFilter
        {
            ExactCode = "DV-HP01",
            SearchPattern = SearchPattern.Contains(ArabicText.Normalize("DV-HP01")),
        });

        // ⚠️ «ديل» بالعربي من نص البحث.
        var byText = await repo.CandidatesAsync(tenant, new HandoverCandidateFilter
        {
            ExactCode = "ديل",
            SearchPattern = SearchPattern.Contains(ArabicText.Normalize("ديل")),
        });

        Assert.Equal(["DV-HP01"], byCode.Rows.Select(r => r.PublicCode));
        Assert.Equal(["DV-DELL"], byText.Rows.Select(r => r.PublicCode));
    }

    /// <summary>⚠️ بالكود — ده الترتيب اللي المدير بيقرا بيه الرف.</summary>
    [Fact]
    public async Task Candidates_are_ordered_by_code()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        foreach (string code in new[] { "DV-30", "DV-10", "DV-20" })
            NewDevice(db, tenant, code);

        await db.SaveChangesAsync();

        var (rows, _) = await new HandoverRepository(db)
            .CandidatesAsync(tenant, new HandoverCandidateFilter());

        Assert.Equal(["DV-10", "DV-20", "DV-30"], rows.Select(r => r.PublicCode));
    }

    [Fact]
    public async Task The_candidate_row_carries_the_review_stamp_and_the_last_test()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var container = new ImportContainer
        {
            TenantId = tenant, Code = "C-1", NormalizedCode = "C1",
            Name = "شحنة", CreatedByName = "كريم",
        };
        db.Containers.Add(container);

        var location = new Location { TenantId = tenant, Name = "المخزن", Code = "W1" };
        db.Locations.Add(location);

        var device = NewDevice(db, tenant, "DV-FULL");
        device.ContainerId = container.Id;
        device.CurrentLocationId = location.Id;
        device.CommercialModelName = "ProBook 6470b";

        await db.SaveChangesAsync();

        var (rows, _) = await new HandoverRepository(db).CandidatesAsync(tenant, Any);

        var row = Assert.Single(rows);

        Assert.Equal("C-1", row.ContainerCode);
        Assert.Equal("المخزن", row.LocationName);
        Assert.Equal("ProBook 6470b", row.Model);
        Assert.NotNull(row.LastTestAtUtc);
        Assert.NotNull(row.ReadyAtUtc);
        Assert.Equal("مدير المخزن", row.ReadyByName);
        Assert.Equal(DeviceOperationalStage.Ready, row.Stage);
    }

    /// <summary>
    /// ⚠️ لاب من غير حاوية ولا مكان بيرجّع نصوص فاضية — مش
    /// <c>null</c> ومش استثناء.
    /// </summary>
    [Fact]
    public async Task Missing_container_and_location_come_back_as_empty_strings()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        NewDevice(db, tenant, "DV-BARE");

        await db.SaveChangesAsync();

        var (rows, _) = await new HandoverRepository(db).CandidatesAsync(tenant, Any);

        var row = Assert.Single(rows);

        Assert.Equal("", row.ContainerCode);
        Assert.Equal("", row.LocationName);
    }

    // =================================================================
    //  المراجعة المتتبّعة
    // =================================================================

    /// <summary>
    /// 🔴 <b>الصفوف لازم تكون متتبّعة.</b>
    ///
    /// <para>القراية بـ<c>AsNoTracking</c> بترجّع صفوف بتتعدّل في
    /// الذاكرة و<c>SaveChanges</c> مابتكتبش حاجة — والنقطة كانت
    /// بترجّع «اتغيّر ٣» والقاعدة زي ما هي. نجاح كداب.</para>
    /// </summary>
    [Fact]
    public async Task The_review_rows_are_tracked_so_the_write_actually_lands()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var device = NewDevice(db, tenant, "DV-TRACK", reviewed: false);

        await db.SaveChangesAsync();

        var repo = new HandoverRepository(db);

        var rows = await repo.TrackedForReviewAsync(tenant, [device.Id], markedOnly: false);

        var stamp = new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc);

        foreach (var row in rows)
        {
            row.ReadyForHandoverAtUtc = stamp;
            row.ReadyByName = "المالك";
        }

        await db.SaveChangesAsync();

        using var fresh = fixture.Create();

        var saved = await fresh.Devices.FindAsync(device.Id);

        Assert.NotNull(saved);
        Assert.Equal(stamp, saved.ReadyForHandoverAtUtc);
        Assert.Equal("المالك", saved.ReadyByName);
    }

    /// <summary>
    /// ⚠️ المعلّم خلاص مابيتلمسش، واللي مش معلّم مابيتشالش —
    /// فالعدد بيوصف الكتابة مش الطلب.
    /// </summary>
    [Fact]
    public async Task The_review_read_skips_rows_that_are_already_in_the_wanted_state()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var marked = NewDevice(db, tenant, "DV-MARKED", reviewed: true);
        var plain = NewDevice(db, tenant, "DV-PLAIN", reviewed: false);

        await db.SaveChangesAsync();

        var repo = new HandoverRepository(db);
        Guid[] both = [marked.Id, plain.Id];

        var toMark = await repo.TrackedForReviewAsync(tenant, both, markedOnly: false);
        var toClear = await repo.TrackedForReviewAsync(tenant, both, markedOnly: true);

        Assert.Equal([plain.Id], toMark.Select(d => d.Id));
        Assert.Equal([marked.Id], toClear.Select(d => d.Id));
    }

    [Fact]
    public async Task Unreviewed_codes_name_the_laptops_that_still_need_a_human()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var a = NewDevice(db, tenant, "DV-A", reviewed: false);
        var b = NewDevice(db, tenant, "DV-B", reviewed: true);
        var c = NewDevice(db, tenant, "DV-C", reviewed: false);

        await db.SaveChangesAsync();

        var codes = await new HandoverRepository(db)
            .UnreviewedCodesAsync(tenant, [a.Id, b.Id, c.Id], take: 5);

        Assert.Equal(["DV-A", "DV-C"], codes);
    }

    /// <summary>
    /// ⚠️ ومفيش ٥٠٠ كود في رسالة واحدة — الرسالة بتقول أول
    /// خمسة.
    /// </summary>
    [Fact]
    public async Task The_refusal_message_shows_only_a_handful_of_codes()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var ids = new List<Guid>();

        for (int i = 0; i < 9; i++)
            ids.Add(NewDevice(db, tenant, $"DV-M{i}", reviewed: false).Id);

        await db.SaveChangesAsync();

        var codes = await new HandoverRepository(db)
            .UnreviewedCodesAsync(tenant, ids, HandoverPolicy.BlockedCodesShown);

        Assert.Equal(HandoverPolicy.BlockedCodesShown, codes.Count);
    }
}
