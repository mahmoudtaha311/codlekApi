using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Codlek.Infrastructure.Data;
using Codlek.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Codlek.Tests;

/// <summary>
/// مستودع محطات الفحص — <b>على قاعدة حقيقية</b>.
///
/// <para>🔴 <b>والملف ده موجود لسبب مقاس.</b> فحوص القطاع بتستعمل
/// مستودع مزيّف، والمزيّف بينفّذ الفلاتر بنفسه — يعني شيل
/// <c>ConsumedAtUtc == null</c> من الاستعلام الحقيقي كان بيعدّي من
/// تحتها كلها. (جرّبناها: التحوير ده <b>نجا</b> من ٥٠ فحص وحدة.)
/// ودي نفس العائلة اللي خلّت <c>/api/v1/technicians</c> ترجّع
/// <c>٥٠٠</c> على الحي والفحوص كلها خضرا.</para>
/// </summary>
public class RackRepositoryTests(RackDbFixture fixture) : IClassFixture<RackDbFixture>
{
    private static Guid NewTenant(AppDbContext db)
    {
        var tenant = new Tenant { Name = "ورشة " + Guid.NewGuid().ToString("N")[..6] };
        db.Tenants.Add(tenant);
        return tenant.Id;
    }

    private static Rack NewRack(
        AppDbContext db, Guid tenantId, string code,
        RackStatus status = RackStatus.Active, string installationId = "")
    {
        var rack = new Rack
        {
            TenantId = tenantId,
            RackCode = code,
            Name = "محطة " + code,
            Status = status,
            InstallationId = installationId,
        };

        db.Racks.Add(rack);
        return rack;
    }

    private static RackPairingCode NewCode(
        AppDbContext db, Guid tenantId, string prefix,
        DateTime? createdAtUtc = null, DateTime? consumedAtUtc = null,
        int failedAttempts = 0)
    {
        var row = new RackPairingCode
        {
            TenantId = tenantId,
            CodePrefix = prefix,
            CodeHash = "hash",
            Salt = "salt",
            IntendedName = "محطة " + prefix,
            CreatedAtUtc = createdAtUtc ?? DateTime.UtcNow,
            ExpiresAtUtc = (createdAtUtc ?? DateTime.UtcNow).AddMinutes(15),
            ConsumedAtUtc = consumedAtUtc,
            FailedAttempts = failedAttempts,
        };

        db.RackPairingCodes.Add(row);
        return row;
    }

    // =================================================================
    //  الفلاتر
    // =================================================================

    /// <summary>
    /// 🔴 <b>التحوير اللي نجا من فحوص الوحدة.</b> شيل
    /// <c>ConsumedAtUtc == null</c> وقايمة «أكواد مستنية» بتبقى «كل
    /// كود اتعمل في الورشة من يوم ما فتحت» — والمالك بيفتكر إن عنده
    /// أكواد جاهزة أكتر من الحقيقة.
    /// </summary>
    [Fact]
    public async Task A_consumed_code_is_not_in_the_pending_list()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        NewCode(db, tenant, "OPEN");
        NewCode(db, tenant, "USED", consumedAtUtc: DateTime.UtcNow.AddMinutes(-3));

        await db.SaveChangesAsync();

        var rows = await new RackRepository(db).PendingCodesAsync(tenant);

