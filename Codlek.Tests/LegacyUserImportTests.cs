using Codlek.Application;
using Codlek.Application.Features.Auth.Login;
using Codlek.Core.Entities;
using Codlek.Core.Entities.Auth;
using Codlek.Core.Enums;
using Codlek.Infrastructure;
using Codlek.Infrastructure.Auth;
using Codlek.Infrastructure.Data;
using Codlek.Infrastructure.Migrations;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Codlek.Tests;

/// <summary>قاعدة فحوص نقل الحسابات.</summary>
public sealed class LegacyUserImportDbFixture : SqlServerDbFixture
{
    protected override string DatabaseName => "codlek_user_import_test";

    /// <summary>
    /// الحاوية الحقيقية للمشروع — <b>نفس التسجيلات بالحرف</b>، على
    /// القاعدة دي. عشان الدخول بعد النقل يتجرّب من نفس الطريق اللي
    /// المستخدم هيمشي فيه: المطبِّع المخصّص، والمشفّر القديم، والمعالج.
    /// </summary>
    public ServiceProvider BuildServices()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SqlServer"] = ConnectionString,
                ["Jwt:Key"] = "test-only-signing-key-0123456789-abcdefghijkl",
                ["Jwt:Issuer"] = "codlek",
                ["Jwt:Audience"] = "codlek",
            })
            .Build();

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddApplicationServices();
        services.AddInfrastructureServices(configuration, isDevelopment: true);

        return services.BuildServiceProvider();
    }
}

