using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Auth;
using Codlek.Application.Interfaces;
using Codlek.Core.Entities.Auth;
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
    ILogger<LoginCommandHandler> log)
    : IRequestHandler<LoginCommand, Result<AuthResponse>>
{
    public async Task<Result<AuthResponse>> Handle(
        LoginCommand command, CancellationToken cancellationToken)
    {
        var user = await users.FindByNameAsync(command.Username);

        if (user is null)
        {
            log.LogWarning("دخول مرفوض: اسم مش موجود — {Username}.", command.Username);
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
            return Result.Failure<AuthResponse>(LoginErrors.InvalidCredentials);
        }

        // ⚠️ الإيقاف بيتفحص **بعد** الباسورد.
        //
        // لأن لو قبله، أي حد يكتب اسم حساب موقوف بياخد «الحساب موقوف»
        // — فيعرف إن الاسم ده موجود من غير ما يعرف الباسورد.
        if (!user.IsActive)
        {
            log.LogWarning("دخول مرفوض: حساب موقوف — {UserId}.", user.Id);
            return Result.Failure<AuthResponse>(LoginErrors.AccountSuspended);
        }

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
