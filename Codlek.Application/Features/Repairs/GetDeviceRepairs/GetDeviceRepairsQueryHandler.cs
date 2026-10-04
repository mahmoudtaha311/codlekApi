using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Repairs;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Devices;
using Codlek.Core.Repairs;
using MediatR;

namespace Codlek.Application.Features.Repairs.GetDeviceRepairs;

/// <summary>
/// تاريخ صيانة اللاب — <b>من غير تصفيح ومن غير فلاتر</b>.
///
/// <para>⚠️ لاب عنده ٥٠ أمر بيرجّعهم كلهم. وده مقبول: ده تبويب في
/// صفحة جهاز واحد، والرقم مابيكبرش بعدد الأجهزة.</para>
/// </summary>
public sealed class GetDeviceRepairsQueryHandler(
    IRepairRepository repairs,
    ICurrentUser me)
    : IRequestHandler<GetDeviceRepairsQuery, Result<IReadOnlyList<RepairListItem>>>
{
    public async Task<Result<IReadOnlyList<RepairListItem>>> Handle(
        GetDeviceRepairsQuery query, CancellationToken cancellationToken)
    {
        /*
          🔴 **الفحص ده مترشّح بالشركة — وده اللي بيخلّيه ٤٠٤ مش
          ٤٠٣.**

          جهاز شركة تانية لازم يرجّع «مش موجود»: الـ<c>403</c> بيقول
          «موجود بس مش من حقك»، وده بيأكّد لحد بره إن المعرّف ده
          حقيقي.
        */
        if (!await repairs.DeviceExistsAsync(me.TenantId, query.DeviceId, cancellationToken))
            return Result.Failure<IReadOnlyList<RepairListItem>>(RepairErrors.DeviceNotFound);

        var rows = await repairs.ListForDeviceAsync(
            me.TenantId, query.DeviceId, cancellationToken);

        var now = DateTime.UtcNow;

        var items = rows.Select(r => new RepairListItem(
            Id: r.Id,
            PublicCode: r.PublicCode,
            DeviceId: r.DeviceId,
            DeviceCode: r.DeviceCode,
            DeviceName: DeviceNaming.Display(
                r.Manufacturer, r.CommercialModelName, r.RawModel),
            Status: r.Status.ToString(),
            StatusText: RepairStatusRules.Text(r.Status),
            FaultSummary: r.FaultSummary,
            AssignedTechnicianId: r.AssignedTechnicianId,
            AssignedTechnicianName: r.AssignedTechnicianName,
            OpenedAtUtc: r.OpenedAtUtc,
            StartedAtUtc: r.StartedAtUtc,
            CompletedAtUtc: r.CompletedAtUtc,
            DurationMs: RepairTiming.DurationMs(r.StartedAtUtc, r.CompletedAtUtc),
            LocationName: r.LocationName,
            IssueCount: r.IssueCount,
            PartCount: r.PartCount,
            OpenAgeHours: RepairTiming.OpenAgeHours(r.OpenedAtUtc, r.CompletedAtUtc, now),
            RepairAgeHours: RepairTiming.RepairAgeHours(r.StartedAtUtc, r.CompletedAtUtc, now),
            Approval: (int)r.Approval,
            ApprovalText: RepairStatusRules.ApprovalText(r.Approval))).ToList();

        return Result.Success<IReadOnlyList<RepairListItem>>(items);
    }
}