/// <summary>
/// نقل حسابات اللوحة من <c>Users</c> لـ<c>AspNetUsers</c> — <b>على
/// قاعدة حقيقية، ومن نفس طريق الدخول</b>.
///
/// <para>🔴 <b>الفحص المهم هنا مش «الصف اتنسخ».</b> المهم إن المستخدم
/// <b>يدخل بباسورده القديم</b> بعد النقل — والطريق ده فيه مطبِّع أسماء
/// مخصّص ومشفّر قديم، وأي تفصيلة فيهم مختلفة بتقفل الحسابات كلها في
/// يوم التحويل.</para>
/// </summary>
public class LegacyUserImportTests(LegacyUserImportDbFixture fixture)
    : IClassFixture<LegacyUserImportDbFixture>
{
    private const string Password = "OldPass1";

    private static string Unique(string prefix) =>
        prefix + "." + Guid.NewGuid().ToString("N")[..8];

    private async Task<WebUser> SeedLegacyAsync(
        string username, string password = Password, Action<WebUser>? tweak = null)
    {
        using var db = fixture.Create();

        var tenant = new Tenant { Name = "ورشة " + Guid.NewGuid().ToString("N")[..6] };
        db.Tenants.Add(tenant);

        var (hash, salt) = PasswordHasher.Create(password);

        var user = new WebUser
        {
            TenantId = tenant.Id,
            Username = username,
            NormalizedUsername = Core.Text.LoginName.Normalize(username),
            DisplayName = "مستخدم قديم",
            Role = UserRole.Manager,
            Code = Random.Shared.Next(100000, 999999).ToString(),
            PasswordHash = hash,
            Salt = salt,
            CredentialVersion = 3,
            CreatedAtUtc = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            LastLoginUtc = new DateTime(2026, 9, 30, 8, 0, 0, DateTimeKind.Utc),
        };

        tweak?.Invoke(user);

        db.WebUsers.Add(user);
        await db.SaveChangesAsync();

        return user;
    }

    private async Task ImportAsync()
    {
        using var db = fixture.Create();
        await db.Database.ExecuteSqlRawAsync(ImportLegacyUsers.Sql);
    }

    private async Task<ApplicationUser?> IdentityRowAsync(Guid id)
    {
        using var db = fixture.Create();
        return await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id);
    }

    private async Task<bool> LogsInAsync(string username, string password)
    {
        using var services = fixture.BuildServices();
        using var scope = services.CreateScope();

        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var result = await sender.Send(new LoginCommand(username, password));

        return result.IsSuccess;
    }

    // =================================================================
    //  النقل نفسه
    // =================================================================

    /// <summary>
    /// 🔴 <b>نفس المعرّف، ونفس الشركة والدور والكود ونسخة الاعتماد.</b>
    /// المعرّف بالذات: السجل وأوامر الصيانة بتشاور عليه.
    /// </summary>
    [Fact]
    public async Task An_account_arrives_with_the_same_id_and_fields()
    {
        var legacy = await SeedLegacyAsync(Unique("manager"));

        await ImportAsync();

        var row = await IdentityRowAsync(legacy.Id);

        Assert.NotNull(row);
        Assert.Equal(legacy.TenantId, row!.TenantId);
        Assert.Equal(legacy.Username, row.UserName);
        Assert.Equal(legacy.NormalizedUsername, row.NormalizedUserName);
        Assert.Equal(legacy.DisplayName, row.DisplayName);
        Assert.Equal(legacy.Code, row.Code);
        Assert.Equal(UserRole.Manager, row.Role);
        Assert.Equal(3, row.CredentialVersion);
        Assert.True(row.IsActive);
        Assert.False(row.MustChangePassword);
        Assert.Equal(legacy.CreatedAtUtc, row.CreatedAtUtc);
        Assert.Equal(legacy.LastLoginUtc, row.LastLoginUtc);

        // ⚠️ البصمة والملح زي ما هم — الهجرة مابتحسبش حاجة.
        Assert.Equal(legacy.PasswordHash, row.PasswordHash);
        Assert.Equal(legacy.Salt, row.LegacySalt);

        // ⚠️ وIdentity محتاجة ختم أمان مش فاضي.
        Assert.False(string.IsNullOrWhiteSpace(row.SecurityStamp));
        Assert.False(string.IsNullOrWhiteSpace(row.ConcurrencyStamp));
    }

    /// <summary>
    /// 🔴 <b>المستخدم بيدخل بباسورده القديم — من نفس طريق الدخول.</b>
    /// وبالاسم بأي حالة حروف ومسافات: المطبِّع المخصّص لازم يوصل لنفس
    /// العمود اللي اتنسخ.
    /// </summary>
    [Fact]
    public async Task The_old_password_logs_in_after_the_import()
    {
        string name = Unique("owner");

        await SeedLegacyAsync(name);
        await ImportAsync();

        Assert.True(await LogsInAsync(name, Password));
        Assert.True(await LogsInAsync("  " + name.ToUpperInvariant() + " ", Password));
        Assert.False(await LogsInAsync(name, "WrongPass1"));
    }

    /// <summary>
    /// ⚠️ <b>وأول دخول بيرقّي الباسورد</b> — الملح القديم بيتفضّى
    /// والبصمة بتتكتب بالشكل الجديد، والدخول التاني لسه شغّال.
    /// </summary>
    [Fact]
    public async Task The_first_login_upgrades_the_password_and_the_next_still_works()
    {
        string name = Unique("floor");

        var legacy = await SeedLegacyAsync(name);
        await ImportAsync();

        Assert.True(await LogsInAsync(name, Password));

        var upgraded = await IdentityRowAsync(legacy.Id);

        Assert.Equal("", upgraded!.LegacySalt);
        Assert.NotEqual(legacy.PasswordHash, upgraded.PasswordHash);

        Assert.True(await LogsInAsync(name, Password));
    }

    /// <summary>⚠️ الحساب الموقوف بيوصل موقوف — والدخول بيترفض.</summary>
    [Fact]
    public async Task A_suspended_account_stays_suspended()
    {
        string name = Unique("accountant");

        var legacy = await SeedLegacyAsync(name, tweak: u =>
        {
            u.IsActive = false;
            u.SuspendedReason = "سايب الشغل";
            u.SuspendedByName = "المالك";
            u.SuspendedAtUtc = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        });

        await ImportAsync();

        var row = await IdentityRowAsync(legacy.Id);

        Assert.False(row!.IsActive);
        Assert.Equal("سايب الشغل", row.SuspendedReason);
        Assert.Equal("المالك", row.SuspendedByName);
        Assert.Equal(legacy.SuspendedAtUtc, row.SuspendedAtUtc);

        Assert.False(await LogsInAsync(name, Password));
    }

    // =================================================================
    //  الأمان: مابيدهسش
    // =================================================================

    /// <summary>
    /// 🔴 <b>تشغيله مرتين مابيغيّرش حاجة.</b> لا صف زيادة ولا ختم جديد.
    /// </summary>
    [Fact]
    public async Task Running_it_twice_changes_nothing()
    {
        var legacy = await SeedLegacyAsync(Unique("twice"));

        await ImportAsync();

        var first = await IdentityRowAsync(legacy.Id);

        await ImportAsync();

        var second = await IdentityRowAsync(legacy.Id);

        Assert.Equal(first!.SecurityStamp, second!.SecurityStamp);
        Assert.Equal(first.ConcurrencyStamp, second.ConcurrencyStamp);

        using var db = fixture.Create();

        Assert.Equal(1, await db.Users.CountAsync(u => u.Id == legacy.Id));
    }

    /// <summary>
    /// 🔴 <b>حساب موجود في الجديد بنفس المعرّف مابيتلمسش</b> — حتى لو
    /// القديم فيه باسورد تاني. ده المستخدم اللي دخل بعد النقل وغيّر
    /// باسورده؛ إعادة التشغيل كانت هترجّعه للقديم.
    /// </summary>
    [Fact]
    public async Task An_account_already_in_identity_is_left_alone()
    {
        string name = Unique("kept");

        var legacy = await SeedLegacyAsync(name);
        await ImportAsync();

        using (var db = fixture.Create())
        {
            var row = await db.Users.SingleAsync(u => u.Id == legacy.Id);
            row.DisplayName = "اتغيّر في الجديد";
            await db.SaveChangesAsync();
        }

        await ImportAsync();

        Assert.Equal("اتغيّر في الجديد", (await IdentityRowAsync(legacy.Id))!.DisplayName);
    }

    /// <summary>
    /// 🔴 <b>حساب اتغيّر اسمه في الجديد بعد النقل مابيتنقلش تاني.</b>
    /// فحص الاسم لوحده مش كفاية هنا: الاسم القديم مابقاش موجود، فلو
    /// مفيش فحص بالمعرّف الجملة بتحاول تضيف نفس المعرّف — وتقع على
    /// المفتاح الأساسي والهجرة كلها بتفشل.
    /// </summary>
    [Fact]
    public async Task An_account_renamed_after_the_import_is_not_imported_again()
    {
        var legacy = await SeedLegacyAsync(Unique("renamed"));

        await ImportAsync();

        string newName = Unique("new.name");

        using (var db = fixture.Create())
        {
            var row = await db.Users.SingleAsync(u => u.Id == legacy.Id);
            row.UserName = newName;
            row.NormalizedUserName = Core.Text.LoginName.Normalize(newName);
            await db.SaveChangesAsync();
        }

        await ImportAsync();

        using var after = fixture.Create();

        var only = await after.Users.SingleAsync(u => u.Id == legacy.Id);

        Assert.Equal(newName, only.UserName);
    }

    /// <summary>
    /// ⚠️ <b>اسم محجوز في الجديد لحساب تاني = الصف القديم بيتساب.</b>
    /// مابيوقّعش الهجرة كلها، ومابيدهسش الحساب الموجود.
    /// </summary>
    [Fact]
    public async Task A_name_already_taken_in_identity_is_skipped()
    {
        string name = Unique("taken");

        Guid existingId;

        using (var db = fixture.Create())
        {
            var tenant = new Tenant { Name = "ورشة الجديد" };
            db.Tenants.Add(tenant);

            var existing = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                UserName = name,
                NormalizedUserName = Core.Text.LoginName.Normalize(name),
                DisplayName = "حساب جديد",
                SecurityStamp = Guid.NewGuid().ToString("N"),
            };

            db.Users.Add(existing);
            await db.SaveChangesAsync();

            existingId = existing.Id;
        }

        var legacy = await SeedLegacyAsync(name);

        await ImportAsync();

        Assert.Null(await IdentityRowAsync(legacy.Id));
        Assert.Equal("حساب جديد", (await IdentityRowAsync(existingId))!.DisplayName);
    }
}
