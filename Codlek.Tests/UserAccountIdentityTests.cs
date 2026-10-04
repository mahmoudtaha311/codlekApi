using Codlek.Core.Entities;
using Codlek.Core.Entities.Auth;
using Codlek.Core.Enums;
using Codlek.Core.Text;
using Codlek.Infrastructure.Auth;
using Codlek.Infrastructure.Data;
using Codlek.Infrastructure.Repositories;
using Microsoft.AspNetCore.Identity;

namespace Codlek.Tests;

/// <summary>
/// مفتاح الدخول والمستودع — على قاعدة حقيقية.
///
/// <para>🔴 <b>الفحوص دي موجودة بسبب باج كتبته وكان هيقع وقت
/// التشغيل.</b> كتبت استعلام فيه <c>LoginName.Normalize(u.UserName)</c>
/// — دالة C# جوّه <c>Where</c>. البناء عدّى عادي، وEF كانت هترمي أول
/// مرة النقطة تتنده: دالة C# ماتترجمش لـSQL.</para>
///
/// <para>⚠️ والإصلاح إن المقارنة بقت على عمود
/// <c>NormalizedUserName</c>، واللي بيحسبه هو
/// <see cref="LoginNameNormalizer"/>.</para>
/// </summary>
public class UserAccountIdentityTests(UserDbFixture fixture)
    : IClassFixture<UserDbFixture>
{
    private static Guid NewTenant(AppDbContext db)
    {
        var tenant = new Tenant { Name = "ورشة " + Guid.NewGuid().ToString("N")[..6] };
        db.Tenants.Add(tenant);
        return tenant.Id;
    }

    private static ApplicationUser NewUser(
        AppDbContext db, Guid tenantId, string username, UserRole role = UserRole.Technician)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserName = username,

            // ⚠️ بنحسبه بنفس القاعدة اللي المطبّع بيستعملها — زي ما
            // Identity بتعمل وقت `CreateAsync`.
            NormalizedUserName = LoginName.Normalize(username),
            DisplayName = "صاحب الحساب",
            Code = "F" + Guid.NewGuid().ToString("N")[..5],
            Role = role,
            IsActive = true,
        };
        db.Users.Add(user);
        return user;
    }

    // =================================================================
    //  المطبّع
    // =================================================================

    /// <summary>
    /// 🔴 <b>المطبّع بيدّي نفس مفتاح <c>LoginName</c> بالحرف.</b>
    ///
    /// <para>لو اختلفوا، فحص «الاسم ده متاخد؟» بيقارن مفتاح بمفتاح
    /// تاني فبيرجّع «فاضي» دايماً — والفهرس الفريد هو اللي بيرمي عند
    /// الحفظ. والأسوأ: الراكة بتوصل لمفتاح والموقع لمفتاح تاني، فاسم
    /// يدخل على الراكة ومايدخلش على الموقع.</para>
    /// </summary>
    [Theory]
    [InlineData("Kareem")]
    [InlineData("  KAREEM  ")]
    [InlineData("kareem")]
    public void The_normalizer_matches_the_projects_login_rule(string typed) =>
        Assert.Equal(
            LoginName.Normalize(typed),
            new LoginNameNormalizer().NormalizeName(typed));

    /// <summary>
    /// ⚠️ <b>والخرج بحروف صغيرة — مش كابيتال زي Identity الافتراضية.</b>
    ///
    /// <para>ده اللي البرنامج المكتبي على الراكة بيعمله.</para>
    /// </summary>
    [Fact]
    public void The_normalizer_lowercases_unlike_the_identity_default()
    {
        Assert.Equal("kareem", new LoginNameNormalizer().NormalizeName("KAREEM"));
        Assert.NotEqual("KAREEM", new LoginNameNormalizer().NormalizeName("KAREEM"));
    }

    /// <summary>
    /// ⚠️ <b>والبريد بيفضل كابيتال.</b>
    ///
    /// <para>قاعدة <c>LoginName</c> بتقص على ٦٠ حرف — صح لاسم مستخدم
    /// وغلط لبريد.</para>
    /// </summary>
    [Fact]
    public void The_normalizer_leaves_email_on_the_default_rule() =>
        Assert.Equal("A@B.COM", new LoginNameNormalizer().NormalizeEmail("  a@b.com  "));

    /// <summary>
    /// ⚠️ والاسم الأطول من العمود بيتقص — <b>قبل ما يوصل القاعدة</b>.
    /// </summary>
    [Fact]
    public void A_very_long_name_is_truncated_to_the_column_width() =>
        Assert.Equal(
            LoginName.MaxLength,
            new LoginNameNormalizer().NormalizeName(new string('a', 200))!.Length);

    // =================================================================
    //  المستودع — الاستعلام اللي كان هيقع
    // =================================================================

    /// <summary>
    /// 🔴 <b>الاستعلام ده هو اللي كان مكتوب غلط.</b>
    ///
    /// <para>الفحص ده بيشغّله على SQL حقيقي — فلو حد رجّعه لدالة C#،
    /// EF بترمي والفحص بيقع.</para>
    /// </summary>
    [Fact]
    public async Task The_username_check_runs_as_sql()
    {
        await using var db = fixture.Create();
        Guid tenant = NewTenant(db);
        NewUser(db, tenant, "Kareem");
        await db.SaveChangesAsync();

        var repository = new UserAccountRepository(db);

        Assert.True(await repository.UsernameTakenAnywhereAsync(LoginName.Normalize("kareem")));
        Assert.True(await repository.UsernameTakenAnywhereAsync(LoginName.Normalize("KAREEM")));
        Assert.False(await repository.UsernameTakenAnywhereAsync(LoginName.Normalize("someone")));
    }

    /// <summary>
    /// 🔴 <b>الفحص على مستوى النظام كله، مش جوّه الشركة.</b>
    ///
    /// <para>مفتاح الدخول عالمي: صفحة الدخول بتستلم اسم وباسورد وبس،
    /// فالاسم لازم يوصل لصف واحد مهما كان عدد الشركات.</para>
    /// </summary>
    [Fact]
    public async Task A_username_taken_in_another_tenant_is_still_taken()
    {
        await using var db = fixture.Create();
        Guid mine = NewTenant(db), theirs = NewTenant(db);
        string name = "shared" + Guid.NewGuid().ToString("N")[..6];
        NewUser(db, theirs, name);
        await db.SaveChangesAsync();

        var repository = new UserAccountRepository(db);

        Assert.True(await repository.UsernameTakenAnywhereAsync(LoginName.Normalize(name)));
    }

    /// <summary>
    /// ⚠️ بس <b>كود الحساب</b> فريد جوّه الشركة بس.
    ///
    /// <para>الكود بيربط الحساب بالفحوصات، والفحوصات مرشّحة بالشركة —
    /// فنفس الكود في شركة تانية مالوش أي تأثير.</para>
    /// </summary>
    [Fact]
    public async Task A_code_taken_in_another_tenant_is_free_here()
    {
        await using var db = fixture.Create();
        Guid mine = NewTenant(db), theirs = NewTenant(db);
        var theirUser = NewUser(db, theirs, "u" + Guid.NewGuid().ToString("N")[..8]);
        await db.SaveChangesAsync();

        var repository = new UserAccountRepository(db);

        Assert.True(await repository.CodeTakenAsync(theirs, theirUser.Code));
        Assert.False(await repository.CodeTakenAsync(mine, theirUser.Code));
    }

    /// <summary>
    /// 🔴 <b>صف شركة تانية مابيترجعش — حتى بنفس المعرّف.</b>
    /// </summary>
    [Fact]
    public async Task A_user_from_another_tenant_is_not_found()
    {
        await using var db = fixture.Create();
        Guid mine = NewTenant(db), theirs = NewTenant(db);
        var theirUser = NewUser(db, theirs, "v" + Guid.NewGuid().ToString("N")[..8]);
        await db.SaveChangesAsync();

        Assert.Null(await new UserAccountRepository(db).FindAsync(mine, theirUser.Id));
    }

    /// <summary>
    /// ⚠️ والفنيين تحت في الترتيب — <b>في SQL</b>.
    /// </summary>
    [Fact]
    public async Task Technicians_sort_last_in_sql()
    {
        await using var db = fixture.Create();
        Guid tenant = NewTenant(db);
        NewUser(db, tenant, "a" + Guid.NewGuid().ToString("N")[..8], UserRole.Technician);
        NewUser(db, tenant, "z" + Guid.NewGuid().ToString("N")[..8], UserRole.Manager);
        await db.SaveChangesAsync();

        var rows = await new UserAccountRepository(db).ListAsync(tenant);

        Assert.Equal(UserRole.Manager, rows[0].Role);
        Assert.Equal(UserRole.Technician, rows[1].Role);
    }

    /// <summary>
    /// 🔴 <b>والصف راجع متتبَّع — عشان الإيقاف يتحفظ.</b>
    ///
    /// <para>لو المستودع رجّعه بـ<c>AsNoTracking</c>، الإيقاف كان
    /// بيعدّي ويرجّع ٢٠٠ و<b>مايتحفظش</b> — من غير أي خطأ.</para>
    /// </summary>
    [Fact]
    public async Task The_returned_row_is_tracked_so_suspension_persists()
    {
        await using var db = fixture.Create();
        Guid tenant = NewTenant(db);
        var user = NewUser(db, tenant, "w" + Guid.NewGuid().ToString("N")[..8]);
        await db.SaveChangesAsync();

        var found = await new UserAccountRepository(db).FindAsync(tenant, user.Id);
        found!.IsActive = false;
        found.SuspendedReason = "اتفصل";
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();
        var reread = await new UserAccountRepository(db).FindAsync(tenant, user.Id);

        Assert.False(reread!.IsActive);
        Assert.Equal("اتفصل", reread.SuspendedReason);
    }

    /// <summary>
    /// 🔴 <b>والفهرس الفريد على الاسم المطبَّع بيرمي فعلاً.</b>
    ///
    /// <para>فحص «الاسم متاخد؟» مش بديل للفهرس — هو عشان رسالة
    /// مفهومة. والفهرس هو اللي بيمنع السباق بين طلبين في نفس
    /// اللحظة.</para>
    /// </summary>
    [Fact]
    public async Task The_unique_index_rejects_a_duplicate_normalised_name()
    {
        await using var db = fixture.Create();
        Guid tenant = NewTenant(db);
        string name = "dup" + Guid.NewGuid().ToString("N")[..6];
        NewUser(db, tenant, name);
        await db.SaveChangesAsync();

        // نفس الاسم بحالة حروف مختلفة → نفس المفتاح المطبَّع
        NewUser(db, tenant, name.ToUpperInvariant());

        await Assert.ThrowsAnyAsync<Exception>(() => db.SaveChangesAsync());
    }
}
