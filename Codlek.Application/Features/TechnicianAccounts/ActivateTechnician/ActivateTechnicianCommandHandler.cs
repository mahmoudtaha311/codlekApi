using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Technicians;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using MediatR;

namespace Codlek.Application.Features.TechnicianAccounts.ActivateTechnician;

public sealed class ActivateTechnicianCommandHandler(
    ITechnicianAccountRepository accounts,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    ICurrentUser me)
    : IRequestHandler<ActivateTechnicianCommand, Result<TechnicianActionResponse>>
{
    public async Task<Result<TechnicianActionResponse>> Handle(
        ActivateTechnicianCommand command, CancellationToken cancellationToken)
    {
        var technician = await accounts.FindAsync(me.TenantId, command.Id, cancellationToken);

        if (technician is null)
            return Result.Failure<TechnicianActionResponse>(
                TechnicianAccountErrors.NotFound);

        technician.IsActive = true;

        // ⚠️ وبيانات الإيقاف بتتفضّى — الصفحة بتعرضها لو موجودة.
        technician.SuspendedReason = "";
        technician.SuspendedByName = "";
        technician.SuspendedAtUtc = null;
        technician.UpdatedAtUtc = DateTime.UtcNow;

        /*
          🔴 **ومفيش زيادة في نسخة البيانات هنا عن قصد.**

          التشغيل مش إلغاء صلاحية — وزيادة الرقم كانت هتمسح نسخ
          **صالحة** على الراكات من غير سبب، فالفني يرجع للخدمة ويلاقي
          نفسه مطرود من كل محطة.
        */
        audit.Record(
            AuditActions.TechnicianActivated, "Technician",
            technician.Id, technician.Code,
            $"رجع «{technician.DisplayName}» للخدمة");

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new TechnicianActionResponse(
            TechnicianAccountMapping.Account(technician),
            $"رجع «{technician.DisplayName}» للخدمة"));
    }
}
