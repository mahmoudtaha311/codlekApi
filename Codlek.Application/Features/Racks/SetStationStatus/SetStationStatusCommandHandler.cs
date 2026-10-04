using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Racks;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Enums;
using Codlek.Core.Racks;
using MediatR;

namespace Codlek.Application.Features.Racks.SetStationStatus;

public sealed class SetStationStatusCommandHandler(
    IRackRepository racks,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    ICurrentUser me)
    : IRequestHandler<SetStationStatusCommand, Result<RackActionResponse>>
{
    public async Task<Result<RackActionResponse>> Handle(
        SetStationStatusCommand command, CancellationToken cancellationToken)
    {
        var rack = await racks.FindAsync(me.TenantId, command.Id, cancellationToken);

        if (rack is null)
            return Result.Failure<RackActionResponse>(RackErrors.NotFound);

        /*
          🔴 **الملغية نهائياً مابترجعش.**

          الرجوع معناه إن مفتاح **اتلغى** يبقى صالح تاني — واللي
          اتلغى غالباً اتلغى عشان اتسرق أو عشان الراكة اتستنسخت.
          والرفض ده قبل أي تعديل، مش بعده.
        */
        if (!RackPolicy.CanChangeStatus(rack.Status))
            return Result.Failure<RackActionResponse>(RackErrors.Revoked);

        rack.Status = command.Resume ? RackStatus.Active : RackStatus.Suspended;

        audit.Record(
            command.Resume ? AuditActions.RackResumed : AuditActions.RackSuspended,
            "Rack", rack.Id, rack.RackCode,
            $"«{rack.Name}» " + (command.Resume ? "رجعت تشتغل" : "اتوقفت"));

        await unitOfWork.SaveChangesAsync(cancellationToken);

        /*
          ⚠️ **ورسالة الإيقاف بتقول العاقبة، مش الحالة.**

          «اتوقفت» لوحدها مابتقولش إيه اللي حصل للمحطة اللي في
          الميدان. «مش هتقدر ترفع تاني» هي اللي بتخلّي المالك يعرف إن
          الفني اللي بيفحص دلوقتي هيلاقي الرفع بيرفض.
        */
        return Result.Success(new RackActionResponse(
            command.Resume
                ? $"«{rack.Name}» رجعت تشتغل"
                : $"«{rack.Name}» اتوقفت — مش هتقدر ترفع تاني"));
    }
}
