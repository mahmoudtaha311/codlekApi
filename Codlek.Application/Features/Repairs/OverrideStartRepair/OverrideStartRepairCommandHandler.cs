using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Repairs;
using Codlek.Application.Contracts.Workflow;
using Codlek.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Codlek.Application.Features.Repairs.OverrideStartRepair;

/// <summary>
/// المالك بيبدأ أمر نيابةً عن الفني.
///
/// <para>🔴 <b>الوقت المتخزّن هو وقت <i>دوسة المدير</i> — وده السبب
/// إنه بيتسجّل كتجاوز.</b> الفني ممكن يكون بدأ الشغل الصبح؛ اللي
/// النظام بيعرفه هو إن المدير دوس دلوقتي.</para>
///
/// <para>⚠️ <b>والسياسة على النقطة <c>ManagerOrAbove</c> والقفل جوّه
/// على المالك.</b> فالمدير بيعدّي الحاجز وبياخد <c>403</c> من هنا.
/// منقول زي ما هو عن قصد — تضييقه لـ<c>OwnerOnly</c> بيغيّر أنهي
/// طبقة بترد وبالتالي شكل الرد.</para>
/// </summary>
public sealed class OverrideStartRepairCommandHandler(
    IRepairTransitions transitions,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    ICurrentUser me,
    ILogger<OverrideStartRepairCommandHandler> log)
    : IRequestHandler<OverrideStartRepairCommand, Result<RepairActionResponse>>
{
    public async Task<Result<RepairActionResponse>> Handle(
        OverrideStartRepairCommand command, CancellationToken cancellationToken)
    {
        // ١ · المالك بس.
        if (!me.IsOwner)
        {
            log.LogWarning(
                "تجاوز إداري مرفوض: {Role} مش المدير العام — {UserId}.", me.Role, me.Id);

            return Result.Failure<RepairActionResponse>(RepairErrors.OverrideOwnerOnly);
        }

        // ٢ · والسبب إجباري — راجع `OverrideReasonRequired`.
        string reason = (command.Reason ?? "").Trim();

        if (reason.Length == 0)
            return Result.Failure<RepairActionResponse>(RepairErrors.OverrideReasonRequired);

        /*
          ٣ · والباقي في الانتقال — وفيه حاجز الموافقة.

          🔴 **المالك بيعدّي الموافقة بإنه يوافق الأول**، مش بإنه
          يتجاوزها. هو داخل في سياسة `RepairApprover` فقرارين مكتوبين
          بدل واحد بيخبّي التاني.
        */
        var result = await transitions.StartAsync(
            me.TenantId,
            command.Id,
            command.TechnicianId,
            WorkflowActor.User(me.Id, me.DisplayName),

            // ⚠️ `null` = دلوقتي — يعني وقت دوسة المدير.
            startedAtUtc: null,
            cancellationToken);

        if (result.IsFailure) return Result.Failure<RepairActionResponse>(result.Error);

        /*
          ⚠️ **السجل بيتكتب حتى لو الانتقال كان ساكن** (الأمر كان
          شغّال خلاص) — سلوك القديم، منقول زي ما هو.

          🔴 والسبب جوّه النص عن قصد: «تجاوز إداري» لوحدها مابتقولش
          ليه.
        */
        audit.Record(
            AuditActions.RepairStarted, "repair",
            command.Id, result.Value!.PublicCode,
            "تجاوز إداري — بدء نيابةً عن الفني: " + reason);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new RepairActionResponse("اتسجّل كتجاوز إداري"));
    }
}
