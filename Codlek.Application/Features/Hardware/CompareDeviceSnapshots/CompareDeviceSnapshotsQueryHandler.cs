using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Hardware;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Hardware;
using MediatR;

namespace Codlek.Application.Features.Hardware.CompareDeviceSnapshots;

/// <summary>
/// بيبني جدول المقارنة.
///
/// <para>🔴 <b>الحكم كله في <see cref="SnapshotComparison"/> —
/// والمعالج ده بيرتّب ويحوّل وبس.</b> القواعد دي بتتحوّل لاتهام إن
/// موظف غيّر قطعة، فهي عايشة في طبقة نقية متجرّبة لوحدها من غير
/// قاعدة ولا HTTP.</para>
/// </summary>
public sealed class CompareDeviceSnapshotsQueryHandler(
    IHardwareRepository hardware,
    ICurrentUser me)
    : IRequestHandler<CompareDeviceSnapshotsQuery, Result<CompareResponse>>
{
    public async Task<Result<CompareResponse>> Handle(
        CompareDeviceSnapshotsQuery query, CancellationToken cancellationToken)
    {
        var device = await hardware.FindDeviceAsync(
            me.TenantId, query.DeviceId, cancellationToken);

        if (device is null)
            return Result.Failure<CompareResponse>(HardwareErrors.DeviceNotFound);

        /*
          ⚠️ **الترتيب ده منقول بالحرف: اللاب قبل المعرّفين.**

          يعني طلب من غير معرّفات على لاب مش موجود بيرجّع «الجهاز مش
          موجود» مش «لازم تحدّد الفحصين». وده أنفع للّي بيقرا: اللاب
          هو أول حاجة غلط.
        */
        if (query.Left is not { } leftId || query.Right is not { } rightId)
            return Result.Failure<CompareResponse>(HardwareErrors.CompareNeedsTwo);

        // ⚠️ مقارنة الفحص بنفسه بترجّع جدول كله «زي ما هي» — رد صحيح
        // تقنياً ومالوش أي معنى للّي بيقرا.
        if (leftId == rightId)
            return Result.Failure<CompareResponse>(HardwareErrors.CompareSameReport);

        /*
          🔴 **الحارس: تابع للشركة <u>وللجهاز ده</u>.**

          التقييد بالشركة لوحده بيخلّي حد يقارن جهازين مختلفين بمجرد
          تغيير الأرقام في الرابط — ويطلّع فروق عتاد مالهاش أي معنى،
          وهو في الحقيقة بيقرا سيريالات بضاعة مش شغله.
        */
        var a = await hardware.FindHeaderForDeviceAsync(
            me.TenantId, query.DeviceId, leftId, cancellationToken);

        var b = await hardware.FindHeaderForDeviceAsync(
            me.TenantId, query.DeviceId, rightId, cancellationToken);

        if (a is null || b is null)
            return Result.Failure<CompareResponse>(HardwareErrors.ReportNotOnDevice);

        /*
          🔴 **الأقدم على الشمال دايماً.**

          المقارنة اتجاهية — «اتضافت» و«اتشالت» بيتقلبوا لو الترتيب
          اتعكس، والمستخدم ممكن يختار الأحدث في الخانة الأولى من غير
          ما ياخد باله. فالنتيجة كانت بتقول «القطعة اتشالت» وهي
          **اتضافت**.
        */
        if (a.StartedAtUtc > b.StartedAtUtc) (a, b) = (b, a);

        var leftParts = await hardware.ComponentsAsync(me.TenantId, a.ReportId, cancellationToken);
        var rightParts = await hardware.ComponentsAsync(me.TenantId, b.ReportId, cancellationToken);

        var diffs = SnapshotComparison.Compare(
            leftParts, rightParts, a.IsPartial, b.IsPartial);

        var steps = await hardware.StepsAsync(
            me.TenantId, a.ReportId, b.ReportId, cancellationToken);

        string leftRack = await hardware.RackCodeAsync(
            me.TenantId, a.SourceRackId, cancellationToken);

        string rightRack = await hardware.RackCodeAsync(
            me.TenantId, b.SourceRackId, cancellationToken);

        return Result.Success(new CompareResponse(
            DeviceId: device.Id,
            DevicePublicCode: device.PublicCode,

            // ⚠️ العدد من القايمة المحمّلة — بيوصف اللي اتقارن فعلاً.
            Left: HardwareMapping.Side(a, leftRack, leftParts.Count),
            Right: HardwareMapping.Side(b, rightRack, rightParts.Count),

            Summary: HardwareMapping.Summarize(diffs),

            // 🔴 تغيّر مرساة النظام أو اللوحة الأم بيتعرض بوضوح —
            // **من غير** ما نقول إنه جهاز تاني.
            MajorWarning: SnapshotComparison.MajorIdentityWarning(diffs),

            EitherPartial: a.IsPartial || b.IsPartial,

            Components: diffs.Select(HardwareMapping.Diff).ToList(),
            Steps: HardwareMapping.Steps(steps, a.ReportId, b.ReportId)));
    }
}
