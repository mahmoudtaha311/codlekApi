using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Auth;
using Codlek.Application.Interfaces;
using Codlek.Core.Auth;
using Codlek.Core.Entities;
using Codlek.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Codlek.Infrastructure.Auth;

/// <summary>
/// جلسات الدخول على جدول <c>RefreshTokens</c>.
///
/// <para>⚠️ القرار نفسه مش هنا — هو في <see cref="RefreshRules"/>
/// (دالة نقية). الكلاس ده بيجيب الصفوف، بيسأل القاعدة النقية،
/// وبينفّذ. والفصل ده هو اللي خلّى الست حالات رفض كلها متجرّبة.</para>
/// </summary>
public sealed class LoginSessions(
    AppDbContext db,
    ITokenIssuer issuer,
    ILogger<LoginSessions> log) : ILoginSessions
{
    public async Task<TokenPair> StartAsync(TokenSubject subject, CancellationToken ct = default)
    {
        var pair = issuer.Issue(subject);
        Record(subject, pair.RefreshToken);
        await db.SaveChangesAsync(ct);
        return pair;
    }

    public async Task<Result<RefreshedSession>> RefreshAsync(
        string refreshToken, CancellationToken ct = default)
    {
        // ١ · التوقيع الأول — رخيص، ومابيلمسش القاعدة.
        var claims = issuer.ReadRefresh(refreshToken);
        if (claims is null)
        {
            log.LogWarning("تجديد مرفوض: توكن توقيعه مش سليم أو منتهي.");
            return Result.Failure<RefreshedSession>(RefreshErrors.Rejected);
        }

        string hash = RefreshTokenHash.Of(refreshToken);

        var row = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == claims.UserId, ct);

        var outcome = RefreshRules.Evaluate(
            tokenKnown: row is not null,
            revokedAtUtc: row?.RevokedAtUtc,
            wasRotated: row?.ReplacedByTokenId is not null,
            expiresAtUtc: row?.ExpiresAtUtc ?? DateTime.MinValue,
            accountActive: user is { IsActive: true },
            accountVersion: user?.CredentialVersion ?? -1,
            tokenVersion: claims.CredentialVersion,
            nowUtc: DateTime.UtcNow);

        if (outcome is not RefreshOutcome.Allowed)
        {
            /*
              🔴 **السرقة بتقفل السلسلة كلها، مش الطلب ده وبس.**

              لأن اللي اتقدّم هنا توكن قديم اتبدّل. يعني فيه نسختين:
              واحدة عند صاحب الحساب وواحدة عند حد تاني. ومش معروف مين
              اللي قدّم ده. فرفض الطلب وبس ممكن يطرد صاحب الحساب
              ويسيب الحرامي شغّال بالتوكن الجديد.

              القفل الشامل بيطرد الاتنين — صاحب الحساب بيدخل تاني
              بباسورده، والحرامي مامعاهوش باسورد.
            */
            if (outcome is RefreshOutcome.Reused && row is not null)
            {
                int killed = await EndAllAsync(row.UserId, "اشتباه سرقة توكن", ct);
                log.LogError(
                    "🔴 توكن تجديد اتستعمل بعد ما اتبدّل — المستخدم {UserId}. " +
                    "اتقفلت {Count} جلسة.", row.UserId, killed);
            }
            else
            {
                log.LogWarning("تجديد مرفوض: {Outcome} للمستخدم {UserId}.",
                    outcome, claims.UserId);
            }

            return Result.Failure<RefreshedSession>(RefreshErrors.Rejected);
        }

        /*
          ٢ · 🔴 **الادعاءات بتتبني من الصف، مش من التوكن القديم.**

          لو نقلناها من التوكن، ترقية صلاحية أو تغيير اسم مايبانش غير
          لما المستخدم يسجّل دخول من جديد — يعني لحد أسبوع. كده بيبان
          في ربع ساعة.
        */
        var subject = new TokenSubject(
            UserId: user!.Id,
            TenantId: user.TenantId,
            Username: user.UserName ?? "",
            DisplayName: user.DisplayName,
            Code: user.Code,
            Role: user.Role.ToString(),
            CredentialVersion: user.CredentialVersion,
            MustChangePassword: user.MustChangePassword);

        var pair = issuer.Issue(subject);
        var replacement = Record(subject, pair.RefreshToken);

        // ٣ · ⚠️ **اللفّ لازم يبقى في نفس الحفظ.**
        //
        // لو القديم اتلغى في حفظة والجديد في حفظة تانية، وقعت القاعدة
        // بينهم = المستخدم مابقاش معاه ولا توكن شغّال. حفظة واحدة
        // معناها الاتنين بيحصلوا أو ولا واحد.
        row!.RevokedAtUtc = DateTime.UtcNow;
        row.ReplacedByTokenId = replacement.Id;
        row.RevokedReason = "تجديد";

        await db.SaveChangesAsync(ct);
        return Result.Success(new RefreshedSession(pair, subject));
    }

    public async Task<int> EndAllAsync(
        Guid userId, string reason, CancellationToken ct = default)
    {
        var live = await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        foreach (var t in live)
        {
            t.RevokedAtUtc = now;

            // ⚠️ `ReplacedByTokenId` بيفضل فاضي — ده إلغاء مش لفّ.
            // والفرق ده هو اللي بيفرّق «خروج» عن «سرقة» في الفحص.
            t.RevokedReason = reason;
        }

        await db.SaveChangesAsync(ct);
        return live.Count;
    }

    /// <summary>بيضيف الصف للتتبّع — <b>مابيحفزش</b>. الحفز مسؤولية المنادي.</summary>
    private RefreshToken Record(TokenSubject subject, string refreshToken)
    {
        var row = new RefreshToken
        {
            TenantId = subject.TenantId,
            UserId = subject.UserId,
            TokenHash = RefreshTokenHash.Of(refreshToken),
            ExpiresAtUtc = ReadExpiry(refreshToken),
        };

        db.RefreshTokens.Add(row);
        return row;
    }

    /// <summary>
    /// تاريخ انتهاء التوكن — <b>من التوكن نفسه</b>.
    ///
    /// <para>🔴 <b>مش من الإعدادات.</b> لو قرأناه من
    /// <c>RefreshTokenDays</c>، أي تغيير في الإعداد بيخلّي الصف يقول
    /// تاريخ والتوكن يقول تاريخ تاني. وساعتها إما التوكن يشتغل بعد ما
    /// الصف يقول انتهى، أو العكس — والاتنين بيبانوا كأعطال عشوائية.</para>
    /// </summary>
    private static DateTime ReadExpiry(string refreshToken)
    {
        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler()
            .ReadJwtToken(refreshToken);

        return token.ValidTo;
    }
}
