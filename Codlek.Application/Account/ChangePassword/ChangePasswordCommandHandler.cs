using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Account;
using Codlek.Application.Contracts.Auth;
using Codlek.Application.Interfaces;
using Codlek.Core.Entities.Auth;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Codlek.Application.Account.ChangePassword;

/// <summary>
/// بيغيّر باسورد المستخدم الحالي.
///
/// <para>🔴 <b>وبيطفّي باقي أجهزته.</b> تغيير الباسورد بيزوّد
/// <c>CredentialVersion</c>، فأي توكن تجديد قديم (لابتوب مسروق، تاب
/// مفتوح في مكان تاني) بيترفض عند أول تجديد. من غير الزيادة دي،
/// «غيّرت الباسورد» مكانش بيغيّر أي حاجة عند اللي فاتح الحساب فعلاً.</para>
/// </summary>
public sealed class ChangePasswordCommandHandler(
    UserManager<ApplicationUser> users,
    ILoginSessions sessions,
    ICurrentUser me,
    ILogger<ChangePasswordCommandHandler> log)
    : IRequestHandler<ChangePasswordCommand, Result<PasswordChangedResponse>>
{
    public async Task<Result<PasswordChangedResponse>> Handle(
        ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(me.Id.ToString());

        // ⚠️ والشركة بتتفحص كمان: حساب اتنقل لشركة تانية بعد ما
        // التوكن اتعمل = جلسة مابقتش صالحة، مش حساب تاني.
        if (user is null || user.TenantId != me.TenantId)
            return Result.Failure<PasswordChangedResponse>(AccountErrors.SessionNoLongerValid);

        /*
          🔴 **الترتيب مقصود: الحالية الأول.**

          لو فحصنا الطول أو التطابق قبلها، الرد كان بيفرّق بين
          «الحالية غلط» و«الجديدة قصيرة» — <b>لواحد مش عارف الباسورد
          أصلاً</b>. يعني اللي قاعد على جهاز سايب مفتوح بياخد منها
          معلومة.
        */
        if (!await users.CheckPasswordAsync(user, command.CurrentPassword))
        {
            log.LogWarning("تغيير باسورد مرفوض: الحالية غلط — {UserId}.", user.Id);
            return Result.Failure<PasswordChangedResponse>(AccountErrors.CurrentPasswordWrong);
        }

        if (command.NewPassword != command.ConfirmPassword)
            return Result.Failure<PasswordChangedResponse>(AccountErrors.ConfirmationMismatch);

        /*
          ⚠️ **التغيير بيعدّي على `UserManager`، مش على البصمة مباشرة.**

          لأنه هو اللي بينده `IPasswordHasher` المسجَّل — واللي هو
          `LegacyPasswordHasher`. يعني الباسورد الجديد بيتخزّن بالشكل
          الجديد **وعلامة الشكل القديم بتتشال** في نفس العملية. ولو
          كتبنا البصمة بإيدينا، الصف كان هيبقى فيه بصمة جديدة وملح
          قديم — وهي الحالة اللي بتقفل الحساب.
        */
        var result = await users.ChangePasswordAsync(
            user, command.CurrentPassword, command.NewPassword);

        if (!result.Succeeded)
        {
            string reason = string.Join(" ", result.Errors.Select(Describe));

            log.LogWarning("تغيير باسورد مرفوض: {Reason} — {UserId}.", reason, user.Id);

            return Result.Failure<PasswordChangedResponse>(
                AccountErrors.NewPasswordRejected(reason));
        }

        user.MustChangePassword = false;

        /*
          🔴 **زيادة النسخة هي اللي بتطفّي الأجهزة التانية.**

          وبتحصل بعد نجاح التغيير، مش قبله: لو زوّدناها الأول وفشل
          التغيير، كان المستخدم بيتطرد من كل أجهزته **وباسورده
          ماتغيّرش** — أسوأ حالة ممكنة.
        */
        user.CredentialVersion++;

        await users.UpdateAsync(user);

        // ⚠️ وكل الجلسات المفتوحة بتتقفل في الجدول كمان، مش بالنسخة
        // بس. الصف الملغي بيقول «اتقفلت ليه وإمتى» — والنسخة لوحدها
        // مابتسجّلش حاجة.
        int closed = await sessions.EndAllAsync(
            user.Id, "تغيير كلمة المرور", cancellationToken);

        /*
          🔴 **وبنبدأ جلسة جديدة فوراً — وإلا اللي غيّر الباسورد
          بيتطرد هو كمان.**

          `EndAllAsync` قفلت كل حاجة، والتوكن اللي في إيده بقى على
          نسخة قديمة. فمن غير السطر ده، المستخدم بينجح في التغيير
          وبعدين بيلاقي نفسه مطرود.
        */
        var pair = await sessions.StartAsync(
            new TokenSubject(
                UserId: user.Id,
                TenantId: user.TenantId,
                Username: user.UserName ?? "",
                DisplayName: user.DisplayName,
                Code: user.Code,
                Role: user.Role.ToString(),
                CredentialVersion: user.CredentialVersion,
                MustChangePassword: false),
            cancellationToken);

        log.LogInformation(
            "الباسورد اتغيّر — {UserId}، واتقفلت {Count} جلسة.", user.Id, closed);

        return Result.Success(new PasswordChangedResponse(
            "كلمة المرور اتغيّرت.",
            pair.AccessToken,
            pair.RefreshToken,
            pair.ExpiresInSeconds));
    }

    /// <summary>
    /// رسالة Identity بالعربي.
    ///
    /// <para>⚠️ رسايل Identity بالإنجليزي («Passwords must be at least
    /// 8 characters»). والمستخدم هنا بيقرا عربي — فالرسالة الإنجليزية
    /// بتبان كأنها عطل مش توجيه.</para>
    /// </summary>
    private static string Describe(IdentityError error) => error.Code switch
    {
        "PasswordTooShort" => "كلمة المرور الجديدة قصيرة أوي.",
        "PasswordRequiresDigit" => "كلمة المرور لازم يكون فيها رقم.",
        "PasswordRequiresLower" => "كلمة المرور لازم يكون فيها حرف صغير.",
        "PasswordRequiresUpper" => "كلمة المرور لازم يكون فيها حرف كبير.",
        "PasswordRequiresUniqueChars" => "كلمة المرور حروفها مكررة أوي.",
        "PasswordMismatch" => "كلمة المرور الحالية غلط.",

        // ⚠️ أي كود مش معروف بيرجع بنصه الإنجليزي بدل ما يتاكل.
        // رسالة وحشة أهون من رسالة فاضية.
        _ => error.Description,
    };
}
