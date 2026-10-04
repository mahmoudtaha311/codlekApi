using Codlek.Core.Entities.Auth;
using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Codlek.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Codlek.Tests;

/// <summary>
/// المستودع نفسه — على قاعدة حقيقية.
///
/// <para>🔴 <b>الفحوص دي موجودة بسبب فجوة اتمسكت فعلاً.</b> شلنا شرط
/// الشركة من <see cref="AccountRepository"/> بالعمد، و<b>كل الـ١١٦
/// فحص عدّوا</b> — لأن فحوص القطاع بتستعمل مستودع بديل، فهي بتثبت إن
/// المعالج صح مش إن المستودع صح.</para>
///
/// <para>⚠️ والقاعدة اللي بتتفحص هنا هي أهم قاعدة في النظام: <b>مفيش
/// صف بيتقرا من غير ترشيح بالشركة</b>. والمستودع هو المكان الوحيد
/// اللي الترشيح ده مكتوب فيه.</para>
/// </summary>
public class AccountRepositoryTests(AccountDbFixture fixture)
    : IClassFixture<AccountDbFixture>
{
    private static async Task<(Guid TenantId, Guid UserId)> SeedAsync(
        Infrastructure.Data.AppDbContext db, string tenantName, string username)
    {
        var tenant = new Tenant { Name = tenantName };
        db.Tenants.Add(tenant);

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            UserName = username,
            NormalizedUserName = username.ToUpperInvariant(),
            DisplayName = "صاحب الحساب",
            Code = "X001",
            Role = UserRole.Manager,
            CredentialVersion = 1,
            IsActive = true,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        return (tenant.Id, user.Id);
    }

    [Fact]
    public async Task My_own_row_is_found()
    {
        await using var db = fixture.Create();
        var repository = new AccountRepository(db);
        var (tenant, user) = await SeedAsync(db, "ورشة أ", "u" + Guid.NewGuid().ToString("N")[..10]);

        var found = await repository.FindAsync(tenant, user);

        Assert.NotNull(found);
        Assert.Equal(user, found.Id);
    }

    /// <summary>
    /// 🔴 <b>الفحص اللي كان ناقص.</b>
    ///
    /// <para>معرّف مستخدم صح + شركة غلط = <c>null</c>. من غير الشرط
    /// ده، مدير شركة كان يقدر يقرا حساب من شركة تانية لو عرف معرّفه —
    /// أو، الحالة الأرجح، حساب اتنقل لشركة تانية بعد ما التوكن اتعمل
    /// فالقراية بتفتح بيانات شركة التوكن مش شركة الصف.</para>
    /// </summary>
    [Fact]
    public async Task A_row_from_another_tenant_is_never_returned()
    {
        await using var db = fixture.Create();
        var repository = new AccountRepository(db);

        var (tenantA, _) = await SeedAsync(db, "ورشة أ", "a" + Guid.NewGuid().ToString("N")[..10]);
        var (_, userB) = await SeedAsync(db, "ورشة ب", "b" + Guid.NewGuid().ToString("N")[..10]);

        var found = await repository.FindAsync(tenantA, userB);

        Assert.Null(found);
    }

    /// <summary>⚠️ ومعرّف مش موجود خالص بيرجّع <c>null</c> مش بيرمي.</summary>
    [Fact]
    public async Task An_unknown_id_returns_null()
    {
        await using var db = fixture.Create();
        var repository = new AccountRepository(db);
        var (tenant, _) = await SeedAsync(db, "ورشة ج", "c" + Guid.NewGuid().ToString("N")[..10]);

        Assert.Null(await repository.FindAsync(tenant, Guid.NewGuid()));
    }

    /// <summary>
    /// 🔴 <b>الصف راجع <i>متتبَّع</i> — عشان تعديل الاسم يتحفظ.</b>
    ///
    /// <para>لو المستودع رجّعه بـ<c>AsNoTracking</c>، تعديل الاسم كان
    /// بيعدّي ويرجّع ٢٠٠ و<b>مايتحفظش</b> — من غير أي خطأ. والمستخدم
    /// بيشوف الاسم الجديد في الرد، ويلاقيه رجع القديم أول ما يعمل
    /// refresh.</para>
    /// </summary>
    [Fact]
    public async Task The_returned_row_is_tracked_so_edits_persist()
    {
        await using var db = fixture.Create();
        var repository = new AccountRepository(db);
        var (tenant, userId) = await SeedAsync(
            db, "ورشة د", "d" + Guid.NewGuid().ToString("N")[..10]);

        var user = await repository.FindAsync(tenant, userId);
        user!.DisplayName = "اسم اتغيّر";
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();
        var reread = await db.Users.SingleAsync(u => u.Id == userId);
        Assert.Equal("اسم اتغيّر", reread.DisplayName);
    }

    [Fact]
    public async Task The_tenant_name_is_read_from_the_tenant_row()
    {
        await using var db = fixture.Create();
        var repository = new AccountRepository(db);
        var (tenant, _) = await SeedAsync(
            db, "ورشة الاسم", "e" + Guid.NewGuid().ToString("N")[..10]);

        Assert.Equal("ورشة الاسم", await repository.TenantNameAsync(tenant));
    }

    /// <summary>
    /// ⚠️ وشركة مش موجودة بترجّع نص فاضي — <b>مش بترمي</b>.
    ///
    /// <para>لأن الاسم بيتعرض في شريط وبس. صفحة خطأ بسبب اسم شركة
    /// ناقص مش رد متناسب.</para>
    /// </summary>
    [Fact]
    public async Task An_unknown_tenant_name_is_empty_not_an_error()
    {
        await using var db = fixture.Create();
        var repository = new AccountRepository(db);

        Assert.Equal("", await repository.TenantNameAsync(Guid.NewGuid()));
    }
}
