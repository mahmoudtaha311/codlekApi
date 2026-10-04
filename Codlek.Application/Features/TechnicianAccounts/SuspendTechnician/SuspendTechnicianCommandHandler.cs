using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Technicians;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Technicians;
using MediatR;

namespace Codlek.Application.Features.TechnicianAccounts.SuspendTechnician;

/// <summary>
/// إيقاف الفني — <b>ولازم معاه سبب</b>.
///
/// <para>🔴 والسبب بيوصل لشاشة الراكة: من غيره الفني بيقف قدام
/// رسالة مقفولة ومش عارف يكلّم مين، فبيروح يجرّب حساب زميله — وده
/// بالظبط اللي الإيقاف بيمنعه.</para>
///
/// <para>🔴 <b>ونسخة البيانات بتزيد هنا كمان:</b> الإيقاف لازم
/// يلغي النسخ المحفوظة على الراكات، مش بس يمنع الدخول
/// الجديد.</para>
/// </summary>
public sealed class SuspendTechnicianCommandHandler(
    ITechnicianAccountRepository accounts,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    ICurrentUser me)
    : IRequestHandler<SuspendTechnicianCommand, Result<TechnicianActionResponse>>
{
    public async Task<Result<TechnicianActionResponse>> Handle(
        SuspendTechnicianCommand command, CancellationToken cancellationToken)
    {
        string reason = (command.Reason ?? "").Trim();

        if (reason.Length < TechnicianAccountRules.MinSuspendReason)
            return Result.Failure<TechnicianActionResponse>(
                TechnicianAccountErrors.SuspendReasonRequired);

        var technician = await accounts.FindAsync(me.TenantId, command.Id, cancellationToken);

        if (technician is null)
            return Result.Failure<TechnicianActionResponse>(
                TechnicianAccountErrors.NotFound);

        technician.IsActive = false;
        technician.SuspendedReason = reason;

        // ⚠️ ومين أوقفه — الفني بيشوف الاسم كمان.
        technician.SuspendedByName = me.DisplayName;
        technician.SuspendedAtUtc = DateTime.UtcNow;

        // 🔴 الزيادة بتلغي النسخ المحفوظة على الراكات.
        technician.CredentialVersion++;
        technician.UpdatedAtUtc = DateTime.UtcNow;

        audit.Record(
            AuditActions.TechnicianSuspended, "Technician",
            technician.Id, technician.Code,
            $"اتوقف الفني «{technician.DisplayName}» — {reason}");

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new TechnicianActionResponse(
            TechnicianAccountMapping.Account(technician),
            $"اتوقف «{technician.DisplayName}»"));
    }
}
