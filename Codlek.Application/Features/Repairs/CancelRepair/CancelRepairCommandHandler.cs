using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Repairs;
using Codlek.Application.Interfaces;
using MediatR;

namespace Codlek.Application.Features.Repairs.CancelRepair;

public sealed class CancelRepairCommandHandler(
    IRepairTransitions transitions,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    ICurrentUser me)
    : IRequestHandler<CancelRepairCommand, Result<RepairActionResponse>>
{
    public async Task<Result<RepairActionResponse>> Handle(
        CancelRepairCommand command, CancellationToken cancellationToken)
    {
        var result = await transitions.CancelAsync(
            me.TenantId, command.Id, command.Reason, cancellationToken);

        if (result.IsFailure) return Result.Failure<RepairActionResponse>(result.Error);

        /*
          ⚠️ **السبب الخام في السجل، مش المقصوص.**

          اللي بيقرا السجل عايز يشوف اللي المدير كتبه. والقص على العمود
          حصل في الانتقال؛ و`AuditTrail` بيقصّ لوحده على ٤٠٠ لو لزم.
        */
        audit.Record(
            AuditActions.RepairCancelled, "repair",
            command.Id, result.Value!.PublicCode, command.Reason ?? "");

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new RepairActionResponse("اتلغى"));
    }
}
