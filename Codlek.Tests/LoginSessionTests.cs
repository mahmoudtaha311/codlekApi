using System.IdentityModel.Tokens.Jwt;
using Codlek.Application.Contracts.Auth;
using Codlek.Application.Interfaces;
using Codlek.Core.Entities;
using Codlek.Core.Entities.Auth;
using Codlek.Core.Enums;
using Codlek.Infrastructure.Auth;
using Codlek.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Codlek.Tests;

/// <summary>
/// الدورة الكاملة على قاعدة حقيقية.
///
/// <para>🔴 <b>الفحوص دي بتثبت حاجة الفحوص النقية مش بتثبتها: إن
/// الكود بيتنادى فعلاً.</b> حصل في المشروع ده إن قاعدة اتكتبت
/// واتجرّبت ومحدش بينديها — الفحص النقي كان أخضر، والميزة مش
/// شغّالة.</para>
///
/// <para>⚠️ وعشان كده بتلمس القاعدة: السؤال هنا مش «القاعدة صح؟»
/// هو «الصف اتكتب؟ والقديم اتلغى؟».</para>
/// </summary>
public class LoginSessionTests : IClassFixture<LoginSessionDbFixture>
{
    private readonly LoginSessionDbFixture _fixture;

    public LoginSessionTests(LoginSessionDbFixture fixture) => _fixture = fixture;

    private static readonly JwtOptions Settings = new()
    {
        Key = "فحوص-الجلسات-مفتاح-طوله-كفاية-عشان-التحقق-يعدّي",
        Issuer = "codlek",
        Audience = "codlek",
        AccessTokenMinutes = 15,
        RefreshTokenDays = 7,
    };

    private static JwtTokenIssuer NewIssuer() => new(Options.Create(Settings));

    private (LoginSessions Sessions, AppDbContext Db) Build()
    {
        var db = _fixture.Create();
        return (new LoginSessions(db, NewIssuer(), NullLogger<LoginSessions>.Instance), db);
    }

    /// <summary>بيحطّ مستخدم حقيقي في القاعدة ويرجّع موضوع التوكن.</summary>
    private static async Task<(Guid UserId, TokenSubject Subject)> SeedUserAsync(AppDbContext db)
    {
        var tenant = new Tenant { Name = "ورشة الفحص" };
        db.Tenants.Add(tenant);

        string username = "u" + Guid.NewGuid().ToString("N")[..12];
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            UserName = username,
            NormalizedUserName = username.ToUpperInvariant(),
            DisplayName = "أحمد الفني",
            Code = "F001",
            Role = UserRole.Technician,
            CredentialVersion = 1,
            IsActive = true,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        return (user.Id, new TokenSubject(
            user.Id, tenant.Id, username, user.DisplayName,
            user.Code, user.Role.ToString(), user.CredentialVersion, false));
    }

    // =================================================================
    //  البداية
    // =================================================================

    /// <summary>
    /// 🔴 <b>الفحص اللي بيثبت إن الجدول بيتستعمل فعلاً.</b>
    ///
    /// <para>توكن اتوقّع ومااتسجّلش بيشتغل ١٥ دقيقة وبعدين تجديده
    /// بيترفض — فالمستخدم بيتطرد ومحدش يعرف ليه.</para>
    /// </summary>
    [Fact]
    public async Task Starting_a_session_writes_a_row()
    {
        var (sessions, db) = Build();
        await using (db)
        {
            var (userId, subject) = await SeedUserAsync(db);

            var pair = await sessions.StartAsync(subject);

            var row = await db.RefreshTokens.SingleOrDefaultAsync(t => t.UserId == userId);
            Assert.NotNull(row);
            Assert.Null(row.RevokedAtUtc);
            Assert.Null(row.ReplacedByTokenId);
            Assert.Equal(RefreshTokenHash.Of(pair.RefreshToken), row.TokenHash);
        }
    }

    /// <summary>
    /// 🔴 <b>التوكن نفسه مش في الجدول — بصمته بس.</b>
    ///
    /// <para>لو حد قرا الجدول (نسخة احتياطية، سجل، موظف) مايقدرش
    /// يستعمل اللي فيه.</para>
    /// </summary>
    [Fact]
    public async Task The_raw_token_is_never_stored()
    {
        var (sessions, db) = Build();
        await using (db)
        {
            var (userId, subject) = await SeedUserAsync(db);

            var pair = await sessions.StartAsync(subject);
            var row = await db.RefreshTokens.SingleAsync(t => t.UserId == userId);

            Assert.NotEqual(pair.RefreshToken, row.TokenHash);
            Assert.DoesNotContain(row.TokenHash, pair.RefreshToken);
        }
    }

