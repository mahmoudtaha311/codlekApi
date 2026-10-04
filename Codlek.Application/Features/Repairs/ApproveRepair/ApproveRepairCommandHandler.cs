using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Repairs;
using Codlek.Application.Contracts.Workflow;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Codlek.Application.Features.Repairs.ApproveRepair;

/// <summary>
/// موافقة المحاسب — <b>ومعاها تغيير الفني في نفس الخطوة</b>.
///
/// <para>🔴 <b>نداء واحد مش اتنين.</b> صاحب الشغل طلب إن المحاسب
/// يوافق <b>ويغيّر الفني</b> مع بعض. ولو اتعملت نداءين، انهيار بينهم
/// بيسيب الأمر <b>موافَق عليه والفني غلط</b> — وده أسوأ من إن
/// الاتنين مايحصلوش.</para>
/// </summary>
public sealed class ApproveRepairCommandHandler(
    IRepairRepository repairs,
    IRepairTransitions transitions,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    ICurrentUser me,
    ILogger<ApproveRepairCommandHandler> log)
    : IRequestHandler<ApproveRepairCommand, Result<RepairActionResponse>>
{
    public async Task<Result<RepairActionResponse>> Handle(
        ApproveRepairCommand command, CancellationToken cancellationToken)
    {
        var item = await repairs.FindAsync(me.TenantId, command.Id, cancellationToken);

        if (item is null) return Result.Failure<RepairActionResponse>(RepairErrors.NotFound);

        /*
          🔴 **إعادة التأكد من «لسه معلّق» — جوّه نفس المعاملة.**

          محاسبين على شاشتين بيقدروا يدوسوا في نفس اللحظة. ومن غير
          السطر ده، التاني بيدهس قرار الأول ويكتب اسمه مكانه — والسجل
          بيقول إن الموافقة اتخدت مرتين من ناس مختلفة.
        */
        if (item.Approval != RepairApproval.Pending)
        {
            log.LogWarning(
                "موافقة مرفوضة: {PublicCode} اتقرر فيه خلاص — {Approval}.",
                item.PublicCode, item.Approval);

            return Result.Failure<RepairActionResponse>(
                RepairErrors.AlreadyDecided(item.Approval));
        }

        /*
          🔴 **تغيير الفني الأول — عشان الفشل يسيب الأمر معلّق.**

          لو الموافقة اتكتبت قبل الإسناد وفشل الإسناد، الأمر بيبقى
          **موافَق عليه ومسنود غلط** — والفني اللي مش من حقه بيبدأ
          شغل بسلطة موافقة حقيقية.
        */
        if (command.TechnicianId is { } technicianId
            && technicianId != item.AssignedTechnicianId)
        {
            var moved = await transitions.AssignAsync(
                me.TenantId,
                command.Id,
                technicianId,
                WorkflowActor.User(me.Id, me.DisplayName),

                // 🔴 المحاسب بيعدّي القيد — والتعدية بتتسجّل على الأمر.
                allowOutsideBrand: true,
                cancellationToken);

            if (moved.IsFailure)
                return Result.Failure<RepairActionResponse>(moved.Error);
        }

        item.Approval = RepairApproval.Approved;
        item.ApprovedByUserId = me.Id;
        item.ApprovedByName = me.DisplayName;
        item.ApprovalDecidedAtUtc = DateTime.UtcNow;

        // ⚠️ الملاحظة اختيارية على الموافقة — بخلاف الرفض.
        item.ApprovalNote = (command.Note ?? "").Trim();

        /*
          ⚠️ **و<c>StartedWithoutApproval</c> مابيتفضّاش.**

          العلم ده **تاريخ** مش حالة: هو بيقول إن راكة اشتغلت على
          الأمر وهو لسه معلّق. والموافقة بعدين مابتمسحش الحقيقة دي —
          اللي بيقرا السجل محتاج يعرف إن الشغل اتعمل قبل القرار.
        */
        audit.Record(
            AuditActions.RepairApproved, "repair",
            command.Id, item.PublicCode, "موافقة على أمر صيانة");

        // 🔴 حفظة واحدة: الموافقة والإسناد والتعدية والحركة والسجل.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new RepairActionResponse("اتوافق عليه."));
    }
}
