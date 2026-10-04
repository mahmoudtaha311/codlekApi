using Codlek.Core.Devices;
using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Codlek.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Codlek.Tests;

/// <summary>قاعدة فحوص ترجمة معرّف الجهاز.</summary>
public sealed class DeviceReferenceDbFixture : SqlServerDbFixture
{
    protected override string DatabaseName => "codlek_device_ref_test";
}

/// <summary>
/// ترجمة معرّف الجهاز للكانوني <b>الحيّ</b> — على قاعدة حقيقية.
///
/// <para>🔴 <b>والتنفيذ ده كان من غير ولا فحص.</b> اتنقل في المرحلة ٤
/// ومسار الحركات بيعتمد عليه، وبعدين اتكتبت نسخة تانية منه في مسار
/// الاستقبال <b>من غير ما حد ياخد باله إنه موجود</b>. نسختين لنفس
/// القاعدة هو بالظبط نوع العطل اللي القديم موثّقه: البوابة كانت بتدوّر
/// في جدول الأجهزة بس والاستقبال بيترجم صح، فالفحص كان بيترفض «الجهاز
/// لسه ماوصلش» وهو واصل — للأبد. النسخة التانية اتشالت، والفحوص بقت
/// هنا على الوحيدة.</para>
///
/// <para>🔴 <b>والقاعدة:</b> «موجود» معناها <b>حيّ</b> مش «ليه صف».
/// الدمج مابيمسحش صف المكرر (بيسيبه شاهد قبر) ومابيعيدش توجيه الأسامي
/// المستعارة القديمة — فـ«أ ← ب» اللي اتعمل قبل «ب ← ج» بيفضل بيشاور
/// على شاهد قبر.</para>
/// </summary>
public class DeviceReferenceTests(DeviceReferenceDbFixture fixture)
    : IClassFixture<DeviceReferenceDbFixture>
{
    private static Guid NewTenant(AppDbContext db)
    {
        var tenant = new Tenant { Name = "ورشة " + Guid.NewGuid().ToString("N")[..6] };
        db.Tenants.Add(tenant);
        return tenant.Id;
    }

    private static Device NewDevice(AppDbContext db, Guid tenantId)
    {
        var device = new Device
        {
            TenantId = tenantId,
            PublicCode = "LP-" + Random.Shared.Next(10_000_000, 99_999_999),
        };

        db.Devices.Add(device);
        return device;
    }

    private static void Merge(Device dead, Guid into)
    {
        dead.Status = DeviceLifecycleStatus.Merged;
        dead.MergedIntoDeviceId = into;
    }

    private static void Alias(AppDbContext db, Guid tenantId, Guid from, Guid to) =>
        db.DeviceAliases.Add(new DeviceAlias
        {
            TenantId = tenantId,
            AliasDeviceId = from,
            CanonicalDeviceId = to,
        });

    private DeviceReference Resolver(AppDbContext db) =>
        new(db, NullLogger<DeviceReference>.Instance);

    // =================================================================
    //  الدالة النقية
    // =================================================================

    /// <summary>
    /// ⚠️ <b>التلات شروط مع بعض.</b> صف <c>Merged</c> من غير هدف (أو
    /// بهدف فاضي) = بيانات ناقصة، واتباعها بيوصل لـ<c>Guid.Empty</c>
    /// ويرجّع «مش موجود». فبنوقف عنده ونعتبره الكانوني — أهون من حركة
    /// بتترفض.
    /// </summary>
    [Fact]
    public void A_tombstone_needs_all_three_conditions()
    {
        var target = Guid.NewGuid();

        Assert.Equal(target, DeviceMergeWalk.MergedInto(DeviceLifecycleStatus.Merged, target));

        Assert.Null(DeviceMergeWalk.MergedInto(DeviceLifecycleStatus.Merged, null));
        Assert.Null(DeviceMergeWalk.MergedInto(DeviceLifecycleStatus.Merged, Guid.Empty));

        // ⚠️ وهدف مكتوب على جهاز مش مدموج = مش شاهد قبر.
        Assert.Null(DeviceMergeWalk.MergedInto(DeviceLifecycleStatus.Active, target));
        Assert.Null(DeviceMergeWalk.MergedInto(DeviceLifecycleStatus.DuplicateSuspected, target));
    }

    [Fact]
    public void An_empty_id_is_not_askable_and_a_revisit_is_a_loop()
    {
        Assert.False(DeviceMergeWalk.IsAskable(Guid.Empty));
        Assert.True(DeviceMergeWalk.IsAskable(Guid.NewGuid()));

        var seen = new HashSet<Guid>();
        var id = Guid.NewGuid();

        Assert.True(DeviceMergeWalk.CanVisit(seen, id));
        Assert.False(DeviceMergeWalk.CanVisit(seen, id));
    }

    // =================================================================
    //  معرّف واحد
    // =================================================================

    [Fact]
    public async Task A_live_device_resolves_to_itself()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant);

        await db.SaveChangesAsync();

        Assert.Equal(device.Id, await Resolver(db).ResolveAsync(tenant, device.Id));
    }

    [Fact]
    public async Task An_unknown_or_empty_id_resolves_to_nothing()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        await db.SaveChangesAsync();

        Assert.Null(await Resolver(db).ResolveAsync(tenant, Guid.NewGuid()));
        Assert.Null(await Resolver(db).ResolveAsync(tenant, Guid.Empty));
    }

    /// <summary>
    /// 🔴 <b>وسلسلة دمج من تلات خطوات بتوصل للحيّ.</b>
    /// </summary>
    [Fact]
    public async Task A_three_hop_merge_chain_reaches_the_living_device()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var a = NewDevice(db, tenant);
        var b = NewDevice(db, tenant);
        var c = NewDevice(db, tenant);
        var d = NewDevice(db, tenant);

        await db.SaveChangesAsync();

        Merge(a, b.Id);
        Merge(b, c.Id);
        Merge(c, d.Id);

        await db.SaveChangesAsync();

        Assert.Equal(d.Id, await Resolver(db).ResolveAsync(tenant, a.Id));
    }

    /// <summary>
    /// 🔴 <b>والحلقة المقفولة بترجع «مش معروف» — مش بتعلّق
    /// الطلب.</b>
    /// </summary>
    [Fact]
    public async Task A_closed_loop_gives_up()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var a = NewDevice(db, tenant);
        var b = NewDevice(db, tenant);

        await db.SaveChangesAsync();

        Merge(a, b.Id);
        Merge(b, a.Id);

        await db.SaveChangesAsync();

        Assert.Null(await Resolver(db).ResolveAsync(tenant, a.Id));

        var map = await Resolver(db).ResolveManyAsync(tenant, [a.Id]);
        Assert.Empty(map);
    }

    /// <summary>⚠️ والسلسلة الأطول من السقف بترجع «مش معروف».</summary>
    [Fact]
    public async Task A_chain_longer_than_the_cap_gives_up()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var chain = Enumerable.Range(0, DeviceMergeWalk.MaxHops + 2)
            .Select(_ => NewDevice(db, tenant))
            .ToList();

        await db.SaveChangesAsync();

        for (int i = 0; i < chain.Count - 1; i++) Merge(chain[i], chain[i + 1].Id);

        await db.SaveChangesAsync();

        Assert.Null(await Resolver(db).ResolveAsync(tenant, chain[0].Id));

        // ⚠️ وحراسة: من نص السلسلة (أقصر من السقف) بيوصل.
        Assert.Equal(
            chain[^1].Id,
            await Resolver(db).ResolveAsync(tenant, chain[^3].Id));
    }

    /// <summary>⚠️ وشاهد قبر بيشاور على لا حاجة = «مش معروف».</summary>
    [Fact]
    public async Task A_tombstone_pointing_nowhere_is_unknown()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var dead = NewDevice(db, tenant);

        await db.SaveChangesAsync();

        Merge(dead, Guid.NewGuid());

        await db.SaveChangesAsync();

        Assert.Null(await Resolver(db).ResolveAsync(tenant, dead.Id));
    }

    // =================================================================
    //  بالجملة
    // =================================================================

    /// <summary>
    /// 🔴 <b>دفعة مخلوطة: حيّ، ومستعار، وشاهد قبر، ومجهول.</b> والمجهول
    /// <b>مابيظهرش في الخريطة خالص</b> — «السيرفر عمره ما شافه» مش
    /// «شافه وماعرفش يترجمه».
    /// </summary>
    [Fact]
    public async Task A_mixed_batch_resolves_each_kind_correctly()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var alive = NewDevice(db, tenant);
        var canonical = NewDevice(db, tenant);
        var dead = NewDevice(db, tenant);

        await db.SaveChangesAsync();

        var local = Guid.NewGuid();
        Alias(db, tenant, local, canonical.Id);
        Merge(dead, canonical.Id);

        await db.SaveChangesAsync();

        var unknown = Guid.NewGuid();

        var map = await Resolver(db).ResolveManyAsync(
            tenant, [alive.Id, local, dead.Id, unknown]);

        Assert.Equal(alive.Id, map[alive.Id]);
        Assert.Equal(canonical.Id, map[local]);
        Assert.Equal(canonical.Id, map[dead.Id]);
        Assert.False(map.ContainsKey(unknown));
    }

    /// <summary>⚠️ وحلقة في سلسلة واحدة مابتوقّفش الباقي.</summary>
    [Fact]
    public async Task A_broken_chain_does_not_poison_the_batch()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var a = NewDevice(db, tenant);
        var b = NewDevice(db, tenant);
        var good = NewDevice(db, tenant);

        await db.SaveChangesAsync();

        Merge(a, b.Id);
        Merge(b, a.Id);

        await db.SaveChangesAsync();

        var map = await Resolver(db).ResolveManyAsync(tenant, [a.Id, good.Id]);

        Assert.Equal(good.Id, map[good.Id]);
        Assert.False(map.ContainsKey(a.Id));
    }

    /// <summary>
    /// 🔴 <b>والحالة الشايعة استعلام واحد — ومن غير ما يلمس جدول
    /// الأسامي المستعارة.</b>
    ///
    /// <para>دي بتشتغل على <b>كل صف في كل دفعة</b>، فالرحلة الزيادة مش
    /// تفصيلة. ومقاسة من نص الـSQL نفسه.</para>
    /// </summary>
    [Fact]
    public async Task A_batch_of_live_devices_is_one_query_and_never_asks_aliases()
    {
        var sql = new List<string>();

        using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(fixture.ConnectionString)
                .LogTo(sql.Add, [DbLoggerCategory.Database.Command.Name], LogLevel.Information)
                .Options);

        var tenant = NewTenant(db);

        var ids = Enumerable.Range(0, 5).Select(_ => NewDevice(db, tenant).Id).ToList();

        await db.SaveChangesAsync();

        sql.Clear();

        var map = await Resolver(db).ResolveManyAsync(tenant, ids);

        Assert.Equal(5, map.Count);

        var selects = sql.Where(l => l.Contains("SELECT", StringComparison.Ordinal)).ToList();

        Assert.Single(selects);
        Assert.DoesNotContain(sql, l => l.Contains("[DeviceAliases]", StringComparison.Ordinal));
    }

    /// <summary>⚠️ والمعرّف الفاضي والمتكرر بيتشالوا قبل أي استعلام.</summary>
    [Fact]
    public async Task Empty_and_duplicate_ids_are_dropped_first()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant);

        await db.SaveChangesAsync();

        var map = await Resolver(db).ResolveManyAsync(
            tenant, [device.Id, device.Id, Guid.Empty, device.Id]);

        Assert.Single(map);

        Assert.Empty(await Resolver(db).ResolveManyAsync(tenant, [Guid.Empty]));
    }

    /// <summary>
    /// 🔴 <b>والمسارين — الواحد والجملة — بيدّوا نفس الإجابة.</b>
    /// نسختين بيختلفوا هو بالظبط العطل اللي الملف ده اتعمل عشانه.
    /// </summary>
    [Fact]
    public async Task One_and_many_agree_on_every_shape()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var alive = NewDevice(db, tenant);
        var mid = NewDevice(db, tenant);
        var end = NewDevice(db, tenant);

        await db.SaveChangesAsync();

        var local = Guid.NewGuid();
        Alias(db, tenant, local, mid.Id);
        Merge(mid, end.Id);

        await db.SaveChangesAsync();

        Guid[] probes = [alive.Id, local, mid.Id, Guid.NewGuid()];

        var many = await Resolver(db).ResolveManyAsync(tenant, probes);

        foreach (var id in probes)
        {
            var one = await Resolver(db).ResolveAsync(tenant, id);

            Assert.Equal(one, many.TryGetValue(id, out var m) ? m : null);
        }
    }
}
