using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Repairs;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Codlek.Application.Features.Repairs.RejectRepair;

/// <summary>
/// رفض المحاسب.
///
/// <para>🔴 <b>السبب إجباري.</b> الفني هيشوف السبب، ورفض من غير سبب
/// بيخلّيه يبعت <b>نفس الطلب تاني</b> — فالمحاسب بيرفض والفني بيعيد
/// والاتنين بيلفّوا.</para>
/// </summary>
public sealed class RejectRepairCommandHandler(
    IRepairRepository repairs,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    ICurrentUser me,
    ILogger<RejectRepairCommandHandler> log)
    : IRequestHandler<RejectRepairCommand, Result<RepairActionResponse>>
{
    /// <summary>أقصر سبب مقبول — نفس رقم القديم.</summary>
    private const int MinNoteLength = 3;

    public async Task<Result<RepairActionResponse>> Handle(
        RejectRepairCommand command, CancellationToken cancellationToken)
    {
        string note = (command.Note ?? "").Trim();

        /*
          ⚠️ **السبب بيتفحص قبل البحث عن الصف — زي القديم بالحرف.**

          يعني رفض من غير سبب على أمر مش موجود بيرجّع «اكتب سبب
          الرفض» مش «مش موجود». وده مقصود: الرسالة بتقول للمحاسب
          يعمل إيه، والمعرّف الغلط مش مشكلته.
        */
        if (note.Length < MinNoteLength)
            return Result.Failure<RepairActionResponse>(RepairErrors.RejectionReasonRequired);

        var item = await repairs.FindAsync(me.TenantId, command.Id, cancellationToken);

        if (item is null) return Result.Failure<RepairActionResponse>(RepairErrors.NotFound);

        // 🔴 نفس حاجز «اتقرر فيه خلاص» — محاسبين على شاشتين.
        if (item.Approval != RepairApproval.Pending)
        {
            log.LogWarning(
                "رفض مرفوض: {PublicCode} اتقرر فيه خلاص — {Approval}.",
                item.PublicCode, item.Approval);

            return Result.Failure<RepairActionResponse>(
                RepairErrors.AlreadyDecided(item.Approval));
        }

        item.Approval = RepairApproval.Rejected;
        item.ApprovedByUserId = me.Id;
        item.ApprovedByName = me.DisplayName;
        item.ApprovalDecidedAtUtc = DateTime.UtcNow;
        item.ApprovalNote = note;

        /*
          ⚠️ **والسبب جوّه نص السجل كمان.**

          `ApprovalNote` بيتعرض للفني، ونص السجل بيتعرض للمدير في
          صفحة الإجراءات — والاتنين محتاجين السبب.
        */
        audit.Record(
            AuditActions.RepairRejected, "repair",
            command.Id, item.PublicCode, $"رفض أمر صيانة — {note}");

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new RepairActionResponse("اترفض."));
    }
}