    /// <summary>
    /// ⚠️ تاريخ انتهاء الصف = تاريخ انتهاء التوكن نفسه.
    ///
    /// <para>لو الصف قال تاريخ والتوكن قال تاريخ تاني، يبقى إما
    /// التوكن يشتغل بعد ما الصف يقول انتهى أو العكس.</para>
    /// </summary>
    [Fact]
    public async Task The_row_expiry_matches_the_token_expiry()
    {
        var (sessions, db) = Build();
        await using (db)
        {
            var (userId, subject) = await SeedUserAsync(db);

            var pair = await sessions.StartAsync(subject);
            var row = await db.RefreshTokens.SingleAsync(t => t.UserId == userId);

            var fromToken = new JwtSecurityTokenHandler()
                .ReadJwtToken(pair.RefreshToken).ValidTo;

            Assert.Equal(fromToken, row.ExpiresAtUtc, TimeSpan.FromSeconds(1));
        }
    }

    // =================================================================
    //  التجديد واللفّ
    // =================================================================

    [Fact]
    public async Task Refreshing_rotates_the_token()
    {
        var (sessions, db) = Build();
        await using (db)
        {
            var (userId, subject) = await SeedUserAsync(db);
            var first = await sessions.StartAsync(subject);

            var result = await sessions.RefreshAsync(first.RefreshToken);

            Assert.True(result.IsSuccess);
            Assert.NotEqual(first.RefreshToken, result.Value!.Tokens.RefreshToken);

            db.ChangeTracker.Clear();
            var rows = await db.RefreshTokens.Where(t => t.UserId == userId).ToListAsync();
            Assert.Equal(2, rows.Count);

            var stale = rows.Single(r => r.TokenHash == RefreshTokenHash.Of(first.RefreshToken));
            var fresh = rows.Single(r =>
                r.TokenHash == RefreshTokenHash.Of(result.Value.Tokens.RefreshToken));

            Assert.NotNull(stale.RevokedAtUtc);
            Assert.Equal(fresh.Id, stale.ReplacedByTokenId);
            Assert.Null(fresh.RevokedAtUtc);
        }
    }

    /// <summary>
    /// 🔴 <b>التوكن القديم مابيشتغلش تاني بعد اللفّ.</b>
    ///
    /// <para>لو شغل، يبقى اللفّ شكل وبس: التوكن المسروق يفضل صالح
    /// للأبد.</para>
    /// </summary>
    [Fact]
    public async Task The_old_token_stops_working_after_rotation()
    {
        var (sessions, db) = Build();
        await using (db)
        {
            var (_, subject) = await SeedUserAsync(db);
            var first = await sessions.StartAsync(subject);
            await sessions.RefreshAsync(first.RefreshToken);

            Assert.True((await sessions.RefreshAsync(first.RefreshToken)).IsFailure);
        }
    }

    /// <summary>
    /// 🔴 <b>وإعادة استعمال توكن ملفوف بتقفل السلسلة كلها.</b>
    ///
    /// <para>لأن فيه نسختين منه ومش معروف مين قدّمه. فرفض الطلب وبس
    /// ممكن يطرد صاحب الحساب ويسيب الحرامي شغّال بالتوكن الجديد.</para>
    /// </summary>
    [Fact]
    public async Task Reusing_a_rotated_token_kills_every_live_session()
    {
        var (sessions, db) = Build();
        await using (db)
        {
            var (userId, subject) = await SeedUserAsync(db);
            var first = await sessions.StartAsync(subject);
            var second = await sessions.RefreshAsync(first.RefreshToken);
            Assert.True(second.IsSuccess);

            // الحرامي بيقدّم القديم
            await sessions.RefreshAsync(first.RefreshToken);

            // والتوكن الجديد — اللي عند صاحب الحساب — مابقاش شغّال
            Assert.True((await sessions.RefreshAsync(second.Value!.Tokens.RefreshToken)).IsFailure);

            db.ChangeTracker.Clear();
            int live = await db.RefreshTokens
                .CountAsync(t => t.UserId == userId && t.RevokedAtUtc == null);
            Assert.Equal(0, live);
        }
    }

