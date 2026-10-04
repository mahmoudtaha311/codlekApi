using Codlek.Application.Contracts.Containers;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Codlek.Core.Text;
using Codlek.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Codlek.Infrastructure.Repositories;

public sealed class ContainerRepository(AppDbContext db) : IContainerRepository
{
    public async Task<IReadOnlyList<ContainerListItem>> ListAsync(
        Guid tenantId, string? search, CancellationToken ct = default)
    {
        var query = db.Containers.AsNoTracking().Where(c => c.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            /*
              ⚠️ **البحث على العمود المطبَّع، مش على الرمز الخام.**

              الفني بيكتب الرمز بشرطات وحالة حروف مختلفة في كل مرة:
              `SH-2024/01` و`sh 2024 01`. والبحث على الخام كان بيرجّع
              فاضي لنفس الحاوية.
            */
            string key = ContainerCode.Normalize(search);

            // ⚠️ ولو اللي اتكتب مفيهوش ولا حرف ولا رقم، بنتجاهل
            // البحث خالص بدل ما نرجّع فاضي — نفس القديم.
            if (key.Length > 0)
                query = query.Where(c => c.NormalizedCode.Contains(key));
        }

        return await query
            .OrderBy(c => c.SortOrder)
            .ThenByDescending(c => c.CreatedAtUtc)
            .Select(c => new ContainerListItem(
                c.Id,
                c.Code,
                c.Name,
                c.IsActive,
                db.Devices.Count(d => d.TenantId == tenantId && d.ContainerId == c.Id),
                c.CreatedByName,
                c.CreatedAtUtc))
            .ToListAsync(ct);
    }

    public Task<ImportContainer?> FindAsync(
        Guid tenantId, Guid id, CancellationToken ct = default) =>
        db.Containers.FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId, ct);

    public Task<bool> CodeTakenAsync(
        Guid tenantId, string normalizedCode, CancellationToken ct = default) =>
        db.Containers.AnyAsync(
            c => c.TenantId == tenantId && c.NormalizedCode == normalizedCode, ct);

    public void Add(ImportContainer container) => db.Containers.Add(container);

    public Task<int> CountDevicesAsync(
        Guid tenantId, Guid containerId, CancellationToken ct = default) =>
        db.Devices.CountAsync(
            d => d.TenantId == tenantId && d.ContainerId == containerId, ct);

    public async Task<IReadOnlyList<ContainerDeviceItem>> DevicesAsync(
        Guid tenantId, Guid containerId, CancellationToken ct = default)
    {
        /*
          ⚠️ **الفحوص الممسوحة مستبعدة من العدّ.**

          عدّاد بيحسب فحص اتمسح بيدّي تاريخ جهاز غلط — نفس قاعدة
          `DeviceQueries.ScopedReports` في المشروع القديم.
        */
        var reports = db.Reports.AsNoTracking()
            .Where(r => r.TenantId == tenantId && !r.IsDeleted);

        /*
          🔴 **والمدموج مستبعد من المحتوى.**

          صف الجهاز المدموج بيفضل عشان التاريخ والكود المتقاعد، بس
          عدّه هنا بيقول «١٠ لابات في الحاوية» والحقيقة ٧.
        */
        return await db.Devices.AsNoTracking()
            .Where(d => d.TenantId == tenantId
                        && d.Status != DeviceLifecycleStatus.Merged
                        && d.ContainerId == containerId)
            .OrderBy(d => d.PublicCode)
            .Select(d => new ContainerDeviceItem(
                d.Id,
                d.PublicCode,
                d.LastKnownManufacturer,

                // ⚠️ الاسم التجاري لو موجود، وإلا الموديل الخام.
                // الفني بيعرف اللاب بـ«ThinkPad T480» مش بـ«20L5».
                string.IsNullOrWhiteSpace(d.CommercialModelName)
                    ? d.LastKnownModel
                    : d.CommercialModelName!,

                d.LastSeenAtUtc,
                reports.Count(r => r.DeviceId == d.Id),

                // ⚠️ فشل + خطأ في **آخر** فحص بس. الفحوص القديمة
                // مالهاش لازمة هنا: اللي المدير عايز يعرفه إن اللاب
                // ده سليم دلوقتي ولا لأ.
                reports.Where(r => r.DeviceId == d.Id)
                       .OrderByDescending(r => r.StartedAtUtc)
                       .Select(r => r.FailCount + r.ErrorCount)
                       .FirstOrDefault()))
            .ToListAsync(ct);
    }
}
