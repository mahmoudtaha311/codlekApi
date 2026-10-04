using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Codlek.Infrastructure.Data;
using Codlek.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Codlek.Tests;

/// <summary>
/// التغذيات النازلة — <b>على قاعدة حقيقية</b>.
///
/// <para>🔴 <b>والملف ده موجود لسبب مقاس.</b> كل قاعدة في التغذيات
/// دي عايشة <b>جوّه استعلام</b>: التقييد بالشركة، «المفتوحة بس»،
/// المقارنة الصارمة، الترتيب، وفاصل التعادل. والمزيّف بينفّذهم
/// بنفسه — يعني شيل أي واحد من الاستعلام الحقيقي بيعدّي من تحت كل
/// فحوص الوحدة.</para>
/// </summary>
public class RackFeedRepositoryTests(RackFeedDbFixture fixture)
    : IClassFixture<RackFeedDbFixture>
{
    private static Guid NewTenant(AppDbContext db)
    {
        var tenant = new Tenant { Name = "ورشة " + Guid.NewGuid().ToString("N")[..6] };
        db.Tenants.Add(tenant);
        return tenant.Id;
    }

    private static Technician NewTech(
        AppDbContext db, Guid tenantId, string name, string code,
        bool isActive = true, bool canRepair = true)
    {
        var tech = new Technician
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = name,
            Username = code,
            NormalizedUsername = code,
            PasswordHash = "SECRET-HASH",
            Salt = "SECRET-SALT",
            IsActive = isActive,
            CanRepair = canRepair,
        };

        db.Technicians.Add(tech);
        return tech;
    }

    private static Device NewDevice(
        AppDbContext db, Guid tenantId, string code,
        string model = "Latitude 5400", string? commercial = null,
        string manufacturer = "Dell Inc.", Guid? containerId = null)
    {
        var device = new Device
        {
            TenantId = tenantId,
            PublicCode = code,
            LastKnownModel = model,
            CommercialModelName = commercial,
            LastKnownManufacturer = manufacturer,
            ContainerId = containerId,
        };

        db.Devices.Add(device);
        return device;
    }

    private static RepairWorkItem NewRepair(
        AppDbContext db, Guid tenantId, Device device, string code,
        RepairStatus status = RepairStatus.New,
        Technician? assigned = null)
    {
        var item = new RepairWorkItem
        {
            TenantId = tenantId,
            PublicCode = code,
            DeviceId = device.Id,
            Status = status,
            AssignedTechnicianId = assigned?.Id,
            OpenedByName = "كريم",
            FaultSummary = "الشاشة بتطفى",
        };

        db.RepairWorkItems.Add(item);
        return item;
    }

    // =================================================================
    //  قايمة فنيي الصيانة
    // =================================================================

    /// <summary>
    /// 🔴 <b>البصمة والملح عمرهم ما يخرجوا من المستودع.</b>
    ///
    /// <para>الصف الضيّق بيخلّي التسريب <b>مستحيل</b> مش «ممنوع»: لو
    /// رجّعنا كيان <c>Technician</c>، سطر إسقاط واحد مكتوب بسرعة في
    /// المعالج بيحطّ البصمة على السلك.</para>
    /// </summary>
    [Fact]
    public async Task The_roster_row_has_no_place_to_put_a_hash()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        NewTech(db, tenant, "أحمد", "R00001");

        await db.SaveChangesAsync();

        var row = Assert.Single(await new RackFeedRepository(db).RepairRosterAsync(tenant));

        foreach (var property in row.GetType().GetProperties())
        {
            if (property.GetValue(row) is not string value) continue;

            Assert.DoesNotContain("SECRET", value, StringComparison.Ordinal);
        }

        // ⚠️ وحراسة: الصف فيه الاسم فعلاً — عشان الفحص مايبقاش
        //    بيفحص كائن فاضي.
        Assert.Equal("أحمد", row.DisplayName);
    }

    /// <summary>
    /// 🔴 <b>التقييد بشركة المحطة هو الحاجز.</b> من غيره أي راكة
    /// بتقرا فنيي كل الشركات على نفس السيرفر.
    /// </summary>
    [Fact]
    public async Task The_roster_is_scoped_to_the_workshop_and_to_repairers()
    {
        using var db = fixture.Create();
        var mine = NewTenant(db);
        var theirs = NewTenant(db);

        NewTech(db, mine, "شغّال بيصلّح", "R10001");
        NewTech(db, mine, "موقوف", "R10002", isActive: false);
        NewTech(db, mine, "مش بيصلّح", "R10003", canRepair: false);
        NewTech(db, theirs, "ورشة تانية", "R10004");

        await db.SaveChangesAsync();

        var rows = await new RackFeedRepository(db).RepairRosterAsync(mine);

        Assert.Equal("شغّال بيصلّح", Assert.Single(rows).DisplayName);
    }

    /// <summary>
    /// ⚠️ <b>وفاصل التعادل إجباري.</b> فنيين بنفس الاسم المعروض
    /// بيتقلبوا بين الطلبات من غيره — والراكة بتخزّن القايمة، فترتيب
    /// متقلّب بيبان كأن القايمة بتتغيّر.
    /// </summary>
    [Fact]
    public async Task Both_feed_listings_end_with_a_unique_column()
    {
        var sql = new List<string>();

        using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(fixture.ConnectionString)
                .LogTo(sql.Add, [DbLoggerCategory.Database.Command.Name], LogLevel.Information)
                .Options);

        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "LP-90000001");

        NewTech(db, tenant, "فني", "R20001");
        NewRepair(db, tenant, device, "RP-20001");

        await db.SaveChangesAsync();

        var repo = new RackFeedRepository(db);

        sql.Clear();

        await repo.RepairRosterAsync(tenant);
        await repo.ContainersAsync(tenant);
        await repo.AssignedRepairsAsync(tenant, null, 10);

        // ⚠️ من **آخر** ORDER BY في النص مش أولها — الاستعلامات
        //    الفرعية ليها ترتيبها.
        var ordered = sql
            .Where(line => line.Contains("ORDER BY", StringComparison.Ordinal))
            .Select(line => line[line.LastIndexOf("ORDER BY", StringComparison.Ordinal)..])
            .ToList();

        Assert.Equal(3, ordered.Count);

        Assert.All(ordered, clause =>
            Assert.Contains("[Id]", clause, StringComparison.Ordinal));
    }

    // =================================================================
    //  الحاويات
    // =================================================================

    [Fact]
    public async Task Containers_are_scoped_active_and_counted()
    {
        using var db = fixture.Create();
        var mine = NewTenant(db);
        var theirs = NewTenant(db);

        var box = new ImportContainer
        {
            TenantId = mine, Code = "C-1", NormalizedCode = "c-1",
            Name = "شحنة يناير", SortOrder = 1,
        };

        var dead = new ImportContainer
        {
            TenantId = mine, Code = "C-2", NormalizedCode = "c-2",
            IsActive = false, SortOrder = 2,
        };

        var elsewhere = new ImportContainer
        {
            TenantId = theirs, Code = "C-3", NormalizedCode = "c-3", SortOrder = 3,
        };

        db.Containers.AddRange(box, dead, elsewhere);

        await db.SaveChangesAsync();

        NewDevice(db, mine, "LP-91000001", containerId: box.Id);
        NewDevice(db, mine, "LP-91000002", containerId: box.Id);
        NewDevice(db, mine, "LP-91000003");

        await db.SaveChangesAsync();

        var row = Assert.Single(await new RackFeedRepository(db).ContainersAsync(mine));

        Assert.Equal("C-1", row.Code);
        Assert.Equal("شحنة يناير", row.Name);
        Assert.Equal(2, row.DeviceCount);
    }

    // =================================================================
    //  الأوامر المسنودة
    // =================================================================

    /// <summary>
    /// ⚠️ <b>المفتوحة بس.</b> الأمر اللي خلص أو اتلغى مالوش أي شغل
    /// على راكة، وسحبه كان معناه إن كل راكة بتحمّل تاريخ الورشة كله
    /// مع الوقت.
    /// </summary>
    [Fact]
    public async Task Only_open_orders_travel_down()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "LP-92000001");

        await db.SaveChangesAsync();

        NewRepair(db, tenant, device, "RP-OPEN-1", RepairStatus.New);
        NewRepair(db, tenant, device, "RP-OPEN-2", RepairStatus.WaitingForRepair);
        NewRepair(db, tenant, device, "RP-OPEN-3", RepairStatus.InProgress);
        NewRepair(db, tenant, device, "RP-DONE", RepairStatus.Completed);
        NewRepair(db, tenant, device, "RP-GONE", RepairStatus.Cancelled);

        await db.SaveChangesAsync();

        var rows = await new RackFeedRepository(db).AssignedRepairsAsync(tenant, null, 50);

        Assert.Equal(3, rows.Count);
        Assert.All(rows, r => Assert.StartsWith("RP-OPEN", r.PublicCode));
    }

    [Fact]
    public async Task Orders_are_scoped_to_the_workshop()
    {
        using var db = fixture.Create();
        var mine = NewTenant(db);
        var theirs = NewTenant(db);

        var ours = NewDevice(db, mine, "LP-93000001");
        var hers = NewDevice(db, theirs, "LP-93000002");

        await db.SaveChangesAsync();

        NewRepair(db, mine, ours, "RP-MINE");
        NewRepair(db, theirs, hers, "RP-THEIRS");

        await db.SaveChangesAsync();

        var rows = await new RackFeedRepository(db).AssignedRepairsAsync(mine, null, 50);

        Assert.Equal("RP-MINE", Assert.Single(rows).PublicCode);
    }

    /// <summary>
    /// 🔴 <b>والمقارنة <c>&gt;</c> صارمة.</b> الراكة بتبعت آخر وقت
    /// <b>شافته</b>، فـ<c>&gt;=</c> كان بيرجّع آخر صف في كل سحبة
    /// للأبد — الصف بيبان كأنه بيتغيّر وهو ساكن.
    /// </summary>
    [Fact]
    public async Task The_cursor_excludes_the_row_it_was_taken_from()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "LP-94000001");

        await db.SaveChangesAsync();

        NewRepair(db, tenant, device, "RP-FIRST");

        await db.SaveChangesAsync();

        var repo = new RackFeedRepository(db);

        var first = await repo.AssignedRepairsAsync(tenant, null, 50);
        var mark = Assert.Single(first).UpdatedAtUtc;

        Assert.Empty(await repo.AssignedRepairsAsync(tenant, mark, 50));

        // ⚠️ وحراسة: علامة أقدم بجزء من المللي **بترجّع** الصف —
        //    عشان الفحص مايبقاش بيقيس استعلام فاضي.
        Assert.Single(await repo.AssignedRepairsAsync(tenant, mark.AddMilliseconds(-1), 50));
    }

    /// <summary>
    /// 🔴 <b>الماركة الخام — كانت بتتشال خالص.</b> الموجز بيركّب اسم
    /// الجهاز من الموديل بس، فالراكة <b>مكانتش بتعرف ماركة اللاب
    /// أصلاً</b> — وقاعدة «الفني ده لماركات معيّنة» مستحيلة من
    /// غيرها.
    /// </summary>
    [Fact]
    public async Task The_raw_manufacturer_travels_down_untouched()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var device = NewDevice(
            db, tenant, "LP-95000001", manufacturer: "Hewlett-Packard");

        await db.SaveChangesAsync();

        NewRepair(db, tenant, device, "RP-BRAND");

        await db.SaveChangesAsync();

        var row = Assert.Single(
            await new RackFeedRepository(db).AssignedRepairsAsync(tenant, null, 50));

        // ⚠️ خام زي ما ويندوز قالها — التوحيد بقاعدة مشتركة في
        //    الطرفين مش بتخمين هنا.
        Assert.Equal("Hewlett-Packard", row.DeviceManufacturer);
    }

    /// <summary>
    /// ⚠️ <b>الاسم التجاري لو موجود، وإلا الموديل الخام</b> — نفس
    /// ترتيب الشاشات، فالفني بيشوف نفس الاسم على الراكة وعلى الموقع.
    /// </summary>
    [Fact]
    public async Task The_commercial_name_wins_and_the_model_is_the_fallback()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var named = NewDevice(
            db, tenant, "LP-96000001", model: "20XW", commercial: "ThinkPad T14");

        var raw = NewDevice(db, tenant, "LP-96000002", model: "20XW");

        await db.SaveChangesAsync();

        NewRepair(db, tenant, named, "RP-NAMED");
        NewRepair(db, tenant, raw, "RP-RAW");

        await db.SaveChangesAsync();

        var rows = await new RackFeedRepository(db).AssignedRepairsAsync(tenant, null, 50);

        Assert.Equal(
            "ThinkPad T14",
            rows.Single(r => r.PublicCode == "RP-NAMED").DeviceName);

        Assert.Equal("20XW", rows.Single(r => r.PublicCode == "RP-RAW").DeviceName);
    }

    /// <summary>
    /// ⚠️ <b>واسم الفني المسنود بيترجع، والمش مسنود بيرجع
    /// فاضي.</b> الراكة بتعرض الاسم زي ما جه، ومفيش <c>null</c> على
    /// السلك.
    /// </summary>
    [Fact]
    public async Task An_unassigned_order_has_an_empty_name_not_a_null()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "LP-97000001");
        var tech = NewTech(db, tenant, "سامي", "R30001");

        await db.SaveChangesAsync();

        NewRepair(db, tenant, device, "RP-ASSIGNED", assigned: tech);
        NewRepair(db, tenant, device, "RP-FREE");

        await db.SaveChangesAsync();

        var rows = await new RackFeedRepository(db).AssignedRepairsAsync(tenant, null, 50);

        Assert.Equal(
            "سامي", rows.Single(r => r.PublicCode == "RP-ASSIGNED").AssignedTechnicianName);

        var free = rows.Single(r => r.PublicCode == "RP-FREE");

        Assert.Equal("", free.AssignedTechnicianName);
        Assert.Null(free.AssignedTechnicianId);
    }

    /// <summary>
    /// 🔴 <b>الترتيب بالوقت تصاعدي — <u>وده شرط على صحة السحب
    /// التراكمي</u> مش تجميل.</b>
    ///
    /// <para>العلامة بتتاخد من <b>آخر</b> صف. لو الترتيب تنازلي،
    /// العلامة بتبقى <b>أقدم</b> صف في الصفحة — والراكة بتفضل تسحب
    /// نفس الحاجة للأبد ومابتتقدّمش خطوة.</para>
    /// </summary>
    [Fact]
    public async Task Rows_come_back_oldest_first()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "LP-98000001");

        await db.SaveChangesAsync();

        // ⚠️ حفظات منفصلة — `StampRepairCursor` بتحط نفس الوقت على
        //    كل اللي اتعدّل في الحفظة الواحدة.
        NewRepair(db, tenant, device, "RP-OLD");
        await db.SaveChangesAsync();

        NewRepair(db, tenant, device, "RP-NEW");
        await db.SaveChangesAsync();

        var rows = await new RackFeedRepository(db).AssignedRepairsAsync(tenant, null, 50);

        Assert.Equal(["RP-OLD", "RP-NEW"], rows.Select(r => r.PublicCode));
    }

    /// <summary>
    /// 🔴 <b>وفجوة محفوظة من القديم — <u>مقصود إننا مانصلّحهاش
    /// هنا</u>.</b>
    ///
    /// <para><c>StampRepairCursor</c> بتحط <b>نفس</b> الوقت على كل
    /// أمر اتعدّل في نفس الحفظة، والعلامة على السلك وقت لوحده
    /// (<c>?since=</c> مافيهاش معرّف). فلو صفوف متساوية في الوقت
    /// اتقسمت على حدّ صفحة، السحبة الجاية بـ<c>&gt;</c> بتتخطّى
    /// الباقي منهم <b>للأبد</b>.</para>
    ///
    /// <para>⚠️ والفحص ده بيوثّق الفجوة مش بيعالجها: العلاج محتاج
    /// علامة مركّبة (وقت + معرّف) — وده <b>تغيير في السلك</b>،
    /// والراكات المنزّلة بتبعت الوقت لوحده. اللي عملناه هنا فاصل
    /// التعادل بالمعرّف، فالترتيب على الأقل <b>ثابت</b> بدل ما يبقى
    /// على مزاج الخطة.</para>
    /// </summary>
    [Fact]
    public async Task Rows_sharing_one_timestamp_are_at_least_ordered_the_same_way_twice()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "LP-99000001");

        await db.SaveChangesAsync();

        // حفظة واحدة = وقت واحد على التلاتة.
        NewRepair(db, tenant, device, "RP-T1");
        NewRepair(db, tenant, device, "RP-T2");
        NewRepair(db, tenant, device, "RP-T3");

        await db.SaveChangesAsync();

        var repo = new RackFeedRepository(db);

        var first = await repo.AssignedRepairsAsync(tenant, null, 50);
        var again = await repo.AssignedRepairsAsync(tenant, null, 50);

        Assert.Equal(1, first.Select(r => r.UpdatedAtUtc).Distinct().Count());

        // ⚠️ نفس الترتيب في الطلبين — ده اللي فاصل التعادل بيضمنه.
        Assert.Equal(
            first.Select(r => r.PublicCode), again.Select(r => r.PublicCode));

        // 🔴 والفجوة نفسها: صفحة من واحد بتسيب اتنين بره للأبد.
        var page = await repo.AssignedRepairsAsync(tenant, null, 1);
        var mark = Assert.Single(page).UpdatedAtUtc;

        Assert.Empty(await repo.AssignedRepairsAsync(tenant, mark, 50));
    }
}