    /// <summary>
    /// 🔴 <b>ادعاءات التوكن الجديد بتتقرا من القاعدة، مش من القديم.</b>
    ///
    /// <para>لو اتنقلت من التوكن، ترقية صلاحية مابتبانش غير لما
    /// المستخدم يسجّل دخول من جديد — يعني لحد أسبوع.</para>
    /// </summary>
    [Fact]
    public async Task A_role_change_shows_up_on_the_next_refresh()
    {
        var (sessions, db) = Build();
        await using (db)
        {
            var (userId, subject) = await SeedUserAsync(db);
            var first = await sessions.StartAsync(subject);

            var user = await db.Users.SingleAsync(u => u.Id == userId);
            user.Role = UserRole.Manager;
            user.DisplayName = "أحمد المدير";
            await db.SaveChangesAsync();

            var refreshed = await sessions.RefreshAsync(first.RefreshToken);

            Assert.True(refreshed.IsSuccess);
            var claims = new JwtSecurityTokenHandler()
                .ReadJwtToken(refreshed.Value!.Tokens.AccessToken).Claims.ToList();

            Assert.Equal("Manager", claims.First(c => c.Type.EndsWith("role")).Value);
            Assert.Equal("أحمد المدير", claims.First(c => c.Type == "display").Value);
        }
    }

    // =================================================================
    //  الطرد
    // =================================================================

    /// <summary>🔴 تغيير الباسورد بيزوّد النسخة — فالتجديد بيترفض.</summary>
    [Fact]
    public async Task Bumping_the_credential_version_rejects_the_refresh()
    {
        var (sessions, db) = Build();
        await using (db)
        {
            var (userId, subject) = await SeedUserAsync(db);
            var first = await sessions.StartAsync(subject);

            var user = await db.Users.SingleAsync(u => u.Id == userId);
            user.CredentialVersion++;
            await db.SaveChangesAsync();

            Assert.True((await sessions.RefreshAsync(first.RefreshToken)).IsFailure);
        }
    }

    [Fact]
    public async Task A_suspended_account_cannot_refresh()
    {
        var (sessions, db) = Build();
        await using (db)
        {
            var (userId, subject) = await SeedUserAsync(db);
            var first = await sessions.StartAsync(subject);

            var user = await db.Users.SingleAsync(u => u.Id == userId);
            user.IsActive = false;
            await db.SaveChangesAsync();

            Assert.True((await sessions.RefreshAsync(first.RefreshToken)).IsFailure);
        }
    }

    /// <summary>
    /// «اقفل كل الأجهزة» — <b>وبيفضل مسجَّل إنه إلغاء مش لفّ</b>.
    ///
    /// <para>⚠️ والفرق ده هو اللي بيمنع كل خروج عادي إنه يتسجّل
    /// «اشتباه سرقة».</para>
    /// </summary>
    [Fact]
    public async Task Ending_all_sessions_revokes_without_marking_theft()
    {
        var (sessions, db) = Build();
        await using (db)
        {
            var (userId, subject) = await SeedUserAsync(db);
            await sessions.StartAsync(subject);
            await sessions.StartAsync(subject);

            int closed = await sessions.EndAllAsync(userId, "خروج من كل الأجهزة");

            Assert.Equal(2, closed);

            db.ChangeTracker.Clear();
            var rows = await db.RefreshTokens.Where(t => t.UserId == userId).ToListAsync();
            Assert.All(rows, r =>
            {
                Assert.NotNull(r.RevokedAtUtc);
                Assert.Null(r.ReplacedByTokenId);
            });
        }
    }

    /// <summary>⚠️ وتوكن مالوش صف خالص بيترفض — مش بيرمي.</summary>
    [Fact]
    public async Task A_signed_token_with_no_row_is_rejected()
    {
        var (sessions, db) = Build();
        await using (db)
        {
            var (_, subject) = await SeedUserAsync(db);

            // توكن موقّع صح، بس عمره ما عدّى على `StartAsync`
            var orphan = NewIssuer().Issue(subject);

            Assert.True((await sessions.RefreshAsync(orphan.RefreshToken)).IsFailure);
        }
    }

    [Fact]
    public async Task Rubbish_is_rejected_without_throwing()
    {
        var (sessions, db) = Build();
        await using (db)
        {
            Assert.True((await sessions.RefreshAsync("مش-توكن-أصلاً")).IsFailure);
            Assert.True((await sessions.RefreshAsync("")).IsFailure);
        }
    }
}
