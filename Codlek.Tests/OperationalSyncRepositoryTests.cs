using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Codlek.Infrastructure.Data;
using Codlek.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Codlek.Tests;

/// <summary>قاعدة فحوص الشغل التشغيلي.</summary>
public sealed class OperationalSyncDbFixture : SqlServerDbFixture
{
    protected override string DatabaseName => "codlek_operational_sync_test";
}

/// <summary>
/// استعلامات الشغل التشغيلي — <b>على قاعدة حقيقية</b>.
///
/// <para>🔴 <b>وفيه هنا فحص بيثبت فخ في EF نفسه</b> — مش في الكود. صف
/// بمفتاح جاهز من الراكة بيتحط في مجموعة أب <b>موجود</b> بيتعلّم
/// «متعدّل» مش «جديد»، وبيطلع UPDATE على صف مالوش وجود. الفخ ده
/// مابيبانش في أول رفع (الأب نفسه جديد) — بيبان في الإعادة بس، يعني
/// بالظبط في الحالة اللي المزامنة بتعتمد عليها.</para>
/// </summary>
public class OperationalSyncRepositoryTests(OperationalSyncDbFixture fixture)
    : IClassFixture<OperationalSyncDbFixture>
{
    private static Guid NewTenant(AppDbContext db)
    {
        var tenant = new Tenant { Name = "ورشة " + Guid.NewGuid().ToString("N")[..6] };
        db.Tenants.Add(tenant);
        return tenant.Id;
    }

    private static Rack NewRack(AppDbContext db, Guid tenantId)
    {
        var rack = new Rack
        {
            TenantId = tenantId,
            RackCode = "R-" + Guid.NewGuid().ToString("N")[..8],
            Name = "محطة",
        };

        db.Racks.Add(rack);
        return rack;
    }

    private static Technician NewTech(AppDbContext db, Guid tenantId)
    {
        string key = "t" + Guid.NewGuid().ToString("N")[..8];

        var tech = new Technician
        {
            TenantId = tenantId,
            Code = Random.Shared.Next(100000, 999999).ToString(),
            DisplayName = "فني",
            Username = key,
            NormalizedUsername = key,
            PasswordHash = "hash",
            Salt = "salt",
        };

        db.Technicians.Add(tech);
        return tech;
    }

    private static void Login(
        AppDbContext db, Guid tenantId, Guid rackId, Guid techId, DateTime at,
        bool success = true)
    {
        db.TechnicianLoginAttempts.Add(new TechnicianLoginAttempt
        {
            TenantId = tenantId,
            RackId = rackId,
            TechnicianId = techId,
            AttemptedUsername = "x",
            Success = success,
            AtUtc = at,
        });
    }

    // =================================================================
    //  دليل التصريح
    // =================================================================

    /// <summary>
    /// 🔴 <b>آخر دخول ناجح، على المحطة دي، في وقت الحركة أو قبله — وبس.</b>
    ///
    /// <para>كل شرط من الخمسة بيقفل باب: الشركة والمحطة والفني بيقفلوا
    /// «دخول في حتة تانية بيصرّح هنا»، و«الناجح» بيقفل «محاولة فاشلة
    /// بتتحسب تصريح»، و«قبل الحركة» بيقفل «دخول النهارده بيصرّح على شغل
    /// إمبارح».</para>
    /// </summary>
    [Fact]
    public async Task Only_a_successful_earlier_login_on_this_rack_counts()
    {
        using var db = fixture.Create();

        var tenant = NewTenant(db);
        var rack = NewRack(db, tenant);
        var otherRack = NewRack(db, tenant);
        var tech = NewTech(db, tenant);
        var otherTech = NewTech(db, tenant);

        await db.SaveChangesAsync();

        var occurred = new DateTime(2026, 10, 5, 10, 0, 0, DateTimeKind.Utc);
        var good = occurred.AddDays(-2);

        // ✅ الوحيد اللي المفروض يرجع.
        Login(db, tenant, rack.Id, tech.Id, good);

        // ✅ أقدم منه — مش آخر واحد.
        Login(db, tenant, rack.Id, tech.Id, occurred.AddDays(-5));

        // ❌ فاشل وأحدث.
        Login(db, tenant, rack.Id, tech.Id, occurred.AddHours(-1), success: false);

        // ❌ محطة تانية وأحدث.
        Login(db, tenant, otherRack.Id, tech.Id, occurred.AddHours(-1));

        // ❌ فني تاني وأحدث.
        Login(db, tenant, rack.Id, otherTech.Id, occurred.AddHours(-1));

        // ❌ بعد الحركة.
        Login(db, tenant, rack.Id, tech.Id, occurred.AddHours(1));

        await db.SaveChangesAsync();

        var at = await new OperationalSyncRepository(db)
            .LastLoginAtAsync(tenant, rack.Id, tech.Id, occurred);

        Assert.Equal(good, at);
    }

    [Fact]
    public async Task A_login_exactly_at_the_event_counts()
    {
        using var db = fixture.Create();

        var tenant = NewTenant(db);
        var rack = NewRack(db, tenant);
        var tech = NewTech(db, tenant);

        await db.SaveChangesAsync();

        var at = new DateTime(2026, 10, 5, 10, 0, 0, DateTimeKind.Utc);

        Login(db, tenant, rack.Id, tech.Id, at);

        await db.SaveChangesAsync();

        Assert.Equal(at, await new OperationalSyncRepository(db)
            .LastLoginAtAsync(tenant, rack.Id, tech.Id, at));
    }

    [Fact]
    public async Task No_qualifying_login_is_null()
    {
        using var db = fixture.Create();

        var tenant = NewTenant(db);
        var rack = NewRack(db, tenant);
        var tech = NewTech(db, tenant);

        await db.SaveChangesAsync();

        Assert.Null(await new OperationalSyncRepository(db)
            .LastLoginAtAsync(tenant, rack.Id, tech.Id, DateTime.UtcNow));
    }

    // =================================================================
    //  العزل
    // =================================================================

    [Fact]
    public async Task A_technician_in_another_workshop_is_not_found()
    {
        using var db = fixture.Create();

        var mine = NewTenant(db);
        var theirs = NewTenant(db);
        var hers = NewTech(db, theirs);

        await db.SaveChangesAsync();

        var repo = new OperationalSyncRepository(db);

        Assert.Null(await repo.FindTechnicianAsync(mine, hers.Id));
        Assert.NotNull(await repo.FindTechnicianAsync(theirs, hers.Id));
    }

    [Fact]
    public async Task A_report_in_another_workshop_does_not_exist_here()
    {
        using var db = fixture.Create();

        var mine = NewTenant(db);
        var theirs = NewTenant(db);

        var report = new Report
        {
            Id = Guid.NewGuid(),
            TenantId = theirs,
            StartedAtUtc = DateTime.UtcNow,
        };

        db.Reports.Add(report);

        await db.SaveChangesAsync();

        var repo = new OperationalSyncRepository(db);

        Assert.False(await repo.ReportExistsAsync(mine, report.Id));
        Assert.True(await repo.ReportExistsAsync(theirs, report.Id));
    }

    // =================================================================
    //  فخ EF — الإضافة الصريحة
    // =================================================================

    private async Task<(Guid Tenant, Guid ItemId)> SeedWorkItemAsync()
    {
        using var db = fixture.Create();

        var tenant = NewTenant(db);

        var device = new Device { TenantId = tenant, PublicCode = "LP-" + Random.Shared.Next(10_000_000, 99_999_999) };
        db.Devices.Add(device);

        await db.SaveChangesAsync();

        var item = new RepairWorkItem
        {
            TenantId = tenant,
            DeviceId = device.Id,
            PublicCode = "RP-" + Random.Shared.Next(10_000_000, 99_999_999),
            Status = RepairStatus.WaitingForRepair,
        };

        db.RepairWorkItems.Add(item);

        await db.SaveChangesAsync();

        return (tenant, item.Id);
    }

    /// <summary>
    /// 🔴 <b>إعادة الرفع بعطل جديد على أمر موجود — بالإضافة الصريحة
    /// بيتحفظ.</b>
    /// </summary>
    [Fact]
    public async Task A_new_issue_on_an_existing_order_saves_when_added_explicitly()
    {
        var (tenant, itemId) = await SeedWorkItemAsync();

        using (var db = fixture.Create())
        {
            var repo = new OperationalSyncRepository(db);
            var row = await repo.FindWorkItemAsync(tenant, itemId);

            var issue = new RepairWorkItemIssue
            {
                Id = Guid.NewGuid(),
                TenantId = tenant,
                WorkItemId = itemId,
                IssueCode = "SCR-01",
            };

            repo.AddIssue(issue);
            row!.Issues.Add(issue);

            var part = new RepairPart
            {
                Id = Guid.NewGuid(),
                TenantId = tenant,
                WorkItemId = itemId,
                Name = "شاشة",
            };

            repo.AddPart(part);
            row.Parts.Add(part);

            await db.SaveChangesAsync();
        }

        using var fresh = fixture.Create();

        var stored = await fresh.RepairWorkItems
            .Include(w => w.Issues)
            .Include(w => w.Parts)
            .SingleAsync(w => w.Id == itemId);

        Assert.Single(stored.Issues);
        Assert.Single(stored.Parts);
    }

    /// <summary>
    /// 🔴 <b>والضابط السلبي: من غير الإضافة الصريحة، الحفظ بيرمي.</b>
    ///
    /// <para>الفحص ده بيثبت إن الفخ <b>حقيقي</b> — مش احتياط زيادة. لو
    /// EF غيّر سلوكه يوم ما والفحص ده وقع، يبقى الإضافة الصريحة بقت
    /// زيادة ونقدر نشيلها.</para>
    /// </summary>
    [Fact]
    public async Task Without_the_explicit_add_the_save_throws()
    {
        var (tenant, itemId) = await SeedWorkItemAsync();

        using var db = fixture.Create();

        var row = await new OperationalSyncRepository(db).FindWorkItemAsync(tenant, itemId);

        // ⚠️ إضافة للمجموعة **بس** — بمفتاح جاهز زي اللي جايّ من الراكة.
        row!.Issues.Add(new RepairWorkItemIssue
        {
            Id = Guid.NewGuid(),
            TenantId = tenant,
            WorkItemId = itemId,
            IssueCode = "SCR-02",
        });

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task A_work_item_in_another_workshop_is_not_found()
    {
        var (tenant, itemId) = await SeedWorkItemAsync();

        using var db = fixture.Create();

        var repo = new OperationalSyncRepository(db);

        Assert.NotNull(await repo.FindWorkItemAsync(tenant, itemId));
        Assert.Null(await repo.FindWorkItemAsync(Guid.NewGuid(), itemId));
    }
}