        Assert.Equal("OPEN", Assert.Single(rows).CodePrefix);
    }

    /// <summary>
    /// ⚠️ <b>والمنتهي <u>موجود</u> في القايمة.</b> لازم يفضل باين
    /// عشان المالك يمسحه — فلترته هنا كانت هتخفي الصف الوحيد اللي
    /// زرار المسح بيبان عليه.
    /// </summary>
    [Fact]
    public async Task An_expired_code_is_still_in_the_pending_list()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var dead = NewCode(db, tenant, "DEAD");
        dead.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-30);

        await db.SaveChangesAsync();

        var rows = await new RackRepository(db).PendingCodesAsync(tenant);

        Assert.Equal("DEAD", Assert.Single(rows).CodePrefix);
    }

    [Fact]
    public async Task Codes_and_stations_are_scoped_to_the_workshop()
    {
        using var db = fixture.Create();
        var mine = NewTenant(db);
        var theirs = NewTenant(db);

        NewCode(db, mine, "MINE");
        NewCode(db, theirs, "THRS");
        NewRack(db, mine, "RACK-100");
        NewRack(db, theirs, "RACK-200");

        await db.SaveChangesAsync();

        var repo = new RackRepository(db);

        Assert.Equal("MINE", Assert.Single(await repo.PendingCodesAsync(mine)).CodePrefix);
        Assert.Equal("RACK-100", Assert.Single(await repo.ListAsync(mine)).RackCode);
    }

    /// <summary>
    /// 🔴 <b>والبحث بمعرّف مربوط بالشركة كمان.</b> من غيره، مالك
    /// ورشة كان يقدر يلغي محطة ورشة تانية بإنه يبعت معرّفها.
    /// </summary>
    [Fact]
    public async Task A_station_in_another_workshop_is_not_found_by_id()
    {
        using var db = fixture.Create();
        var mine = NewTenant(db);
        var theirs = NewTenant(db);

        var rack = NewRack(db, theirs, "RACK-300");
        var code = NewCode(db, theirs, "THRS");

        await db.SaveChangesAsync();

        var repo = new RackRepository(db);

        Assert.Null(await repo.FindAsync(mine, rack.Id));
        Assert.Null(await repo.FindCodeAsync(mine, code.Id));
        Assert.NotNull(await repo.FindAsync(theirs, rack.Id));
    }

    /// <summary>
    /// 🔴 <b>والمستهلك بيترجّع من البحث بمعرّف.</b> المسح محتاج
    /// يشوفه عشان يرفضه برسالة بتشرح السبب — فلترته هنا كانت بترجّع
    /// «مش موجود»، والمالك مكانش هيفهم ليه الكود اختفى.
    /// </summary>
    [Fact]
    public async Task A_consumed_code_is_still_found_by_id()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var code = NewCode(db, tenant, "USED", consumedAtUtc: DateTime.UtcNow.AddMinutes(-1));

        await db.SaveChangesAsync();

        var found = await new RackRepository(db).FindCodeAsync(tenant, code.Id);

        Assert.NotNull(found);
        Assert.NotNull(found.ConsumedAtUtc);
    }

    // =================================================================
    //  التتبّع
    // =================================================================

    /// <summary>
    /// 🔴 <b>الصف متتبّع — والمعالج بيعدّله في مكانه.</b> لو رجع
    /// <c>AsNoTracking</c>، الإيقاف والإلغاء كانوا هيردّوا
    /// <c>200</c> ومحصلش حاجة في القاعدة.
    /// </summary>
    [Fact]
    public async Task A_station_found_by_id_is_tracked()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var rack = NewRack(db, tenant, "RACK-400");

        await db.SaveChangesAsync();

        var found = await new RackRepository(db).FindAsync(tenant, rack.Id);

        found!.Status = RackStatus.Suspended;
        await db.SaveChangesAsync();

        using var fresh = fixture.Create();

        Assert.Equal(
            RackStatus.Suspended,
            (await fresh.Racks.SingleAsync(r => r.Id == rack.Id)).Status);
    }

    [Fact]
    public async Task Removing_a_code_actually_deletes_the_row()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var code = NewCode(db, tenant, "GONE");

        await db.SaveChangesAsync();

        var repo = new RackRepository(db);
        var found = await repo.FindCodeAsync(tenant, code.Id);

        repo.RemoveCode(found!);
        await db.SaveChangesAsync();

        using var fresh = fixture.Create();

        Assert.False(await fresh.RackPairingCodes.AnyAsync(c => c.Id == code.Id));
    }

    // =================================================================
    //  الترتيب
    // =================================================================

    [Fact]
    public async Task Stations_come_back_ordered_by_code()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        NewRack(db, tenant, "RACK-003");
        NewRack(db, tenant, "RACK-001");
        NewRack(db, tenant, "RACK-002");

        await db.SaveChangesAsync();

        var rows = await new RackRepository(db).ListAsync(tenant);

        Assert.Equal(
            ["RACK-001", "RACK-002", "RACK-003"], rows.Select(r => r.RackCode));
    }

    [Fact]
    public async Task The_newest_code_comes_first()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var now = DateTime.UtcNow;

        NewCode(db, tenant, "OLD", createdAtUtc: now.AddMinutes(-10));
        NewCode(db, tenant, "NEW", createdAtUtc: now.AddMinutes(-1));
        NewCode(db, tenant, "MID", createdAtUtc: now.AddMinutes(-5));

        await db.SaveChangesAsync();

        var rows = await new RackRepository(db).PendingCodesAsync(tenant);

        Assert.Equal(["NEW", "MID", "OLD"], rows.Select(c => c.CodePrefix));
    }

    /// <summary>
    /// 🔴 <b>وده الفحص اللي بيقفل باب الفاصل فعلاً.</b>
    ///
    /// <para>فحص الترتيب اللي فوق <b>مش</b> بيلقط شيل
    /// <c>ThenBy(Id)</c> — صفوف قليلة بتطلع بنفس الخطة فالترتيب
    /// بيبان ثابت <b>بالعرض</b> مش بالعقد. (ونفس الحكاية حصلت في
    /// قايمة الصيانة وقايمة الأجهزة.)</para>
    ///
    /// <para>⚠️ فالفحص بيقرا جملة <c>ORDER BY</c> المولّدة ويتأكد إن
    /// فيها عمود <b>فريد</b> — ومن <b>آخر</b> <c>ORDER BY</c> في
    /// النص مش أولها.</para>
    /// </summary>
    [Fact]
    public async Task Both_listings_end_with_a_unique_column()
    {
        var sql = new List<string>();

        using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(fixture.ConnectionString)
                .LogTo(sql.Add, [DbLoggerCategory.Database.Command.Name], LogLevel.Information)
                .Options);

        var tenant = NewTenant(db);
        NewRack(db, tenant, "RACK-SQL");
        NewCode(db, tenant, "SQL1");

        await db.SaveChangesAsync();

        var repo = new RackRepository(db);

        sql.Clear();

        await repo.ListAsync(tenant);
        await repo.PendingCodesAsync(tenant);

        var ordered = sql
            .Where(line => line.Contains("ORDER BY", StringComparison.Ordinal))
            .Select(line => line[line.LastIndexOf("ORDER BY", StringComparison.Ordinal)..])
            .ToList();

        Assert.Equal(2, ordered.Count);

        Assert.All(ordered, clause =>
            Assert.Contains("[Id]", clause, StringComparison.Ordinal));
    }

    // =================================================================
    //  التسجيل
    // =================================================================
    //
    // 🔴 كل فحص تحت ليه **بادئة كود لوحده** (PRF1، PRF2، …).
    //
    //    الفحوص دي كلها بتشارك قاعدة واحدة، و`CodesByPrefixAsync`
    //    **مش مربوطة بشركة** — فالعزل بالشركة اللي بيحمي باقي
    //    الملف مابيحميهاش. بادئة مشتركة بين فحصين معناها إن
    //    الصفوف بتعدّي من واحد للتاني، والفشل بيظهر **بس** لما
    //    المجموعة كلها تتشغّل. (حصل فعلاً: الفحصين الأولانيين
    //    وقعوا وهما صح.)

    /// <summary>
    /// 🔴 <b>البحث بالبادئة <u>مش</u> مربوط بشركة — وده مقصود.</b>
    ///
    /// <para>المحطة الجديدة مالهاش مفتاح ومالهاش شركة؛ <b>الكود</b>
    /// هو اللي بيحدّد الشركة. فترشيح بالشركة قبل التحقق دور.</para>
    /// </summary>
    [Fact]
    public async Task Codes_are_searched_by_prefix_across_all_workshops()
    {
        using var db = fixture.Create();
        var mine = NewTenant(db);
        var theirs = NewTenant(db);

        NewCode(db, mine, "PRF1");
        NewCode(db, theirs, "PRF1");
        NewCode(db, mine, "ZZZZ");

        await db.SaveChangesAsync();

        var rows = await new RackRepository(db).CodesByPrefixAsync("PRF1");

        Assert.Equal(2, rows.Count);
        Assert.Equal(2, rows.Select(r => r.TenantId).Distinct().Count());
    }

    /// <summary>
    /// ⚠️ <b>والمستهلك مستبعد من المرشّحين.</b> من غير الفلتر، كود
    /// اتستخدم مرة كان يرجع يتطابق — وراكة تانية تاخد مفتاح من كود
    /// مستهلك.
    /// </summary>
    [Fact]
    public async Task A_consumed_code_is_not_a_candidate()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        NewCode(db, tenant, "PRF2");
        NewCode(db, tenant, "PRF2", consumedAtUtc: DateTime.UtcNow.AddHours(-1));

        await db.SaveChangesAsync();

        var row = Assert.Single(await new RackRepository(db).CodesByPrefixAsync("PRF2"));

        Assert.Null(row.ConsumedAtUtc);
    }

    /// <summary>
    /// 🔴 <b>والمرشّحين بيرجعوا <u>متتبّعين</u> — والتتبّع ده
    /// حمّال.</b>
    ///
    /// <para>زيادة المحاولات في المعالج بتتعمل على الكائنات اللي
    /// رجعت من هنا وبعدها <c>SaveChanges</c>. حد يضيف
    /// <c>AsNoTracking()</c> على الاستعلام ده — الزيادة بتفضل في
    /// الذاكرة بس، والقفل بعد خمس محاولات <b>بيبقى مفيش</b>،
    /// والتخمين يبقى مفتوح للأبد. والفحوص بالمزيّف كلها بتعدّي
    /// لأن المزيّف بيشارك نفس الكائن.</para>
    /// </summary>
    [Fact]
    public async Task Incrementing_a_candidate_actually_persists()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        NewCode(db, tenant, "PRF3");

        await db.SaveChangesAsync();

        var repo = new RackRepository(db);

        foreach (var candidate in await repo.CodesByPrefixAsync("PRF3"))
            candidate.FailedAttempts++;

        await db.SaveChangesAsync();

        // ⚠️ سياق جديد — عشان القراية تيجي من القاعدة مش من التتبّع.
        using var fresh = fixture.Create();

        Assert.Equal(
            1,
            (await fresh.RackPairingCodes.SingleAsync(c => c.TenantId == tenant))
                .FailedAttempts);
    }

    /// <summary>
    /// 🔴 <b>الاستهلاك بيكسب <u>مرة واحدة</u>.</b>
    ///
    /// <para>راكتين بيسجّلوا بنفس الكود في نفس اللحظة لازم واحدة بس
    /// تكسب. والقراية-ثم-الكتابة بتخلّي الاتنين يكسبوا — يعني
    /// محطتين بمفتاحين من كود واحد.</para>
    /// </summary>
    [Fact]
    public async Task Only_the_first_consume_wins()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var code = NewCode(db, tenant, "PRF4");

        await db.SaveChangesAsync();

        var repo = new RackRepository(db);
        var at = new DateTime(2026, 10, 4, 9, 30, 0, DateTimeKind.Utc);

        Assert.True(await repo.ConsumeCodeAsync(code.Id, at));
        Assert.False(await repo.ConsumeCodeAsync(code.Id, at.AddMinutes(1)));

        using var fresh = fixture.Create();
        var stored = await fresh.RackPairingCodes.SingleAsync(c => c.Id == code.Id);

        // ⚠️ والوقت بتاع اللي كسب — مش بتاع اللي خسر.
        Assert.Equal(at, stored.ConsumedAtUtc);
    }

    /// <summary>⚠️ وكود مش موجود مابيكسبش.</summary>
    [Fact]
    public async Task Consuming_a_missing_code_claims_nothing()
    {
        using var db = fixture.Create();

        Assert.False(await new RackRepository(db).ConsumeCodeAsync(
            Guid.NewGuid(), DateTime.UtcNow));
    }

    /// <summary>
    /// ⚠️ <b>والكود المستهلك مابيتمسحش — بيتربط.</b> الصف ده هو
    /// الدليل الوحيد على إن المحطة الفلانية اتسجّلت بأنهي إذن ومن
    /// مين.
    /// </summary>
    [Fact]
    public async Task Linking_a_code_keeps_the_row_and_names_the_rack()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var code = NewCode(db, tenant, "PRF5");
        var rack = NewRack(db, tenant, "RACK-500");

        await db.SaveChangesAsync();

        var repo = new RackRepository(db);

        await repo.ConsumeCodeAsync(code.Id, DateTime.UtcNow);
        await repo.LinkCodeToRackAsync(code.Id, rack.Id);

        using var fresh = fixture.Create();
        var stored = await fresh.RackPairingCodes.SingleAsync(c => c.Id == code.Id);

        Assert.Equal(rack.Id, stored.ConsumedByRackId);
        Assert.NotNull(stored.ConsumedAtUtc);
    }

    /// <summary>
    /// 🔴 <b>التوأم: نفس هوية القرص في نفس الورشة.</b>
    ///
    /// <para>⚠️ والملغية مستبعدة — محطة اتلغت وهوية قرصها اتسجّلت
    /// تاني ده تسجيل جديد مشروع مش استنساخ. ومن غير الاستبعاد ده،
    /// كل إعادة تسجيل لراكة ملغية بتولّد تنبيه استنساخ كذّاب.</para>
    /// </summary>
    [Fact]
    public async Task Twins_exclude_the_new_rack_other_workshops_and_revoked_stations()
    {
        using var db = fixture.Create();
        var mine = NewTenant(db);
        var theirs = NewTenant(db);

        var fresh_rack = NewRack(db, mine, "RACK-001", installationId: "disk-9");
        NewRack(db, mine, "RACK-002", installationId: "disk-9");
        NewRack(db, mine, "RACK-003", installationId: "disk-9",
            status: RackStatus.Revoked);
        NewRack(db, mine, "RACK-004", installationId: "disk-OTHER");
        NewRack(db, theirs, "RACK-005", installationId: "disk-9");

        await db.SaveChangesAsync();

        var twins = await new RackRepository(db)
            .TwinsByInstallationAsync(mine, "disk-9", fresh_rack.Id);

        Assert.Equal("RACK-002", Assert.Single(twins).RackCode);
    }

    /// <summary>
    /// ⚠️ <b>وهوية قرص فاضية مابتعملش توأم مع كل محطة قديمة.</b>
    /// المحطات القديمة كلها عندها <c>""</c> في الخانة دي.
    /// </summary>
    [Fact]
    public async Task An_empty_installation_id_still_matches_only_empties()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var subject = NewRack(db, tenant, "RACK-010", installationId: "disk-1");
        NewRack(db, tenant, "RACK-011");

        await db.SaveChangesAsync();

        Assert.Empty(await new RackRepository(db)
            .TwinsByInstallationAsync(tenant, "disk-1", subject.Id));
    }

    /// <summary>
    /// ⚠️ <b>واسم الشركة بيرجع <c>null</c> لو اتمسحت.</b> ودي الحالة
    /// اللي بتخلّي الرد «مقفول» (<c>423</c>) مش «كود غلط».
    /// </summary>
    [Fact]
    public async Task A_missing_workshop_has_no_name()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        await db.SaveChangesAsync();

        var repo = new RackRepository(db);

        Assert.NotNull(await repo.TenantNameAsync(tenant));
        Assert.Null(await repo.TenantNameAsync(Guid.NewGuid()));
    }
}
