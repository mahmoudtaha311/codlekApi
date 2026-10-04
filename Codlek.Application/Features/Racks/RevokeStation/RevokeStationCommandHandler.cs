using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Racks;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Enums;
using Codlek.Core.Racks;
using MediatR;

namespace Codlek.Application.Features.Racks.RevokeStation;

/// <summary>
/// إلغاء محطة نهائياً — <b>المفتاح مبقاش ينفع</b>.
///
/// <para>🔴 والمحطة محتاجة <b>كود تفعيل جديد</b> عشان ترجع. مفيش
/// زرار بيرجّعها.</para>
/// </summary>
public sealed class RevokeStationCommandHandler(
    IRackRepository racks,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    ICurrentUser me)
    : IRequestHandler<RevokeStationCommand, Result<RackActionResponse>>
{
    public async Task<Result<RackActionResponse>> Handle(
        RevokeStationCommand command, CancellationToken cancellationToken)
    {
        string reason = (command.Reason ?? "").Trim();

        /*
          🔴 **السبب بيتفحص قبل قراية القاعدة.**

          الإلغاء نهائي، فالسبب هو الحاجة الوحيدة اللي بتفضل تشرح
          ليه مفتاح محطة اتلغى. ولو الفحص جا بعد القراية، كان فيه
          نداء بيقرا صف ويرجّع رفض — شغل زيادة على نفس الرد.
        */
        if (reason.Length < RackPolicy.MinRevokeReason)
            return Result.Failure<RackActionResponse>(RackErrors.RevokeReasonRequired);

        var rack = await racks.FindAsync(me.TenantId, command.Id, cancellationToken);

        if (rack is null)
            return Result.Failure<RackActionResponse>(RackErrors.NotFound);

        /*
          ⚠️ **والملغية مرة تانية بتعدّي زي القديم.**

          الإلغاء مابيرجعش حاجة، فتكراره مابيأذيش — بس بيكتب وقت
          وسبب جديدين وسطر سجل تاني. وده مقصود: المالك اللي بيلغي
          محطة ملغية بيسجّل سبب أوضح، والسطر الأول مش بيروح.
        */
        rack.Status = RackStatus.Revoked;
        rack.RevokedAtUtc = DateTime.UtcNow;
        rack.RevokedReason = reason;

        audit.Record(
            AuditActions.RackRevoked, "Rack", rack.Id, rack.RackCode,
            $"«{rack.Name}» اتلغت نهائياً: {reason}");

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new RackActionResponse($"«{rack.Name}» اتلغت نهائياً"));
    }
}
