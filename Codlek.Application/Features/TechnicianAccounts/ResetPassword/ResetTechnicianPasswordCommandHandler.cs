using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Technicians;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Technicians;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Codlek.Application.Features.TechnicianAccounts.ResetPassword;

/// <summary>
/// باسورد جديد — <b>ونسخة البيانات بتزيد</b>.
///
/// <para>🔴 <b>الزيادة هي اللي بتوصل للراكات الأوفلاين.</b> النسخة
/// المحفوظة على الراكة شايلة الرقم القديم؛ وأول ما الراكة تتصل
/// وتلاقي رقم أحدث بتمسح نسختها. ومن غير الزيادة، تغيير الباسورد
/// على اللوحة مكانش هيمنع الدخول بالقديم على راكة أوفلاين.</para>
/// </summary>
public sealed class ResetTechnicianPasswordCommandHandler(
    ITechnicianAccountRepository accounts,
    ITechnicianPasswords passwords,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    ICurrentUser me,
    ILogger<ResetTechnicianPasswordCommandHandler> log)
    : IRequestHandler<ResetTechnicianPasswordCommand, Result<TechnicianSecretResponse>>
{
    public async Task<Result<TechnicianSecretResponse>> Handle(
        ResetTechnicianPasswordCommand command, CancellationToken cancellationToken)
    {
        string password = command.Password ?? "";

        if (password.Length < TechnicianAccountRules.MinPassword)
            return Result.Failure<TechnicianSecretResponse>(
                TechnicianAccountErrors.PasswordTooShort);

        var technician = await accounts.FindAsync(me.TenantId, command.Id, cancellationToken);

        if (technician is null)
            return Result.Failure<TechnicianSecretResponse>(
                TechnicianAccountErrors.NotFound);

        var (hash, salt) = passwords.Create(password);

        technician.PasswordHash = hash;
        technician.Salt = salt;

        // ⚠️ المدير حطّ الباسورد، فالفني لازم يغيّره أول دخول.
        technician.MustChangePassword = true;

        // 🔴 والزيادة دي هي اللي بتلغي النسخ المحفوظة.
        technician.CredentialVersion++;
        technician.UpdatedAtUtc = DateTime.UtcNow;

        audit.Record(
            AuditActions.TechnicianPasswordReset, "Technician",
            technician.Id, technician.Code,
            $"اتغيّرت كلمة مرور «{technician.DisplayName}»");

        await unitOfWork.SaveChangesAsync(cancellationToken);

        log.LogInformation(
            "اتغيّر باسورد الفني {Code} — النسخة بقت {Version}.",
            technician.Code, technician.CredentialVersion);

        return Result.Success(new TechnicianSecretResponse(
            TechnicianAccountMapping.Account(technician),
            password,
            $"اتغيّرت كلمة مرور «{technician.DisplayName}»"));
    }
}
