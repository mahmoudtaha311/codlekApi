using Codlek.Core.Entities;
using Codlek.Core.Repairs;
using Codlek.Infrastructure.Data;

namespace Codlek.Tests;

/// <summary>
/// العدّاد الذرّي — <b>على قاعدة حقيقية، لأن السؤال هو السباق</b>.
///
/// <para>🔴 <b>الفحوص دي مش ينفع تتعمل بمستودع بديل.</b> اللي بيتفحص
/// هنا هو إن جملة <c>UPDATE … OUTPUT deleted.NextValue</c> بتحجز
/// الرقم فعلاً — ودي خاصية في SQL Server مش في الكود.</para>
/// </summary>
public class TenantCounterTests(CounterDbFixture fixture) : IClassFixture<CounterDbFixture>
{
    private static Guid NewTenant(AppDbContext db)
    {
        var tenant = new Tenant { Name = "ورشة " + Guid.NewGuid().ToString("N")[..6] };
        db.Tenants.Add(tenant);
        db.SaveChanges();
        return tenant.Id;
    }

    [Fact]
    public async Task The_first_number_is_one()
    {
        await using var db = fixture.Create();
        Guid tenant = NewTenant(db);

        int first = await new TenantCounters(db).NextAsync(tenant, "repair");

        Assert.Equal(1, first);
    }

    /// <summary>⚠️ والأرقام بتتابع من غير فراغ.</summary>
    [Fact]
    public async Task The_numbers_run_in_sequence()
    {
        await using var db = fixture.Create();
        Guid tenant = NewTenant(db);
        var counters = new TenantCounters(db);

        var taken = new List<int>();
        for (int i = 0; i < 5; i++) taken.Add(await counters.NextAsync(tenant, "repair"));

        Assert.Equal([1, 2, 3, 4, 5], taken);
    }

    /// <summary>
    /// 🔴 <b>الفحص الأساسي: محدش بياخد نفس الرقم.</b>
    ///
    /// <para>عشرين نداء على <b>سياقات مختلفة بالتوازي</b> — زي عشرين
    /// راكة بتسجّل في نفس اللحظة. ولو الحجز مش ذرّي، تلاقي أرقام
    /// مكرّرة.</para>
    ///
    /// <para>⚠️ والتكرار في الإنتاج بيبان كـ«الفهرس الفريد رفض
    /// التسجيل» — رسالة مالهاش أي معنى عند الفني.</para>
    /// </summary>
    [Fact]
    public async Task Twenty_concurrent_callers_never_share_a_number()
    {
        Guid tenant;
        await using (var seed = fixture.Create()) tenant = NewTenant(seed);

        var taken = await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
        {
            // ⚠️ سياق لكل نداء — مشاركة السياق بتسلسل الطلبات
            // فالفحص كان هيعدّي وهو مش بيقيس حاجة.
            await using var db = fixture.Create();
            return await new TenantCounters(db).NextAsync(tenant, "repair");
        }));

        Assert.Equal(20, taken.Distinct().Count());
        Assert.Equal(Enumerable.Range(1, 20), taken.OrderBy(x => x));
    }

    /// <summary>
    /// ⚠️ وعدّادين بأسماء مختلفة مستقلّين تماماً.
    ///
    /// <para>أكواد الأجهزة وأكواد أوامر الصيانة في نفس الجدول، وخلطهم
    /// بيخلّي فتح أمر صيانة يحرق كود جهاز.</para>
    /// </summary>
    [Fact]
    public async Task Two_counter_names_are_independent()
    {
        await using var db = fixture.Create();
        Guid tenant = NewTenant(db);
        var counters = new TenantCounters(db);

        Assert.Equal(1, await counters.NextAsync(tenant, "repair"));
        Assert.Equal(1, await counters.NextAsync(tenant, "device"));
        Assert.Equal(2, await counters.NextAsync(tenant, "repair"));
    }

    /// <summary>⚠️ وشركتين مستقلّتين — كل واحدة بترقّم من واحد.</summary>
    [Fact]
    public async Task Two_tenants_number_independently()
    {
        await using var db = fixture.Create();
        Guid a = NewTenant(db), b = NewTenant(db);
        var counters = new TenantCounters(db);

        Assert.Equal(1, await counters.NextAsync(a, "repair"));
        Assert.Equal(1, await counters.NextAsync(b, "repair"));
        Assert.Equal(2, await counters.NextAsync(a, "repair"));
    }

    /// <summary>
    /// 🔴 <b>النداء مابيلمسش متتبّع التغييرات.</b>
    ///
    /// <para>ودي الحاجة اللي كانت بتعمل <b>فقد بيانات صامت</b>: النسخة
    /// الأولى كانت <c>Add</c> + <c>SaveChanges</c> جوّه <c>catch</c>
    /// بينادي <c>ChangeTracker.Clear()</c> — فأي خطأ من أي كيان تاني
    /// في الدفعة كان بيتبلع على إنه «سباق عدّاد»، و<c>Clear()</c>
    /// بتفصل الدفعة كلها. السيرفر يرد «اتقبل» ومفيش ولا صف اتكتب.</para>
    ///
    /// <para>⚠️ والفحص بيحطّ كيان نص مبني في السياق قبل النداء،
    /// وبيتأكّد إنه <b>لسه موجود ولسه مش محفوظ</b> بعديه.</para>
    /// </summary>
    [Fact]
    public async Task Allocating_a_number_never_touches_the_change_tracker()
    {
        await using var db = fixture.Create();
        Guid tenant = NewTenant(db);

        // كيان نص مبني — زي أمر صيانة جوّه حلقة دفعة مزامنة
        var halfBuilt = new Tenant { Name = "لسه مش جاهزة" };
        db.Tenants.Add(halfBuilt);

        int before = db.ChangeTracker.Entries().Count();

        await new TenantCounters(db).NextAsync(tenant, "repair");

        // لسه متتبَّع، ولسه مش محفوظ
        Assert.Equal(before, db.ChangeTracker.Entries().Count());
        Assert.Equal(
            Microsoft.EntityFrameworkCore.EntityState.Added,
            db.Entry(halfBuilt).State);
    }

    /// <summary>
    /// 🔴 واسم عدّاد الصيانة هو اللي <c>RepairCode</c> بيقوله.
    ///
    /// <para>لو اتفرقوا، كل أمر جديد بياخد رقم من عدّاد تاني — والأكواد
    /// بتتكرر.</para>
    /// </summary>
    [Fact]
    public async Task The_repair_counter_uses_the_name_from_core()
    {
        await using var db = fixture.Create();
        Guid tenant = NewTenant(db);

        int number = await new TenantCounters(db).NextAsync(tenant, RepairCode.CounterName);

        Assert.Equal("RP-00000001", RepairCode.Format(number));
    }
}
