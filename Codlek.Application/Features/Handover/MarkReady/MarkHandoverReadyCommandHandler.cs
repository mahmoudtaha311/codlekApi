using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Handover;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Handover;
using MediatR;

namespace Codlek.Application.Features.Handover.MarkReady;

public sealed class MarkHandoverReadyCommandHandler(
    IHandoverRepository handover,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    ICurrentUser me)
    : IRequestHandler<MarkHandoverReadyCommand, Result<HandoverReadyResult>>
{
    public async Task<Result<HandoverReadyResult>> Handle(
        MarkHandoverReadyCommand command, CancellationToken cancellationToken)
    {
        // ⚠️ المعرّف الفاضي بيتشال بدل ما يرجّع غلط — الواجهة بتبعت
        // القايمة زي ما هي.
        var ids = (command.DeviceIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
            return Result.Failure<HandoverReadyResult>(HandoverErrors.NothingSelected);

        if (ids.Count > HandoverPolicy.BatchLimit)
            return Result.Failure<HandoverReadyResult>(HandoverErrors.BatchTooBigForReview);

        /*
          🔴 **شيل العلامة مالوش شروط. حطّها ليها شروط.**

          الرجوع عن غلطة لازم يفضل ممكن دايماً: لاب اتعلّم جاهز
          وبعدين اتفتحله أمر صيانة مبقاش «مؤهّل» — ولو منعنا شيل
          العلامة عنه، بيفضل معلّم غلط لحد ما الصيانة تخلص.
        */
        if (!command.Ready) return await ClearAsync(ids, cancellationToken);

        /*
          🔴 **الأهلية شرط للتعليم — والرسالة بتقول أنهي لاب
          بالكود.**

          «فيه لاب مش مؤهّل» بتخلّي اللي بيراجع يلغي الدفعة كلها وهو
          مش عارف ليه.
        */
        var eligible = await handover.EligibleIdsAsync(me.TenantId, ids, cancellationToken);

        if (eligible.Count != ids.Count)
        {
            var blocked = await handover.CodesAsync(
                me.TenantId,
                ids.Except(eligible).ToList(),
                HandoverPolicy.BlockedCodesShown,
                cancellationToken);

            return Result.Failure<HandoverReadyResult>(
                HandoverErrors.NotEligibleForReview(blocked));
        }

        // ⚠️ المعلّم خلاص مابيتلمسش — الرقم بيوصف الكتابة مش الطلب.
        var rows = await handover.TrackedForReviewAsync(
            me.TenantId, ids, markedOnly: false, cancellationToken);

        // ⚠️ وقت واحد للدفعة كلها: لفّة بتقرا `UtcNow` كل مرة بتدّي
        // أوقات مختلفة لنفس المراجعة.
        var stamp = DateTime.UtcNow;

        foreach (var device in rows)
        {
            device.ReadyForHandoverAtUtc = stamp;

            // 🔴 ومين قاله — العلم لوحده مش حكم، هو حكم **حد
            // معيّن**.
            device.ReadyByUserId = me.Id;
            device.ReadyByName = me.DisplayName;
        }

        if (rows.Count > 0)
        {
            audit.Record(
                AuditActions.DevicesMarkedReady, "Device", null, "",
                $"اتعلّم {rows.Count} لاب «جاهز للتسليم»");

            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(new HandoverReadyResult(rows.Count, true));
    }

    private async Task<Result<HandoverReadyResult>> ClearAsync(
        List<Guid> ids, CancellationToken ct)
    {
        var rows = await handover.TrackedForReviewAsync(
            me.TenantId, ids, markedOnly: true, ct);

        foreach (var device in rows)
        {
            device.ReadyForHandoverAtUtc = null;
            device.ReadyByUserId = null;
            device.ReadyByName = null;
        }

        if (rows.Count > 0)
        {
            audit.Record(
                AuditActions.DevicesMarkedReady, "Device", null, "",
                $"اتشالت علامة «جاهز» عن {rows.Count} لاب");

            await unitOfWork.SaveChangesAsync(ct);
        }

        return Result.Success(new HandoverReadyResult(rows.Count, false));
    }
}
