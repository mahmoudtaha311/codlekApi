using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Enums;
using Codlek.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Codlek.Infrastructure.Repositories;

/// <inheritdoc cref="IRackFeedRepository"/>
public sealed class RackFeedRepository(AppDbContext db) : IRackFeedRepository
{
    public async Task<IReadOnlyList<RosterFeedRow>> RepairRosterAsync(
        Guid tenantId, CancellationToken ct = default) =>
        await db.Technicians.AsNoTracking()

            // 🔴 التقييد بشركة المحطة هو الحاجز — من غيره أي راكة
            //    بتقرا فنيي كل الشركات على نفس السيرفر.
            .Where(t => t.TenantId == tenantId && t.IsActive && t.CanRepair)

            .OrderBy(t => t.DisplayName)

            // ⚠️ فاصل التعادل إجباري: فنيين بنفس الاسم المعروض
            //    بيتقلبوا بين الطلبات من غيره.
            .ThenBy(t => t.Id)

            .Select(t => new RosterFeedRow
            {
                Id = t.Id,
                Code = t.Code,
                DisplayName = t.DisplayName,
            })
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ContainerFeedRow>> ContainersAsync(
        Guid tenantId, CancellationToken ct = default) =>
        await db.Containers.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.IsActive)

            // ⚠️ ترتيب المالك الأول، وبعده الأحدث — نفس ترتيب الشاشة،
            //    عشان الفني يلاقي الحاوية في نفس المكان على الاتنين.
            .OrderBy(c => c.SortOrder)
            .ThenByDescending(c => c.CreatedAtUtc)
            .ThenBy(c => c.Id)

            .Select(c => new ContainerFeedRow
            {
                Id = c.Id,
                Code = c.Code,
                Name = c.Name,
                DeviceCount = db.Devices.Count(d => d.ContainerId == c.Id),
            })
            .ToListAsync(ct);

    public async Task<IReadOnlyList<AssignedRepairFeedRow>> AssignedRepairsAsync(
        Guid tenantId, DateTime? sinceUtc, int take, CancellationToken ct = default)
    {
        var query = db.RepairWorkItems.AsNoTracking()
            .Where(r => r.TenantId == tenantId

                     /*
                       ⚠️ **المفتوحة بس.**

                       الأمر اللي خلص أو اتلغى مالوش أي شغل على راكة،
                       وسحبه كان معناه إن كل راكة بتحمّل تاريخ الورشة
                       كله مع الوقت.
                     */
                     && (r.Status == RepairStatus.New
                      || r.Status == RepairStatus.WaitingForRepair
                      || r.Status == RepairStatus.InProgress));

        /*
          🔴 **والمقارنة `>` صارمة.**

          الراكة بتبعت آخر وقت **شافته**، فـ`>=` كان بيرجّع آخر صف
          في كل سحبة للأبد — الصف بيبان كأنه بيتغيّر وهو ساكن.
        */
        if (sinceUtc is { } floor) query = query.Where(r => r.UpdatedAtUtc > floor);

        return await query
            .OrderBy(r => r.UpdatedAtUtc)

            /*
              🔴 **وفاصل التعادل ده مش موجود في القديم — وده فرق
              مقصود.**

              `StampRepairCursor` بتحط **نفس** الوقت على كل أمر
              اتعدّل في نفس الحفظة، فإسناد جماعي بيطلّع صفوف
              متساوية في `UpdatedAtUtc` بالظبط. من غير فاصل، ترتيبهم
              بين الطلبات مش مضمون — وصف بيقع على حدّ الصفحة ممكن
              يتكرّر أو يتخطّى حسب مزاج الخطة.
            */
            .ThenBy(r => r.Id)

            .Take(take)
            .Select(r => new AssignedRepairFeedRow
            {
                Id = r.Id,
                PublicCode = r.PublicCode,

                DeviceId = r.DeviceId,
                DeviceCode = r.Device != null ? r.Device.PublicCode : "",

                // ⚠️ الاسم التجاري لو موجود، وإلا الموديل الخام —
                //    نفس ترتيب الشاشات.
                DeviceName = r.Device == null
                    ? ""
                    : (r.Device.CommercialModelName ?? r.Device.LastKnownModel),

                // 🔴 الماركة الخام — كانت بتتشال خالص، والراكة مكانتش
                //    تعرف ماركة اللاب أصلاً.
                DeviceManufacturer =
                    r.Device != null ? r.Device.LastKnownManufacturer : "",

                Status = (int)r.Status,

                AssignedTechnicianId = r.AssignedTechnicianId,
                AssignedTechnicianName =
                    r.AssignedTechnician != null ? r.AssignedTechnician.DisplayName : "",

                OpenedByName = r.OpenedByName,
                OpenedAtUtc = r.OpenedAtUtc,

                FaultSummary = r.FaultSummary,

                Approval = (int)r.Approval,
                ApprovalNote = r.ApprovalNote,
                ApprovedByName = r.ApprovedByName,
                ApprovalDecidedAtUtc = r.ApprovalDecidedAtUtc,

                UpdatedAtUtc = r.UpdatedAtUtc,
            })
            .ToListAsync(ct);
    }
}
