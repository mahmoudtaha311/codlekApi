using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Auth;
using Codlek.Application.Interfaces;
using Codlek.Core.Entities;
using Codlek.Core.Entities.Auth;
using Codlek.Core.Text;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Codlek.Application.Features.Auth.Login;

/// <summary>
/// بيتحقق من الباسورد وبيبدأ جلسة.
///
/// <para>⚠️ التحقق من الباسورد نفسه مش هنا —
/// <c>LegacyPasswordHasher</c> هو اللي بيعمله، وهو اللي بيفهم الشكل
/// القديم والجديد. الحتة دي بتقرّر، مابتحسبش بصمات.</para>
/// </summary>
public sealed class LoginCommandHandler(
    UserManager<ApplicationUser> users,
    ILoginSessions sessions,
    ILoginEventLog events,
    IUnitOfWork unitOfWork,
    ILogger<LoginCommandHandler> log)
    : IRequestHandler<LoginCommand, Result<AuthResponse>>
{
    /// <summary>أسباب الرفض في السجل — <b>نفس كلام القديم بالحرف</b>.</summary>
    public const string WrongCredentialsReason = "بيانات دخول غلط";

    public const string SuspendedReason = "الحساب موقوف";

    /// <summary>
    /// 🔴 <b>كل محاولة بتتسجّل — والفاشلة بتتحفظ على طول.</b> زي القديم:
    /// المحاولة الفاشلة مالهاش حفظ تاني بعدها، ولو ماتحفظتش هنا
    /// بتضيع — وهي بالظبط الدليل على التخمين.
    /// </summary>
    private async Task RecordAsync(
        LoginCommand command, ApplicationUser? user, bool success, string reason,
        CancellationToken ct)
    {
        events.Add(new LoginEvent
        {
            TenantId = user?.TenantId ?? Guid.Empty,
            Username = TextClip.To((command.Username ?? "").Trim(), 60),
            DisplayName = TextClip.To(user?.DisplayName ?? "", 120),
            Success = success,
            Reason = reason,
            Ip = TextClip.To(command.Ip ?? "", 60),
            AtUtc = DateTime.UtcNow,
        });

        if (!success) await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<Result<AuthResponse>> Handle(
        LoginCommand command, CancellationToken cancellationToken)
    {
        var user = await users.FindByNameAsync(command.Username);

        if (user is null)
        {
            log.LogWarning("دخول مرفوض: اسم مش موجود — {Username}.", command.Username);
            await RecordAsync(command, null, false, WrongCredentialsReason, cancellationToken);
            return Result.Failure<AuthResponse>(LoginErrors.InvalidCredentials);
        }

        /*
          🔴 **`CheckPasswordAsync` مش مقارنة نصوص.**

          هي اللي بتنده `IPasswordHasher<ApplicationUser>` — واللي
          `Replace` حطّت مكانه `LegacyPasswordHasher`. يعني الباسوردات
          القديمة بتعدّي من هنا، وبتترقّى للشكل الجديد لوحدها.

          ⚠️ ولو حد غيّر السطر ده لمقارنة يدوية، الترقية التلقائية
          بتضيع — والنظام بيفضل على الشكل القديم للأبد.
        */
        if (!await users.CheckPasswordAsync(user, command.Password))
        {
            log.LogWarning("دخول مرفوض: باسورد غلط — {UserId}.", user.Id);
            await RecordAsync(command, user, false, WrongCredentialsReason, cancellationToken);
            return Result.Failure<AuthResponse>(LoginErrors.InvalidCredentials);
        }

        // ⚠️ الإيقاف بيتفحص **بعد** الباسورد.
        //
        // لأن لو قبله، أي حد يكتب اسم حساب موقوف بياخد «الحساب موقوف»
        // — فيعرف إن الاسم ده موجود من غير ما يعرف الباسورد.
        if (!user.IsActive)
        {
            log.LogWarning("دخول مرفوض: حساب موقوف — {UserId}.", user.Id);
            await RecordAsync(command, user, false, SuspendedReason, cancellationToken);
            return Result.Failure<AuthResponse>(LoginErrors.AccountSuspended);
        }

        /*
          ⚠️ **آخر دخول بيتختم هنا — زي القديم.** صفحة المستخدمين وصفحة
          الحساب بيعرضوه، والجديد كان بيقراه ومابيكتبوش: يعني بعد التحويل
          «آخر دخول» كان هيفضل واقف على تاريخ النقل لكل الناس.

          والحفظ بيمشي مع سطر السجل في نفس المرة — المستخدم متتبّع من
          نفس السياق، و`ConcurrencyStamp` مابيتغيّرش.
        */
        user.LastLoginUtc = DateTime.UtcNow;

        await RecordAsync(command, user, true, "", cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var subject = new TokenSubject(
            UserId: user.Id,
            TenantId: user.TenantId,
            Username: user.UserName ?? "",
            DisplayName: user.DisplayName,
            Code: user.Code,
            Role: user.Role.ToString(),
            CredentialVersion: user.CredentialVersion,
            MustChangePassword: user.MustChangePassword);

        var pair = await sessions.StartAsync(subject, cancellationToken);

        log.LogInformation("دخول ناجح — {UserId} ({Role}).", user.Id, user.Role);

        return Result.Success(new AuthResponse(
            pair.AccessToken,
            pair.RefreshToken,
            pair.ExpiresInSeconds,
            user.Id,
            user.DisplayName,
            user.Role.ToString(),
            user.MustChangePassword));
    }
}
