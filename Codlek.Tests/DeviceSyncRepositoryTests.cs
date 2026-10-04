using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Codlek.Infrastructure.Data;
using Codlek.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Codlek.Tests;

/// <summary>
/// استعلامات مزامنة الأجهزة — <b>على قاعدة حقيقية</b>.
///
/// <para>🔴 <b>والملف ده موجود لسبب واحد كبير:</b> استبعاد <b>شواهد
/// الدمج</b> عايش جوّه الاستعلامات، والمزيّف بينفّذه بنفسه.</para>
///
/// <para>اللي اتشاف في الإنتاج: المدير بيدمج الجهازين، الحالة بتتصفّى
/// صح، وأول مزامنة من الراكة بترجّع الكانوني «يُشتبه أنه مكرر» تاني —
/// <b>بيشتبه في نفسه</b>، لأن التوأم الوحيد هو شاهد دمجه هو. وده كان
/// بيعمل تنطيط: الحالة بتروح وتيجي حسب أنهي مسار اشتغل آخر حاجة،
/// والمدير بيقفل التحذير ويلاقيه رجع تاني الصبح.</para>
/// </summary>
public class DeviceSyncRepositoryTests(DeviceSyncDbFixture fixture)
    : IClassFixture<DeviceSyncDbFixture>
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
        Guid? mergedInto = null)
    {
        var device = new Device
        {
            TenantId = tenantId,
            PublicCode = code,
            Status = status,
            MergedIntoDeviceId = mergedInto,
            LastKnownManufacturer = "Dell Inc.",
            LastKnownModel = "Latitude 5400",
        };

        db.Devices.Add(device);
        return device;
    }

    private static void Anchor(
        AppDbContext db, Guid tenantId, Guid deviceId, string value,
        DeviceIdentifierKind kind = DeviceIdentifierKind.BiosSerial,
        bool active = true)
    {
        db.DeviceIdentifiers.Add(new DeviceIdentifierRow
        {
            TenantId = tenantId,
            DeviceId = deviceId,
            Kind = kind,
            RawValue = value.ToLowerInvariant(),
            NormalizedValue = value,
            IsActive = active,
        });
    }

    // =================================================================
    //  المطابقة بالمرساة
    // =================================================================

    /// <summary>
    /// 🔴 <b>وشاهد الدمج مابيتطابقش — وده اللي بيمنع الجهاز يشتبه في
    /// نفسه.</b>
    /// </summary>
    [Fact]
    public async Task A_merged_device_never_matches_an_anchor()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var canonical = NewDevice(db, tenant, "LP-20000001");
        var merged = NewDevice(db, tenant, "LP-20000002");

        await db.SaveChangesAsync();

        merged.Status = DeviceLifecycleStatus.Merged;
        merged.MergedIntoDeviceId = canonical.Id;

        // 🔴 **ومراسي المدموج بتفضل نشطة عليه** — وده مقصود، التاريخ
        //    مابيتمسحش.
        Anchor(db, tenant, canonical.Id, "SHARED-ANCHOR");
        Anchor(db, tenant, merged.Id, "SHARED-ANCHOR");

        await db.SaveChangesAsync();

        var found = await new DeviceSyncRepository(db).MatchAnchorAsync(
            tenant, DeviceIdentifierKind.BiosSerial, "SHARED-ANCHOR", null, 5);

        Assert.Equal(canonical.Id, Assert.Single(found));
    }

    /// <summary>⚠️ والمرساة المتقاعدة مابتطابقش.</summary>
    [Fact]
    public async Task A_retired_anchor_never_matches()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var device = NewDevice(db, tenant, "LP-20100001");

        await db.SaveChangesAsync();

        Anchor(db, tenant, device.Id, "OLD-BOARD", active: false);

        await db.SaveChangesAsync();

        Assert.Empty(await new DeviceSyncRepository(db).MatchAnchorAsync(
            tenant, DeviceIdentifierKind.BiosSerial, "OLD-BOARD", null, 5));
    }

    /// <summary>🔴 والتقييد بالشركة هو الحاجز.</summary>
    [Fact]
    public async Task An_anchor_only_matches_inside_the_workshop()
    {
        using var db = fixture.Create();
        var mine = NewTenant(db);
        var theirs = NewTenant(db);

        var ours = NewDevice(db, mine, "LP-20200001");
        var hers = NewDevice(db, theirs, "LP-20200002");

        await db.SaveChangesAsync();

        Anchor(db, mine, ours.Id, "CROSS-TENANT");
        Anchor(db, theirs, hers.Id, "CROSS-TENANT");

        await db.SaveChangesAsync();

        var found = await new DeviceSyncRepository(db).MatchAnchorAsync(
            mine, DeviceIdentifierKind.BiosSerial, "CROSS-TENANT", null, 5);

        Assert.Equal(ours.Id, Assert.Single(found));
    }

    /// <summary>⚠️ والاستثناء بيشيل الجهاز نفسه من المطابقة.</summary>
    [Fact]
    public async Task The_excluded_device_is_left_out()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var device = NewDevice(db, tenant, "LP-20300001");

        await db.SaveChangesAsync();

        Anchor(db, tenant, device.Id, "SELF-ANCHOR");

        await db.SaveChangesAsync();

        var repo = new DeviceSyncRepository(db);

        Assert.Single(await repo.MatchAnchorAsync(
            tenant, DeviceIdentifierKind.BiosSerial, "SELF-ANCHOR", null, 5));

        Assert.Empty(await repo.MatchAnchorAsync(
            tenant, DeviceIdentifierKind.BiosSerial, "SELF-ANCHOR", device.Id, 5));
    }

    /// <summary>
    /// ⚠️ <b>والترتيب ثابت</b> — عشان نفس المدخلات تدّي نفس القرار،
    /// والقرار ده بيتكتب في سجل المراجعة.
    /// </summary>
    [Fact]
    public async Task The_matches_come_back_in_a_stable_order()
    {
        var sql = new List<string>();

        using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(fixture.ConnectionString)
                .LogTo(sql.Add, [Microsoft.EntityFrameworkCore.DbLoggerCategory.Database.Command.Name],
                    Microsoft.Extensions.Logging.LogLevel.Information)
                .Options);

        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "LP-20400001");

        await db.SaveChangesAsync();

        Anchor(db, tenant, device.Id, "ORDER-ANCHOR");

        await db.SaveChangesAsync();

        sql.Clear();

        await new DeviceSyncRepository(db).MatchAnchorAsync(
            tenant, DeviceIdentifierKind.BiosSerial, "ORDER-ANCHOR", null, 5);

        var ordered = sql
            .Where(line => line.Contains("ORDER BY", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(ordered);
    }

    // =================================================================
    //  التوائم
    // =================================================================

    /// <summary>
    /// 🔴 <b>وشاهد الدمج مش توأم — نفس القاعدة بالظبط.</b>
    /// </summary>
    [Fact]
    public async Task A_merged_device_is_not_a_twin()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var subject = NewDevice(db, tenant, "LP-20500001");
        var merged = NewDevice(db, tenant, "LP-20500002");
        var live = NewDevice(db, tenant, "LP-20500003");

        await db.SaveChangesAsync();

        merged.Status = DeviceLifecycleStatus.Merged;
        merged.MergedIntoDeviceId = subject.Id;

        Anchor(db, tenant, merged.Id, "TWIN-ANCHOR");
        Anchor(db, tenant, live.Id, "TWIN-ANCHOR");

        await db.SaveChangesAsync();

        var twins = await new DeviceSyncRepository(db)
            .TwinsByAnchorsAsync(tenant, subject.Id, ["TWIN-ANCHOR"]);

        Assert.Equal(live.Id, Assert.Single(twins));
    }

    [Fact]
    public async Task No_anchors_means_no_twins()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        Assert.Empty(await new DeviceSyncRepository(db)
            .TwinsByAnchorsAsync(tenant, Guid.NewGuid(), []));
    }

    // =================================================================
    //  تكرار الكود
    // =================================================================

    [Fact]
    public async Task A_code_on_another_device_is_a_clash()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var holder = NewDevice(db, tenant, "LP-20600001");

        await db.SaveChangesAsync();

        var repo = new DeviceSyncRepository(db);

        Assert.Equal(
            holder.Id,
            await repo.CodeClashAsync(tenant, "LP-20600001", Guid.NewGuid()));

        // ⚠️ ونفس الجهاز مش تصادم مع نفسه.
        Assert.Null(await repo.CodeClashAsync(tenant, "LP-20600001", holder.Id));
    }

    /// <summary>
    /// ⚠️ <b>والكود بيتكرر بين ورشتين بشكل شرعي</b> — كل ورشة بتطبع
    /// استيكراتها.
    /// </summary>
    [Fact]
    public async Task The_same_code_in_two_workshops_is_not_a_clash()
    {
        using var db = fixture.Create();
        var mine = NewTenant(db);
        var theirs = NewTenant(db);

        NewDevice(db, theirs, "LP-20700001");

        await db.SaveChangesAsync();

        Assert.Null(await new DeviceSyncRepository(db)
            .CodeClashAsync(mine, "LP-20700001", Guid.NewGuid()));
    }

    // =================================================================
    //  «اتغيّر فيه حاجة؟»
    // =================================================================

    /// <summary>
    /// 🔴 <b>ومرساة جديدة لوحدها = الجهاز «اتحدّث».</b>
    ///
    /// <para>والصف نفسه مش <c>Modified</c> — المراسي كيانات تانية.
    /// فسؤال «اتغيّر؟» لازم يشوفها كمان، وإلا جهاز وصلته مرساة جديدة
    /// بيرجع «زي ما هو».</para>
    /// </summary>
    [Fact]
    public async Task A_new_anchor_alone_counts_as_touched()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var device = NewDevice(db, tenant, "LP-20800001");

        await db.SaveChangesAsync();

        var repo = new DeviceSyncRepository(db);

        // مفيش أي تغيير لسه.
        Assert.False(repo.WasTouched(device));

        device.Identifiers.Add(new DeviceIdentifierRow
        {
            TenantId = tenant,
            DeviceId = device.Id,
            Kind = DeviceIdentifierKind.BiosSerial,
            RawValue = "new-anchor",
            NormalizedValue = "NEW-ANCHOR",
            IsActive = true,
        });

        Assert.True(repo.WasTouched(device));
    }

    /// <summary>⚠️ وتغيير خانة في الصف نفسه كمان.</summary>
    [Fact]
    public async Task A_changed_column_counts_as_touched()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var device = NewDevice(db, tenant, "LP-20900001");

        await db.SaveChangesAsync();

        var repo = new DeviceSyncRepository(db);

        Assert.False(repo.WasTouched(device));

        device.LastKnownModel = "Latitude 7400";

        Assert.True(repo.WasTouched(device));
    }

    /// <summary>
    /// ⚠️ <b>ومرساة جهاز <u>تاني</u> مابتعدّش.</b> دفعة فيها عشر
    /// أجهزة ماينفعش كل واحد فيهم يبان «اتحدّث» عشان التاني وصلته
    /// مرساة.
    /// </summary>
    [Fact]
    public async Task Another_devices_anchor_does_not_count()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var mine = NewDevice(db, tenant, "LP-21000001");
        var other = NewDevice(db, tenant, "LP-21000002");

        await db.SaveChangesAsync();

        other.Identifiers.Add(new DeviceIdentifierRow
        {
            TenantId = tenant,
            DeviceId = other.Id,
            Kind = DeviceIdentifierKind.BiosSerial,
            RawValue = "other-anchor",
            NormalizedValue = "OTHER-ANCHOR",
            IsActive = true,
        });

        Assert.False(new DeviceSyncRepository(db).WasTouched(mine));
    }

    // =================================================================
    //  الحاوية
    // =================================================================

    [Fact]
    public async Task A_container_is_found_by_its_normalised_code()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        db.Containers.Add(new ImportContainer
        {
            TenantId = tenant,
            Code = "C-9",
            NormalizedCode = "c9",
        });

        await db.SaveChangesAsync();

        var repo = new DeviceSyncRepository(db);

        Assert.NotNull(await repo.FindContainerAsync(tenant, "c9"));

        // ⚠️ وورشة تانية مابتشوفهاش.
        Assert.Null(await repo.FindContainerAsync(NewTenant(db), "c9"));
    }

    // =================================================================
    //  الأجهزة الشغّالة
    // =================================================================

    /// <summary>
    /// ⚠️ <b>والتعليم بيمسّ الشغّالين بس</b> — قرار المدير على جهاز
    /// متقاعد مايتلغيش.
    /// </summary>
    [Fact]
    public async Task Only_active_devices_come_back_for_flagging()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var active = NewDevice(db, tenant, "LP-21100001");
        var retired = NewDevice(db, tenant, "LP-21100002",
            status: DeviceLifecycleStatus.Retired);

        await db.SaveChangesAsync();

        var rows = await new DeviceSyncRepository(db)
            .ActiveDevicesAsync(tenant, [active.Id, retired.Id]);

        Assert.Equal(active.Id, Assert.Single(rows).Id);
    }
}
