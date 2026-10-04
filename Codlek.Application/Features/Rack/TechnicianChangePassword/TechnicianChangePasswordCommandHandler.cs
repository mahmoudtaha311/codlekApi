using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Rack;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Core.Technicians;
using Codlek.Core.Text;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Codlek.Application.Features.Rack.TechnicianChangePassword;

/// <inheritdoc cref="TechnicianChangePasswordCommand"/>
public sealed class TechnicianChangePasswordCommandHandler(
    ITechnicianLoginRepository logins,
    ITechnicianPasswords passwords,
    IUnitOfWork unitOfWork,
    ILogger<TechnicianChangePasswordCommandHandler> log)
    : IRequestHandler<TechnicianChangePasswordCommand, Result<TechnicianPasswordChanged>>
{
    public async Task<Result<TechnicianPasswordChanged>> Handle(
        TechnicianChangePasswordCommand command, CancellationToken cancellationToken)
    {
        string key = LoginName.Normalize(command.Username);
        var now = DateTime.UtcNow;

        // ⚠️ نفس عدّاد الدخول بالظبط (محطة + اسم) — مش عدّاد خاص.
        bool throttled = false;

        if (key.Length > 0)
        {
            int recent = await logins.RecentFailuresAsync(
                command.RackId, key,
                TechnicianLoginRules.WindowStart(now), cancellationToken);

            throttled = TechnicianLoginRules.Throttled(recent);
        }

        /*
          🔴 **والبحث مابيحصلش خالص لو مقفول.**

          مش تحسين: استعلام بالاسم على كود مقفول بيخلّي زمن الرد
          يفرّق بين «اسم موجود» و«اسم مش موجود» — يعني العدّاد بيقفل
          التخمين بالباسورد وبيسيب التخمين بالاسم مفتوح.
        */
        var technician = throttled
            ? null
            : await logins.FindByLoginKeyAsync(command.TenantId, key, cancellationToken);

        bool currentOk = technician is not null
            && passwords.Verify(
                   command.CurrentPassword ?? "",
                   technician.PasswordHash, technician.Salt);

        /*
          ⚠️ **و«الجديد زي القديم» بيتفحص بالبصمة مش بمقارنة نصين.**

          البصمة القديمة هي اللي عندنا، والنص القديم عمره ما اتخزّن.
        */
        bool sameAsCurrent = currentOk
            && technician is not null
            && passwords.Verify(
                   command.NewPassword ?? "",
                   technician.PasswordHash, technician.Salt);

        var decision = TechnicianPasswordChangeRules.Decide(
            throttled,
            technician is not null,
            currentOk,
            technician?.IsActive ?? false,
            command.NewPassword,
            command.ConfirmPassword,
            sameAsCurrent);

        if (!decision.Ok)
        {
            /*
              🔴 **الباسورد الحالي الغلط بس هو اللي بيتسجّل.**

              باسورد جديد قصير مش محاولة تخمين — وتسجيله كان بيقفل
              **الدخول** على الفني بسبب غلطة إملائية.
            */
            if (TechnicianPasswordChangeRules.CountsAsFailedAttempt(decision.Code))
            {
                logins.AddAttempt(new TechnicianLoginAttempt
                {
                    TenantId = command.TenantId,
                    RackId = command.RackId,
                    TechnicianId = technician?.Id,
                    // ⚠️ مفيش قص — `Normalize` قصّه قبل كده.
                    AttemptedUsername = key,
                    Success = false,
                    Reason = TechnicianLoginRules.ReasonChangeWrongCurrent,
                    AtUtc = now,
                });

                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return Result.Failure<TechnicianPasswordChanged>(
                new Error(
                    decision.Code, decision.Message,
                    TechnicianAuthCodes.ChangeStatus(decision.Code)));
        }

        var (hash, salt) = passwords.Create(command.NewPassword!);

        technician!.PasswordHash = hash;
        technician.Salt = salt;

        // 🔴 **ودي الحاجة اللي الميزة كلها اتعملت عشانها.**
        technician.MustChangePassword = false;

        /*
          🔴 **والنسخة بتزيد زي إعادة التعيين بالظبط.**

          أي جهاز تاني لسه فاكر الباسورد القديم بيبقى على نسخة
          قديمة — ومن غير الزيادة دي، لابتوب مسروق فيه نسخة محفوظة
          بيفضل شغّال بعد ما الفني غيّر باسورده.
        */
        technician.CredentialVersion++;
        technician.UpdatedAtUtc = now;

        /*
          ⚠️ **ومفيش صف في جدول المحاولات هنا — لا ناجح ولا فاشل.**

          الناجح دليل تصريح أوفلاين، والفاشل بيصرف من رصيد دخول
          الفني. وتغيير باسورد ناجح مش واحد ولا التاني.
        */
        await unitOfWork.SaveChangesAsync(cancellationToken);

        log.LogInformation(
            "الفني {Code} غيّر باسورده من محطة {Rack} — النسخة بقت {Version}",
            technician.Code, command.RackCode, technician.CredentialVersion);

        return Result.Success(new TechnicianPasswordChanged(
            TechnicianId: technician.Id,
            CredentialVersion: technician.CredentialVersion,
            MustChangePassword: technician.MustChangePassword,
            Message: decision.Message));
    }
}
