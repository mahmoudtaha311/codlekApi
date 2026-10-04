using Codlek.Core.Entities;
using Codlek.Infrastructure.Data;
using Codlek.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Codlek.Tests;

/// <summary>
/// عدّاد محاولات الدخول — <b>على قاعدة حقيقية</b>.
///
/// <para>🔴 <b>والملف ده موجود لسبب مقاس.</b> العدّاد ده هو الفرملة
/// الوحيدة على تخمين باسورد فني <b>بالاسم</b> — الحد على مستوى HTTP
/// بيقسّم بالمحطة بس. وكل شروطه الأربعة في استعلام واحد، والمزيّف
/// بينفّذها بنفسه — يعني شيل شرط من الاستعلام الحقيقي بيعدّي من تحت
/// كل فحوص الوحدة.</para>
/// </summary>
public class TechnicianLoginRepositoryTests(TechnicianLoginDbFixture fixture)
    : IClassFixture<TechnicianLoginDbFixture>
{
    private static Guid NewTenant(AppDbContext db)
    {
        var tenant = new Tenant { Name = "ورشة " + Guid.NewGuid().ToString("N")[..6] };
        db.Tenants.Add(tenant);
        return tenant.Id;
    }

    private static Rack NewRack(AppDbContext db, Guid tenantId, string code)
    {
        var rack = new Rack { TenantId = tenantId, RackCode = code, Name = "محطة " + code };
        db.Racks.Add(rack);
        return rack;
    }

    private static Technician NewTech(
        AppDbContext db, Guid tenantId, string username, string code = "100200")
    {
        var tech = new Technician
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = "فني " + username,
            Username = username,
            NormalizedUsername = username.ToLowerInvariant(),
            PasswordHash = "hash",
            Salt = "salt",
        };

        db.Technicians.Add(tech);
        return tech;
    }

    private static void Attempt(
        AppDbContext db, Guid tenantId, Guid rackId, string username,
        bool success, DateTime atUtc)
    {
        db.TechnicianLoginAttempts.Add(new TechnicianLoginAttempt
        {
            TenantId = tenantId,
            RackId = rackId,
            AttemptedUsername = username,
            Success = success,
            AtUtc = atUtc,
        });
    }

    // =================================================================
    //  العدّاد — أربع شروط، كل واحد بيتقاس لوحده
    // =================================================================

    /// <summary>
    /// 🔴 <b>أربع شروط، وشيل أي واحد منهم بيكسر حاجة مختلفة:</b>
    ///
    /// <list type="bullet">
    /// <item>المحطة — من غيرها، فني مقفول على بنش بيتقفل على الورشة
    /// كلها</item>
    /// <item>الاسم — من غيره، عشر محاولات على أي اسم بتقفل كل
    /// الفنيين</item>
    /// <item><c>!Success</c> — من غيره، الفني اللي بيدخل كتير بيتقفل
    /// على نفسه</item>
    /// <item>النافذة — من غيرها، القفل بيبقى <b>أبدي</b> ومفيش طريق
    /// يفتحه</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task The_counter_sees_only_recent_failures_for_this_rack_and_this_name()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var mine = NewRack(db, tenant, "RACK-C01");
        var other = NewRack(db, tenant, "RACK-C02");

        await db.SaveChangesAsync();

        var now = DateTime.UtcNow;
        var since = now.AddMinutes(-5);

        // ✅ الصف الوحيد اللي المفروض يتعدّ.
        Attempt(db, tenant, mine.Id, "counted", false, now.AddMinutes(-1));

        // ❌ محطة تانية.
        Attempt(db, tenant, other.Id, "counted", false, now.AddMinutes(-1));

        // ❌ اسم تاني.
        Attempt(db, tenant, mine.Id, "someone", false, now.AddMinutes(-1));

        // ❌ محاولة ناجحة.
        Attempt(db, tenant, mine.Id, "counted", true, now.AddMinutes(-1));

        // ❌ خارج النافذة.
        Attempt(db, tenant, mine.Id, "counted", false, now.AddMinutes(-30));

        await db.SaveChangesAsync();

        int recent = await new TechnicianLoginRepository(db)
            .RecentFailuresAsync(mine.Id, "counted", since);

        Assert.Equal(1, recent);
    }

    /// <summary>
    /// ⚠️ <b>وحدّ النافذة <c>&gt;=</c> — الصف اللي على الحد بالظبط
    /// بيتعدّ.</b>
    /// </summary>
    [Fact]
    public async Task A_failure_exactly_on_the_window_edge_counts()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var rack = NewRack(db, tenant, "RACK-C03");

        await db.SaveChangesAsync();

        // ⚠️ القاعدة بتخزّن `datetime2` بدقة ١٠٠ نانوثانية، فالوقت
        //    بيتقرا زي ما اتكتب — والحد بيتقاس بالظبط.
        var edge = new DateTime(2026, 10, 4, 9, 25, 0, DateTimeKind.Utc);

        Attempt(db, tenant, rack.Id, "edge", false, edge);

        await db.SaveChangesAsync();

        var repo = new TechnicianLoginRepository(db);

        Assert.Equal(1, await repo.RecentFailuresAsync(rack.Id, "edge", edge));

        Assert.Equal(
            0, await repo.RecentFailuresAsync(rack.Id, "edge", edge.AddTicks(1)));
    }

    // =================================================================
    //  البحث بالاسم
    // =================================================================

    /// <summary>
    /// 🔴 <b>والبحث مربوط بشركة المحطة — وده الحاجز نفسه.</b> نفس
    /// اسم الدخول موجود في ورشتين، ولو الفلتر اتشال، مفتاح محطة
    /// واحد كان بيفتح فني ورشة تانية.
    /// </summary>
    [Fact]
    public async Task The_same_login_name_in_two_workshops_stays_two_people()
    {
        using var db = fixture.Create();
        var mine = NewTenant(db);
        var theirs = NewTenant(db);

        var ours = NewTech(db, mine, "shared", code: "111111");
        NewTech(db, theirs, "shared", code: "222222");

        await db.SaveChangesAsync();

        var found = await new TechnicianLoginRepository(db)
            .FindByLoginKeyAsync(mine, "shared");

        Assert.NotNull(found);
        Assert.Equal(ours.Id, found!.Id);
        Assert.Equal("111111", found.Code);
    }

    /// <summary>
    /// 🔴 <b>والصف بيرجع <u>متتبّع</u> — والتتبّع ده حمّال.</b>
    ///
    /// <para>الدخول الناجح بيكتب <c>LastSuccessfulLoginUtc</c>،
    /// وتغيير الباسورد بيكتب البصمة والنسخة — وكلهم بيتحفظوا من وحدة
    /// العمل. حد يضيف <c>AsNoTracking()</c> هنا: تغيير الباسورد
    /// بيرجّع <c>200</c> و<b>مابيتحفظش</b>، والفني بيلاقي باسورده
    /// القديم لسه شغّال. وكل فحوص الوحدة بتعدّي لأن المزيّف بيشارك
    /// نفس الكائن.</para>
    /// </summary>
    [Fact]
    public async Task A_found_technician_is_tracked_so_a_change_actually_saves()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        NewTech(db, tenant, "tracked", code: "333333");

        await db.SaveChangesAsync();

        var found = await new TechnicianLoginRepository(db)
            .FindByLoginKeyAsync(tenant, "tracked");

        found!.CredentialVersion = 9;
        found.MustChangePassword = false;

        await db.SaveChangesAsync();

        // ⚠️ سياق جديد — عشان القراية تيجي من القاعدة مش من التتبّع.
        using var fresh = fixture.Create();

        Assert.Equal(
            9,
            (await fresh.Technicians.SingleAsync(t => t.TenantId == tenant))
                .CredentialVersion);
    }

    /// <summary>
    /// ⚠️ <b>والصف بيتضاف من غير حفظ</b> — الحفظ من وحدة العمل، عشان
    /// الصف وتاريخ الدخول ينزلوا مرة واحدة.
    /// </summary>
    [Fact]
    public async Task Adding_an_attempt_waits_for_the_unit_of_work()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var rack = NewRack(db, tenant, "RACK-C04");

        await db.SaveChangesAsync();

        new TechnicianLoginRepository(db).AddAttempt(new TechnicianLoginAttempt
        {
            TenantId = tenant,
            RackId = rack.Id,
            AttemptedUsername = "pending",
            Success = false,
            AtUtc = DateTime.UtcNow,
        });

        using (var before = fixture.Create())
            Assert.False(await before.TechnicianLoginAttempts
                .AnyAsync(a => a.AttemptedUsername == "pending"));

        await db.SaveChangesAsync();

        using var after = fixture.Create();

        Assert.True(await after.TechnicianLoginAttempts
            .AnyAsync(a => a.AttemptedUsername == "pending"));
    }
}
