using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Handover;
using Codlek.Application.Contracts.Workflow;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Enums;
using Codlek.Core.Handover;
using MediatR;

namespace Codlek.Application.Features.Handover.ExecuteHandover;

/// <summary>
/// بينفّذ التسليم.
///
/// <para>🔴 <b>الكل أو ولا واحد.</b> لو ٣٩ من ٤٠ نجحوا، الشحنة
/// ناقصة والسجل بيقول إنها تمّت — والحفظ بيحصل <b>بعد</b> ما كلهم
/// ينجحوا.</para>
/// </summary>
public sealed class ExecuteHandoverCommandHandler(
    IHandoverRepository handover,
    IDeviceWorkflowRecorder workflow,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    ICurrentUser me)
    : IRequestHandler<ExecuteHandoverCommand, Result<HandoverResult>>
{
    public async Task<Result<HandoverResult>> Handle(
        ExecuteHandoverCommand command, CancellationToken cancellationToken)
    {
        var ids = (command.DeviceIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
            return Result.Failure<HandoverResult>(HandoverErrors.NothingSelected);

        // ⚠️ سقف على الدفعة: الحركة بتتنفّذ جهاز جهاز جوّه معاملة
        // واحدة، ودفعة ضخمة بتقفل صفوف كتير وقت طويل.
        if (ids.Count > HandoverPolicy.BatchLimit)
            return Result.Failure<HandoverResult>(HandoverErrors.BatchTooBigForHandover);

        string receiver = (command.ReceivedByName ?? "").Trim();

        // 🔴 اسم المستلم هو اللي بيجاوب «هاني سلّم لمين الشهر ده».
        if (receiver.Length < HandoverPolicy.MinReceiverName)
            return Result.Failure<HandoverResult>(HandoverErrors.ReceiverRequired);

        var destination = await handover.FindDestinationAsync(
            me.TenantId, command.DestinationId, cancellationToken);

        if (destination is null)
            return Result.Failure<HandoverResult>(HandoverErrors.DestinationNotFound);

        if (!destination.IsActive)
        {
            return Result.Failure<HandoverResult>(
                HandoverErrors.DestinationSuspended(destination.Name));
        }

        /*
          🔴 **قاعدة الأهلية بتتفرض هنا كمان، مش في القايمة بس.**

          الخدمة اللي بتكتب الحركات مافيهاش أي تحقّق من صحة
          الانتقال، والقايمة اللي في الشاشة مجرد اقتراح — اللي يبعت
          معرّفات بنفسه بيعدّي منها.

          ⚠️ وكانت القاعدة متكتوبة **مرتين بنصّين مختلفين**، وواحدة
          منهم كانت ناقصة الصيانة المفتوحة والفني الحائز. بقت مكان
          واحد في المستودع.
        */
        var eligible = await handover.EligibleIdsAsync(me.TenantId, ids, cancellationToken);

        if (eligible.Count != ids.Count)
        {
            var blocked = await handover.CodesAsync(
                me.TenantId,
                ids.Except(eligible).ToList(),
                HandoverPolicy.BlockedCodesShown,
                cancellationToken);

            return Result.Failure<HandoverResult>(
                HandoverErrors.NotEligibleForHandover(blocked));
        }

        /*
          🔴 **والمراجعة شرط — بمخرج مسجّل.**

          «عدّى الفحص» حكم آلي، و«جاهز يمشي» حكم بني آدم (اتنضّف؟
          اتغلّف؟). الأصل إن اللي يتسلّم يكون حد راجعه.

          ⚠️ **بس الباب المقفول بالكامل بيتحايل عليه.** حالة مستعجلة
          والمراجعة مقفولة معناها إن حد هيعلّم «جاهز» على السريع عشان
          يعدّي — وساعتها المراجعة تبقى ختم مالوش معنى وإحنا مش
          عارفين إن ده حصل. فالباب مفتوح **بشرط إنه يتكتب**: السبب
          إجباري وبيتسجّل في الحركة وفي سجل التدقيق. الفرق بين
          استثناء وخرق إن الاستثناء متسجّل.
        */
        string overrideReason = (command.OverrideReason ?? "").Trim();

        var unreviewed = await handover.UnreviewedCodesAsync(
            me.TenantId, ids, HandoverPolicy.BlockedCodesShown, cancellationToken);

        if (unreviewed.Count > 0 && overrideReason.Length < HandoverPolicy.MinOverrideReason)
            return Result.Failure<HandoverResult>(HandoverErrors.NotReviewed(unreviewed));

        /*
          ⚠️ **وفحص الوجود بعد كل ده.**

          الأهلية بترشّح على النشط، فلاب موجود بس «مستبعد» بيطلع في
          رسالة الأهلية. والفحص ده بيلقط المعرّف اللي <b>مش موجود
          خالص</b> — رسالة مختلفة عن قصد.
        */
        int found = await handover.CountExistingAsync(me.TenantId, ids, cancellationToken);

        if (found != ids.Count)
            return Result.Failure<HandoverResult>(HandoverErrors.DeviceMissing);

        var now = DateTime.UtcNow;

        string reason = (command.Reason ?? "").Trim();

        if (reason.Length == 0) reason = $"تسليم لـ{destination.Name}";

        /*
          ⚠️ **الاستثناء بيتكتب في <u>سبب الحركة</u> نفسه، مش في
          الملاحظات.**

          الملاحظات اختيارية وبتتفلتر في العرض؛ والسبب بيبان في سجل
          التسليم جمب كل صف — فاللي بيراجع السجل بعد شهر يشوف إن ده
          تسليم استثنائي من غير ما يفتح حاجة.
        */
        if (unreviewed.Count > 0)
            reason = $"⚠️ تسليم من غير مراجعة — {overrideReason} · {reason}";

        bool toSales = destination.Kind == LocationKind.Sales;

        string notes = (command.Notes ?? "").Trim();

        var moved = await workflow.RecordManyAsync(me.TenantId, ids, id => new WorkflowMove
        {
            DeviceId = id,

            // ⚠️ التسليم للمبيعات حدث ليه نوع مخصّص؛ الباقي نقل
            // مكان. النوعين الاتنين كانوا معرّفين من زمان ومحدش
            // بيطلّعهم.
            EventType = toSales
                ? DeviceWorkflowEventType.DispatchedToSales
                : DeviceWorkflowEventType.LocationMoved,

            Actor = WorkflowActor.User(me.Id, me.DisplayName),

            /*
              🔴 **المرحلة مابتتغيّرش بقصد.**

              مفيش قيمة مناسبة: «جاهز» بتوصف الصلاحية مش المكان
              واللاب أصلاً جاهز قبل التسليم، و«نقطة بيع» غلط لمخزن.
              فالمكان هو اللي بيجاوب «هو فين»، وده مايحتاجش هجرة ولا
              مزامنة أرقام مع الراكة.

              ⚠️ إلا المبيعات: «مع المبيعات» موجودة وبتوصف الحالة دي
              بالظبط.
            */
            ToStage = toSales ? DeviceOperationalStage.WithSales : null,

            ToLocationId = destination.Id,

            // 🔴 اللاب مبقاش مع فني — هو في المخزن دلوقتي.
            ClearsHolder = true,

            // ⚠️ وقت واحد للدفعة كلها — ده اللي بيخلّي السجل يعرضها
            // كشحنة واحدة.
            OccurredAtUtc = now,

            ReceivedByName = receiver,
            Reason = reason,
            Notes = notes,
        }, cancellationToken);

        // ⚠️ فشل أي حركة بيوقّف الدفعة كلها — ومفيش حفظ حصل.
        if (!moved.Ok)
            return Result.Failure<HandoverResult>(HandoverErrors.MoveRefused(moved.Error!));

        string summary = $"اتسلّم {moved.Moved} لاب لـ«{destination.Name}» — استلمها {receiver}";

        if (unreviewed.Count > 0)
            summary += $" · من غير مراجعة ({unreviewed.Count} لاب): {overrideReason}";

        audit.Record(AuditActions.DevicesHandedOver, "Device", null, "", summary);

        // 🔴 حفظة واحدة: الحركات كلها وسجل التدقيق مع بعض أو ولا
        // واحد.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new HandoverResult(
            moved.Moved, destination.Name, receiver, now));
    }
}
