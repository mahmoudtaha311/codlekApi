using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Codlek.Infrastructure.Data;
using Codlek.Infrastructure.Repositories;

namespace Codlek.Tests;

/// <summary>
/// مستودع الحاويات — على قاعدة حقيقية.
///
/// <para>🔴 <b>الفحوص دي موجودة بسبب درس اتعلمناه:</b> فحوص القطاع
/// بتستعمل مستودع بديل، فهي بتثبت إن <b>القرار</b> صح — مش إن
/// <b>الاستعلام</b> صح. وقواعد زي «استبعاد المدموج» و«استبعاد الفحوص
/// الممسوحة» و«الترشيح بالشركة» عايشة في الاستعلام بس.</para>
/// </summary>
public class ContainerRepositoryTests(ContainerDbFixture fixture)
    : IClassFixture<ContainerDbFixture>
{
    private static Guid NewTenant(AppDbContext db)
    {
        var tenant = new Tenant { Name = "ورشة " + Guid.NewGuid().ToString("N")[..6] };
        db.Tenants.Add(tenant);
        return tenant.Id;
    }

    private static ImportContainer NewContainer(
        AppDbContext db, Guid tenantId, string code, int sortOrder = 0)
    {
        var row = new ImportContainer
        {
            TenantId = tenantId,
            Code = code,
            NormalizedCode = Core.Text.ContainerCode.Normalize(code),
            Name = "شحنة",
            SortOrder = sortOrder,
            CreatedByName = "كريم",
        };
        db.Containers.Add(row);
        return row;
    }

    private static Device NewDevice(
        AppDbContext db, Guid tenantId, Guid containerId, string code,
        DeviceLifecycleStatus status = DeviceLifecycleStatus.Active)
    {
        var row = new Device
        {
            TenantId = tenantId,
            ContainerId = containerId,
            PublicCode = code,
            LastKnownManufacturer = "Lenovo",
            LastKnownModel = "20L5",
            Status = status,
            LastSeenAtUtc = DateTime.UtcNow,
        };
        db.Devices.Add(row);
        return row;
    }

    // =================================================================
    //  الترشيح بالشركة
    // =================================================================

    [Fact]
    public async Task Containers_of_another_tenant_are_never_listed()
    {
        await using var db = fixture.Create();
        Guid mine = NewTenant(db), theirs = NewTenant(db);
        NewContainer(db, mine, "MINE-1");
        NewContainer(db, theirs, "THEIRS-1");
        await db.SaveChangesAsync();

        var rows = await new ContainerRepository(db).ListAsync(mine, null);

        Assert.Single(rows);
        Assert.Equal("MINE-1", rows[0].Code);
    }

    [Fact]
    public async Task A_container_of_another_tenant_is_not_found_by_id()
    {
        await using var db = fixture.Create();
        Guid mine = NewTenant(db), theirs = NewTenant(db);
        var foreign = NewContainer(db, theirs, "THEIRS-2");
        await db.SaveChangesAsync();

        Assert.Null(await new ContainerRepository(db).FindAsync(mine, foreign.Id));
    }

    /// <summary>
    /// ⚠️ ورمز محجوز في شركة تانية <b>مش</b> محجوز هنا.
    /// </summary>
    [Fact]
    public async Task A_code_taken_in_another_tenant_is_free_here()
    {
        await using var db = fixture.Create();
        Guid mine = NewTenant(db), theirs = NewTenant(db);
        NewContainer(db, theirs, "SH-9");
        await db.SaveChangesAsync();

        var repository = new ContainerRepository(db);

        Assert.False(await repository.CodeTakenAsync(mine, Core.Text.ContainerCode.Normalize("SH-9")));
        Assert.True(await repository.CodeTakenAsync(theirs, Core.Text.ContainerCode.Normalize("sh 9")));
    }

    // =================================================================
    //  البحث — على العمود المطبَّع
    // =================================================================

    /// <summary>
    /// 🔴 البحث بأي شكل مكتوب بيلاقي الحاوية — <b>في SQL مش في
    /// الذاكرة</b>.
    /// </summary>
    [Theory]
    [InlineData("sh-2024-01")]
    [InlineData("SH202401")]
    [InlineData("2024")]
    public async Task Search_matches_the_normalised_column(string typed)
    {
        await using var db = fixture.Create();
        Guid tenant = NewTenant(db);
        NewContainer(db, tenant, "SH-2024/01");
        await db.SaveChangesAsync();

        Assert.Single(await new ContainerRepository(db).ListAsync(tenant, typed));
    }

    /// <summary>
    /// ⚠️ وبحث مفيهوش ولا حرف ولا رقم بيتجاهل — <b>مش بيفضّي
    /// القايمة</b>.
    /// </summary>
    [Fact]
    public async Task A_junk_search_returns_everything_not_nothing()
    {
        await using var db = fixture.Create();
        Guid tenant = NewTenant(db);
        NewContainer(db, tenant, "A-1");
        NewContainer(db, tenant, "B-2");
        await db.SaveChangesAsync();

        Assert.Equal(2, (await new ContainerRepository(db).ListAsync(tenant, "---")).Count);
    }

    // =================================================================
    //  العدّ — والمدموج
    // =================================================================

    /// <summary>
    /// 🔴 <b>الجهاز المدموج مستبعد من المحتوى.</b>
    ///
    /// <para>صفه بيفضل عشان التاريخ والكود المتقاعد، بس عدّه هنا
    /// بيقول «٣ لابات في الحاوية» والحقيقة ٢.</para>
    /// </summary>
    [Fact]
    public async Task Merged_devices_are_excluded_from_the_contents()
    {
        await using var db = fixture.Create();
        Guid tenant = NewTenant(db);
        var container = NewContainer(db, tenant, "SH-M");
        NewDevice(db, tenant, container.Id, "LP-1");
        NewDevice(db, tenant, container.Id, "LP-2");
        NewDevice(db, tenant, container.Id, "LP-3", DeviceLifecycleStatus.Merged);
        await db.SaveChangesAsync();

        var devices = await new ContainerRepository(db).DevicesAsync(tenant, container.Id);

        Assert.Equal(2, devices.Count);
        Assert.DoesNotContain(devices, d => d.PublicCode == "LP-3");
    }

    /// <summary>
    /// ⚠️ <b>بس عدّاد القايمة بيحسب المدموج — زي القديم بالحرف.</b>
    ///
    /// <para>الفرق ده موجود في المشروع القديم: عدّاد القايمة
    /// <c>db.Devices.Count(...)</c> من غير شرط الدمج، ومحتوى الحاوية
    /// بيستبعده. <b>مانقلناه زي ما هو عن قصد</b> — تصليحه هنا بيخلّي
    /// نفس الصفحة تقول رقمين مختلفين على حسب الشاشتين.</para>
    ///
    /// <para>🔴 والتصليح مكانه بعد التحويل، ولازم يتعمل في الاتنين مع
    /// بعض.</para>
    /// </summary>
    [Fact]
    public async Task The_list_counter_still_includes_merged_devices_like_the_old_project()
    {
        await using var db = fixture.Create();
        Guid tenant = NewTenant(db);
        var container = NewContainer(db, tenant, "SH-C");
        NewDevice(db, tenant, container.Id, "LP-4");
        NewDevice(db, tenant, container.Id, "LP-5", DeviceLifecycleStatus.Merged);
        await db.SaveChangesAsync();

        var rows = await new ContainerRepository(db).ListAsync(tenant, null);

        Assert.Equal(2, rows.Single().DeviceCount);
    }

    /// <summary>
    /// 🔴 <b>الفحوص الممسوحة مستبعدة من العدّ.</b>
    ///
    /// <para>عدّاد بيحسب فحص اتمسح بيدّي تاريخ جهاز غلط.</para>
    /// </summary>
    [Fact]
    public async Task Deleted_reports_are_not_counted()
    {
        await using var db = fixture.Create();
        Guid tenant = NewTenant(db);
        var container = NewContainer(db, tenant, "SH-R");
        var device = NewDevice(db, tenant, container.Id, "LP-6");
        await db.SaveChangesAsync();

        db.Reports.Add(new Report
        {
            Id = Guid.NewGuid(), TenantId = tenant, DeviceId = device.Id,
            StartedAtUtc = DateTime.UtcNow.AddHours(-2), IsDeleted = false,
            FailCount = 1, ErrorCount = 2,
        });
        db.Reports.Add(new Report
        {
            Id = Guid.NewGuid(), TenantId = tenant, DeviceId = device.Id,
            StartedAtUtc = DateTime.UtcNow.AddHours(-1), IsDeleted = true,
            FailCount = 9, ErrorCount = 9,
        });
        await db.SaveChangesAsync();

        var devices = await new ContainerRepository(db).DevicesAsync(tenant, container.Id);

        Assert.Equal(1, devices.Single().TestCount);

        // ⚠️ و«مشاكل آخر فحص» بتتحسب من الفحص الشغّال، مش الممسوح.
        Assert.Equal(3, devices.Single().LastProblems);
    }

    /// <summary>
    /// ⚠️ والاسم التجاري بيسبق الموديل الخام.
    ///
    /// <para>الفني بيعرف اللاب بـ«ThinkPad T480» مش بـ«20L5».</para>
    /// </summary>
    [Fact]
    public async Task The_commercial_name_wins_over_the_raw_model()
    {
        await using var db = fixture.Create();
        Guid tenant = NewTenant(db);
        var container = NewContainer(db, tenant, "SH-N");
        var named = NewDevice(db, tenant, container.Id, "LP-7");
        named.CommercialModelName = "ThinkPad T480";
        NewDevice(db, tenant, container.Id, "LP-8");
        await db.SaveChangesAsync();

        var devices = await new ContainerRepository(db).DevicesAsync(tenant, container.Id);

        Assert.Equal("ThinkPad T480", devices.Single(d => d.PublicCode == "LP-7").Model);
        Assert.Equal("20L5", devices.Single(d => d.PublicCode == "LP-8").Model);
    }

    /// <summary>⚠️ والترتيب بـ<c>SortOrder</c> الأول.</summary>
    [Fact]
    public async Task The_list_is_ordered_by_sort_order()
    {
        await using var db = fixture.Create();
        Guid tenant = NewTenant(db);
        NewContainer(db, tenant, "LAST", sortOrder: 9);
        NewContainer(db, tenant, "FIRST", sortOrder: 1);
        await db.SaveChangesAsync();

        var rows = await new ContainerRepository(db).ListAsync(tenant, null);

        Assert.Equal("FIRST", rows[0].Code);
        Assert.Equal("LAST", rows[1].Code);
    }

    /// <summary>
    /// ⚠️ والحاوية الموقوفة بترجع في القايمة، بعلامة.
    ///
    /// <para>لابات كتير بتشاور على حاوية اتوقفت، ولو اختفت اسمها كان
    /// هيبان فاضي من غير أي تفسير.</para>
    /// </summary>
    [Fact]
    public async Task Inactive_containers_are_still_listed()
    {
        await using var db = fixture.Create();
        Guid tenant = NewTenant(db);
        var stopped = NewContainer(db, tenant, "OFF-1");
        stopped.IsActive = false;
        await db.SaveChangesAsync();

        var rows = await new ContainerRepository(db).ListAsync(tenant, null);

        Assert.Single(rows);
        Assert.False(rows[0].IsActive);
    }
}
