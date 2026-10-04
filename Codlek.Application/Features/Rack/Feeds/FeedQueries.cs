using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Rack;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Repairs;
using Codlek.Core.Sync;
using MediatR;

namespace Codlek.Application.Features.Rack.Feeds;

/// <summary>
/// ⚠️ <b>الشركة من المفتاح المتحقق</b> — الطلب مالوش شركة.
/// </summary>
public sealed record RepairRosterQuery(Guid TenantId)
    : IRequest<Result<RepairRosterResponse>>;

public sealed record RackContainersQuery(Guid TenantId)
    : IRequest<Result<ContainersResponse>>;

/// <param name="Since">
/// 🔴 <b>النص الخام زي ما الراكة بعتته.</b> التحليل جوّه المعالج
/// عشان «مش مفهوم» يبقى <c>null</c> مش <c>400</c> — ورفضه كان
/// بيوقّف السحب على الراكة اللي علامتها اتخربت، وهي بالظبط اللي
/// محتاجة تسحب من الأول.
/// </param>
public sealed record AssignedRepairsQuery(Guid TenantId, string? Since)
    : IRequest<Result<AssignedRepairsResponse>>;

/// <inheritdoc cref="RepairRosterQuery"/>
public sealed class RepairRosterQueryHandler(IRackFeedRepository feeds)
    : IRequestHandler<RepairRosterQuery, Result<RepairRosterResponse>>
{
    public async Task<Result<RepairRosterResponse>> Handle(
        RepairRosterQuery query, CancellationToken cancellationToken)
    {
        var rows = await feeds.RepairRosterAsync(query.TenantId, cancellationToken);

        return Result.Success(new RepairRosterResponse(
            Technicians: [.. rows.Select(r => new RepairTechnicianRow(
                Id: r.Id,
                Code: r.Code,
                DisplayName: r.DisplayName,

                /*
                  ⚠️ **ثابتين `true` عن قصد — والاستعلام هو اللي
                  بيفلتر.**

                  الراكة بتخزّن القيمة زي ما جت. لو يوم ما التغذية
                  رجّعت فنيين موقوفين بعلامة، الراكة القديمة هتحترمها
                  من غير تحديث — فالخانتين دول باب للمستقبل مش
                  حشو.
                */
                CanRepair: true,
                IsActive: true))],

            ServerTimeUtc: DateTime.UtcNow));
    }
}

/// <inheritdoc cref="RackContainersQuery"/>
public sealed class RackContainersQueryHandler(IRackFeedRepository feeds)
    : IRequestHandler<RackContainersQuery, Result<ContainersResponse>>
{
    public async Task<Result<ContainersResponse>> Handle(
        RackContainersQuery query, CancellationToken cancellationToken)
    {
        var rows = await feeds.ContainersAsync(query.TenantId, cancellationToken);

        return Result.Success(new ContainersResponse(
            Containers: [.. rows.Select(r => new ContainerRow(
                Id: r.Id,
                Code: r.Code,
                Name: r.Name,
                DeviceCount: r.DeviceCount))],

            ServerTimeUtc: DateTime.UtcNow));
    }
}

/// <inheritdoc cref="AssignedRepairsQuery"/>
public sealed class AssignedRepairsQueryHandler(IRackFeedRepository feeds)
    : IRequestHandler<AssignedRepairsQuery, Result<AssignedRepairsResponse>>
{
    public async Task<Result<AssignedRepairsResponse>> Handle(
        AssignedRepairsQuery query, CancellationToken cancellationToken)
    {
        var since = SinceCursor.Parse(query.Since);

        var rows = await feeds.AssignedRepairsAsync(
            query.TenantId, since, SinceCursor.Need, cancellationToken);

        bool hasMore = SinceCursor.HasMore(rows.Count);

        var page = hasMore ? rows.Take(SinceCursor.PageSize).ToList() : [.. rows];

        return Result.Success(new AssignedRepairsResponse(
            Items: [.. page.Select(r => new AssignedRepairRow(
                Id: r.Id,
                PublicCode: r.PublicCode,
                DeviceId: r.DeviceId,
                DeviceCode: r.DeviceCode,
                DeviceName: r.DeviceName,
                Status: r.Status,
                AssignedTechnicianId: r.AssignedTechnicianId,
                AssignedTechnicianName: r.AssignedTechnicianName,
                OpenedByName: r.OpenedByName,
                OpenedAtUtc: r.OpenedAtUtc,
                FaultSummary: r.FaultSummary,
                Approval: r.Approval,
                ApprovalNote: r.ApprovalNote,
                ApprovedByName: r.ApprovedByName,
                ApprovalDecidedAtUtc: r.ApprovalDecidedAtUtc,
                DeviceManufacturer: r.DeviceManufacturer,
                UpdatedAtUtc: r.UpdatedAtUtc))],

            /*
              🔴 **العلامة من آخر صف — مش من الساعة.**

              لو أخدناها من الساعة، صف اتكتب في نفس الجزء من الثانية
              بعد ما بنينا الرد كان هيتفوّت **للأبد** والراكة مش
              هتعرف إنها فوّتته.

              ⚠️ وعلى صفحة فاضية بترجع العلامة اللي جات
              (`null` لو مافيش) — **مش** ساعة السيرفر. ساعة السيرفر
              هنا كانت بتدّي لراكة جديدة علامة ماكسبتهاش وتخلّيها
              تتخطّى تاريخها كله.
            */
            HighWaterUtc: page.Count > 0 ? page[^1].UpdatedAtUtc : since,

            /*
              ⚠️ **والسيرفر بيقول صريح إنه بيطبّق الموافقة ولا لأ.**

              راكة جديدة على سيرفر قديم هتلاقي الخانة ناقصة،
              و«ناقصة» معناها «مش بيطبّق» — مش «كله معلّق».
            */
            ApprovalEnforced: RepairPolicy.ApprovalEnforced,

            HasMore: hasMore,
            ServerTimeUtc: DateTime.UtcNow));
    }
}
