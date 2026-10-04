using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Repairs;
using Codlek.Application.Contracts.Workflow;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Codlek.Core.Repairs;
using Codlek.Core.Text;
using MediatR;

namespace Codlek.Application.Features.Repairs.OpenRepair;

/// <summary>
/// بيفتح أمر صيانة لجهاز.
///
/// <para>⚠️ <b>مفيش جهاز بيتعمل هنا.</b> الأمر بيتعلّق بجهاز موجود
/// بمعرّفه الدائم؛ ولو الجهاز مش موجود، ده عطل مش سبب لإنشاء
/// واحد.</para>
/// </summary>
public sealed class OpenRepairCommandHandler(
    IRepairRepository repairs,
    IRepairTransitions transitions,
    IDeviceWorkflowRecorder workflow,
    ITenantCounters counters,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    ICurrentUser me)
    : IRequestHandler<OpenRepairCommand, Result<OpenedRepairResponse>>
{
    public async Task<Result<OpenedRepairResponse>> Handle(
        OpenRepairCommand command, CancellationToken cancellationToken)
    {
        var device = await repairs.FindDeviceAsync(
            me.TenantId, command.DeviceId, cancellationToken);

        if (device is null)
            return Result.Failure<OpenedRepairResponse>(RepairErrors.DeviceNotFound);

        // 🔴 الجهاز المدموج مش لاب مستقل — أمره بيتفتح على الكانوني.
        if (device.Status == DeviceLifecycleStatus.Merged)
            return Result.Failure<OpenedRepairResponse>(RepairErrors.DeviceMerged);

        // ⚠️ والفحص الممسوح مش موجود — أمر متعلّق بفحص اتمسح معناه
        // سلسلة مقطوعة.
        if (command.SourceReportId is { } reportId
            && !await repairs.ReportExistsAsync(me.TenantId, reportId, cancellationToken))
        {
            return Result.Failure<OpenedRepairResponse>(RepairErrors.SourceReportNotFound);
        }

        Technician? assigned = null;

        if (command.AssignTechnicianId is { } technicianId)
        {
            // ⚠️ إسناد شغل جديد، فالقيد بيتطبّق.
            var resolved = await transitions.ResolveTechnicianAsync(
                me.TenantId, technicianId, device.Id,
                RepairPolicy.BrandCheck.Enforce, cancellationToken);

            if (resolved.IsFailure)
                return Result.Failure<OpenedRepairResponse>(resolved.Error);

            assigned = resolved.Value;
        }

        var now = DateTime.UtcNow;

        var item = new RepairWorkItem
        {
            Id = Guid.NewGuid(),
            TenantId = me.TenantId,
            DeviceId = device.Id,
            SourceReportId = command.SourceReportId,

            /*
              🔴 **الإسناد هو اللي بيعمل المسؤولية.**

              مسنود من أول لحظة = «بانتظار الصيانة»؛ من غير إسناد =
              «جديدة» ومعروضة على كل فني مطابق. والفرق ده هو الحاجة
              الوحيدة اللي بتدّي «مين المسؤول دلوقتي» جواب.
            */
            Status = RepairOpening.StatusAtOpen(assigned is not null),

            // 🔴 والسطر ده هو اللي بيشغّل ميزة الموافقة كلها.
            Approval = RepairPolicy.NewOrderApproval,

            AssignedTechnicianId = assigned?.Id,
            ClaimedAtUtc = assigned is null ? null : now,

            // ⚠️ الفاعل لقطة: لو المدير اتشال، الأمر لازم يفضل بيقول
            // مين فتحه.
            OpenedByActorType = "User",
            OpenedByUserId = me.Id,
            OpenedByName = TextClip.To(me.DisplayName, TextClip.Lengths.PersonName),
            OpenedAtUtc = now,

            // ⚠️ ومفيش فحص طول على وصف العطل — العمود `nvarchar(max)`،
            // وده سلوك القديم.
            FaultSummary = command.FaultSummary ?? "",

            RequiredSpecialty = (TechnicianSpecialty)(command.RequiredSpecialty ?? 0),
        };

        /*
          🔴 **الكود من العدّاد الذرّي، مش من `MAX+1`.**

          راكتين بتفتحوا أمر في نفس اللحظة بياخدوا رقمين مختلفين —
          راجع `TenantCounters`.
        */
        item.PublicCode = RepairCode.Format(
            await counters.NextAsync(me.TenantId, RepairCode.CounterName, cancellationToken));

        /*
          ⚠️ **نسخة الويب من نص البحث — فيها كود الجهاز.**

          ومسار المزامنة بيكتب نسخة تانية من غيره. الاتنين موجودين في
          بيانات الإنتاج، واتسابوا منفصلين بالاسم عن قصد.
        */
        item.SearchText = RepairSearchText.FromWeb(
            item.PublicCode, device.PublicCode, item.FaultSummary, item.OpenedByName);

        repairs.Add(item);

        /*
          ⚠️ **وناتج الحركة بيتجاهل عن قصد.**

          ده سلوك القديم: فتح الأمر نجح، ورفض حركة الجهاز (موقع مش
          موجود مثلاً) مش سبب إن الأمر مايتفتحش. والأمر هو الحاجة
          اللي الفني محتاجها.
        */
        await workflow.RecordAsync(me.TenantId, new WorkflowMove
        {
            DeviceId = device.Id,
            EventType = DeviceWorkflowEventType.SentToRepair,
            Actor = WorkflowActor.User(me.Id, me.DisplayName),
            ToStage = DeviceOperationalStage.NeedsRepair,
            ToTechnicianId = assigned?.Id,
            RepairWorkItemId = item.Id,
            Reason = TextClip.To(item.FaultSummary, TextClip.Lengths.Reason),
        }, cancellationToken);

        audit.Record(
            AuditActions.RepairCreated, "repair",
            item.Id, item.PublicCode, $"أمر صيانة {item.PublicCode}");

        // 🔴 حفظة واحدة: الأمر والحركة والسجل بينزلوا مع بعض أو ولا
        // واحد.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new OpenedRepairResponse(item.Id, item.PublicCode));
    }
}
