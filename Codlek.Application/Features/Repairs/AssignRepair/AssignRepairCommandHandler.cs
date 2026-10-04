using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Repairs;
using Codlek.Application.Contracts.Workflow;
using Codlek.Application.Interfaces;
using MediatR;

namespace Codlek.Application.Features.Repairs.AssignRepair;

/// <summary>
/// إسناد أمر لفني.
///
/// <para>🔴 <b>الحاجز هو السياسة، والتعدية هي الخاصية.</b> السياسة على
/// النقطة <c>ManagerOrAbove</c> — دي اللي بتقول «مين يقدر يسند». ومين
/// يقدر يسند <b>برّه الماركة</b> بييجي من
/// <c>ICurrentUser.IsRepairApprover</c>.</para>
///
/// <para>⚠️ ودمج الاتنين كان بيخلّي المدير يعدّي القيد وهو مش من
/// حقه.</para>
/// </summary>
public sealed class AssignRepairCommandHandler(
    IRepairTransitions transitions,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    ICurrentUser me)
    : IRequestHandler<AssignRepairCommand, Result<RepairActionResponse>>
{
    public async Task<Result<RepairActionResponse>> Handle(
        AssignRepairCommand command, CancellationToken cancellationToken)
    {
        var result = await transitions.AssignAsync(
            me.TenantId,
            command.Id,
            command.TechnicianId,
            WorkflowActor.User(me.Id, me.DisplayName),

            // 🔴 المحاسب والمالك بس — راجع `IsRepairApprover`.
            allowOutsideBrand: me.IsRepairApprover,
            cancellationToken);

        if (result.IsFailure) return Result.Failure<RepairActionResponse>(result.Error);

        /*
          ⚠️ **السجل بيتكتب من غير شرط — حتى لو الفني هو نفسه.**

          ده سلوك القديم بالحرف: السجل بيقول «إسناد أمر صيانة» حتى لو
          مفيش حاجة اتغيّرت. منقول زي ما هو عشان السجل المشترك بين
          اللوحتين مايفترقش.
        */
        audit.Record(
            AuditActions.RepairAssigned, "repair",
            command.Id, result.Value!.PublicCode, "إسناد أمر صيانة");

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new RepairActionResponse("اتسند"));
    }
}
