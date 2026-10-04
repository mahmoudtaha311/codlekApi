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
        RackStatus status = RackStatus.Active)
    {
        var rack = new Rack
        {
            TenantId = tenantId,
            RackCode = code,
            Name = "محطة " + code,
            Status = status,
        };

        db.Racks.Add(rack);
        return rack;
    }

    private static RackPairingCode NewCode(
        AppDbContext db, Guid tenantId, string prefix,
        DateTime? createdAtUtc = null, DateTime? consumedAtUtc = null)
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
}
